#!/usr/bin/env python3
"""Ultron production champion training — offline only (all historical sessions)."""

from __future__ import annotations

import json
import re
import shutil
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import pandas as pd

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "UltronML"))
sys.path.insert(0, str(ROOT / "UltronTraining"))

from ultronml.dataset.builder import DatasetBuilder
from ultronml.dataset.versioning import DatasetVersionManager
from ultronml.io.jsonl import iter_jsonl, parse_jsonl_line

from ultrontraining.config import TrainingConfig
from ultrontraining.data.feature_selection import (
    mutual_information_ranking,
    permutation_importance_ranking,
    select_features,
)
from ultrontraining.data.features import build_feature_matrix, validate_features
from ultrontraining.metrics.classification import compute_classification_metrics, compute_trading_metrics
from ultrontraining.pipeline.training_pipeline import TrainingPipeline


SESSIONS_ROOT = Path.home() / "Documents/NinjaTrader 8/UltronV1/dataset"
DATASETS_DIR = ROOT / "UltronML" / "datasets"
OUTPUT_ROOT = ROOT / "UltronTraining" / "trained_models"
CHAMPION_DIR = OUTPUT_ROOT / "champion_candidate"
THRESHOLD_GRID = [0.50, 0.55, 0.60, 0.65, 0.70, 0.75, 0.80, 0.85, 0.90]
_SIGNAL_ID_RE = re.compile(r'"signalId"\s*:\s*"([^"]+)"')


def _lookup_opportunities(opp_path: Path, needed: set[str]) -> dict[str, dict]:
    """Scan opportunities.jsonl and return records for requested signalIds."""
    pending = set(needed)
    found: dict[str, dict] = {}
    with opp_path.open(encoding="utf-8-sig") as fh:
        for line in fh:
            if not pending:
                break
            m = _SIGNAL_ID_RE.search(line)
            if m is None:
                continue
            sid = m.group(1)
            if sid not in pending or sid in found:
                continue
            rec = parse_jsonl_line(line)
            if rec is None:
                continue
            found[sid] = rec
            pending.discard(sid)
    return found


def audit_sessions(sessions_dir: Path) -> dict:
    stats = {
        "session_folders": 0,
        "valid_sessions": 0,
        "corrupt_sessions": [],
        "sessions_with_outcomes": 0,
        "opportunities_lines": 0,
        "outcome_trades": 0,
        "winners": 0,
        "losers": 0,
        "sessions": [],
    }
    for sd in sorted(sessions_dir.iterdir()):
        if not sd.is_dir():
            continue
        stats["session_folders"] += 1
        opp_path = sd / "opportunities.jsonl"
        out_path = sd / "outcomes.jsonl"
        entry = {"id": sd.name, "opportunities_mb": 0, "outcomes": 0, "status": "ok"}
        if not opp_path.exists():
            entry["status"] = "missing_opportunities"
            stats["corrupt_sessions"].append(sd.name)
            stats["sessions"].append(entry)
            continue
        entry["opportunities_mb"] = round(opp_path.stat().st_size / 1e6, 2)
        try:
            if out_path.exists():
                for o in iter_jsonl(out_path):
                    if not o.get("signalId"):
                        continue
                    stats["outcome_trades"] += 1
                    entry["outcomes"] += 1
                    pnl = float(o.get("realizedPnLTicks") or 0)
                    if o.get("isWin") or pnl > 0:
                        stats["winners"] += 1
                    elif pnl < 0:
                        stats["losers"] += 1
                if entry["outcomes"] > 0:
                    stats["sessions_with_outcomes"] += 1
            stats["valid_sessions"] += 1
        except Exception as ex:
            entry["status"] = f"corrupt:{ex}"
            stats["corrupt_sessions"].append(sd.name)
        stats["sessions"].append(entry)
    return stats


def build_dataset_outcomes_first(dataset_root: Path) -> pd.DataFrame:
    """Join outcomes → opportunities by signalId (avoids scanning full 34GB opp files)."""
    builder = DatasetBuilder(dataset_root)
    sessions_dir = dataset_root / "sessions"
    rows: list[dict] = []

    for i, sd in enumerate(sorted(sessions_dir.iterdir()), 1):
        if not sd.is_dir():
            continue
        out_path = sd / "outcomes.jsonl"
        opp_path = sd / "opportunities.jsonl"
        if not out_path.exists() or not opp_path.exists():
            continue

        outcomes = {o["signalId"]: o for o in iter_jsonl(out_path) if o.get("signalId")}
        if not outcomes:
            continue

        needed = set(outcomes.keys())
        total = len(needed)
        found = _lookup_opportunities(opp_path, needed)

        print(f"  session {sd.name}: {len(found)}/{total} joined", flush=True)

        for sid, out in outcomes.items():
            opp = found.get(sid)
            if opp is None:
                continue
            row = builder._flatten_opportunity(opp, sd.name)
            row.update(builder._flatten_outcome_labels(out))
            rows.append(row)

    if not rows:
        return pd.DataFrame()
    df = pd.DataFrame(rows)
    if "timestamp_utc" in df.columns:
        df["timestamp_utc"] = pd.to_datetime(df["timestamp_utc"], utc=True)
        df = df.sort_values("timestamp_utc").reset_index(drop=True)
    return df


def threshold_analysis(y_true: np.ndarray, y_proba: np.ndarray, pnl: np.ndarray) -> list[dict]:
    n = len(y_true)
    results = []
    for t in THRESHOLD_GRID:
        allow = y_proba >= t
        blocked = ~allow
        selected_pnl = pnl[allow]
        base = compute_trading_metrics(y_proba, pnl, threshold=t)
        gross = float(selected_pnl.sum()) if allow.any() else 0.0
        dd = base.get("max_drawdown_ticks") or 0.0
        recovery = round(gross / abs(dd), 4) if dd < 0 else None
        results.append({
            **base,
            "recovery_factor": recovery,
            "trades_blocked": int(blocked.sum()),
            "winners_blocked": int((blocked & (y_true == 1)).sum()),
            "losers_blocked": int((blocked & (y_true == 0)).sum()),
            "trades_total": n,
        })
    return results


def rc1_vs_ml_comparison(y_true: np.ndarray, y_proba: np.ndarray, pnl: np.ndarray, threshold: float) -> dict:
    rc1 = compute_trading_metrics(np.ones_like(y_proba), pnl, threshold=0.0)
    rc1["trades"] = len(pnl)
    rc1["win_rate"] = round(float((pnl > 0).mean()), 4)
    ml_mask = y_proba >= threshold
    ml_pnl = pnl[ml_mask]
    ml = compute_trading_metrics(y_proba, pnl, threshold=threshold)
    blocked = ~ml_mask
    return {
        "rc1": rc1,
        "ml_filter": ml,
        "delta_win_rate": round((ml.get("win_rate") or 0) - rc1["win_rate"], 4),
        "delta_profit_factor": _delta(ml.get("profit_factor"), rc1.get("profit_factor")),
        "delta_expectancy": round((ml.get("expectancy") or 0) - (rc1.get("expectancy") or 0), 4),
        "delta_max_drawdown_ticks": round((ml.get("max_drawdown_ticks") or 0) - (rc1.get("max_drawdown_ticks") or 0), 4),
        "trades_blocked": int(blocked.sum()),
        "blocked_winners": int((blocked & (y_true == 1)).sum()),
        "blocked_losers": int((blocked & (y_true == 0)).sum()),
        "blocked_winners_pct": round(float((blocked & (y_true == 1)).sum()) / max(blocked.sum(), 1), 4),
        "blocked_losers_pct": round(float((blocked & (y_true == 0)).sum()) / max(blocked.sum(), 1), 4),
    }


def _delta(a, b):
    if a is None or b is None:
        return None
    return round(float(a) - float(b), 4)


def write_report(
    path: Path,
    audit: dict,
    dataset_version: str,
    df: pd.DataFrame,
    feat_report: dict,
    selection_report: dict,
    result,
    comparison: dict,
    threshold_rows: list[dict],
    perm_rank: list[dict],
) -> None:
    champ = result.champion
    test_m = champ.get("test_metrics") or {}
    val_m = champ.get("val_metrics") or {}
    cal = champ.get("calibration") or {}
    rec = champ.get("thresholds", {}).get("recommended") or {}
    shap_top = (champ.get("shap") or {}).get("global_importance") or []
    mi_top = selection_report.get("mutual_information") or []

    ready_shadow = test_m.get("roc_auc", 0) >= 0.55 and test_m.get("brier_score", 1) <= 0.30
    ready_active = (
        ready_shadow
        and (comparison.get("delta_profit_factor") or 0) > 0
        and (comparison.get("blocked_losers_pct") or 0) >= 0.55
    )

    lines = [
        "# ML_CHAMPION_REPORT — Ultron Production Training",
        "",
        f"**Generated:** {datetime.now(timezone.utc).isoformat()}",
        f"**Dataset:** `{dataset_version}` ({len(df)} trades)",
        f"**Champion model:** {champ.get('name')}",
        f"**Artifacts:** `{CHAMPION_DIR}`",
        "",
        "> NÃO promovido automaticamente — candidate only.",
        "",
        "---",
        "",
        "## Etapa 1 — Sessões descobertas",
        "",
        f"| Métrica | Valor |",
        f"|---------|-------|",
        f"| Pastas de sessão | {audit['session_folders']} |",
        f"| Sessões válidas | {audit['valid_sessions']} |",
        f"| Sessões com outcomes | {audit['sessions_with_outcomes']} |",
        f"| Sessões corrompidas | {len(audit['corrupt_sessions'])} |",
        f"| Trades (outcomes) | {audit['outcome_trades']} |",
        f"| Winners | {audit['winners']} |",
        f"| Losers | {audit['losers']} |",
    ]
    if len(df):
        lines += [
            f"| Período | {df['timestamp_utc'].min()} → {df['timestamp_utc'].max()} |",
            f"| Dias únicos | {df['timestamp_utc'].dt.date.nunique()} |",
        ]
    if audit["corrupt_sessions"]:
        lines += ["", "Sessões ignoradas: " + ", ".join(audit["corrupt_sessions"])]

    lines += [
        "",
        "## Etapa 2 — Dataset",
        "",
        f"- Parquet: `UltronML/datasets/{dataset_version}.parquet`",
        f"- Labels OK: `{feat_report.get('passed', False)}`",
        f"- Features: {feat_report.get('feature_count')}",
        f"- NaN ratio: {feat_report.get('nan_ratio')}",
        f"- Constant features: {len(feat_report.get('constant_features') or [])}",
        "",
        "## Etapa 3 — Feature engineering",
        "",
    ]
    for step in selection_report.get("steps", []):
        lines.append(f"- {json.dumps(step, ensure_ascii=False)}")
    lines += [
        "",
        "### Top 15 — Mutual Information",
        "",
    ]
    for r in mi_top[:15]:
        lines.append(f"- `{r['feature']}`: {r['mutual_information']}")
    lines += [
        "",
        "## Etapa 4–6 — Modelos, calibração, SHAP",
        "",
        f"| Modelo | Val ROC-AUC | Test ROC-AUC |",
        f"|--------|-------------|--------------|",
    ]
    for row in result.comparison:
        model = row.get("model") or row.get("name", "?")
        val_auc = row.get("roc_auc") or row.get("val_roc_auc")
        test_auc = row.get("test_roc_auc")
        lines.append(f"| {model} | {val_auc} | {test_auc} |")
    lines += [
        "",
        f"**Calibração escolhida:** {champ.get('calibration_method')}",
        f"**Brier (cal):** {cal.get('brier_score')}",
        "",
        "### Top 30 SHAP (fatores de vitória)",
        "",
    ]
    for r in shap_top[:30]:
        lines.append(f"- `{r['feature']}`: {r['mean_abs_shap']}")
    if shap_top:
        lines += [
            "",
            f"**Interpretação:** `{shap_top[0]['feature']}` é o principal driver de P(win) no holdout; "
            "features de order-flow, score e regime dominam a explicação local.",
        ]

    lines += [
        "",
        "## Etapa 7 — Threshold grid",
        "",
        "| T | WR | PF | Exp | DD | RF | Blocked | W blk | L blk |",
        "|---|----|----|-----|----|----|---------|-------|-------|",
    ]
    for r in threshold_rows:
        lines.append(
            f"| {r['threshold']:.2f} | {r.get('win_rate')} | {r.get('profit_factor')} | "
            f"{r.get('expectancy')} | {r.get('max_drawdown_ticks')} | {r.get('recovery_factor')} | "
            f"{r.get('trades_blocked')} | {r.get('winners_blocked')} | {r.get('losers_blocked')} |"
        )
    lines += [
        "",
        f"**Threshold recomendado:** {rec.get('threshold')}",
        "",
        "## Etapa 9 — RC1 vs Ultron+ML (test set)",
        "",
        json.dumps(comparison, indent=2),
        "",
        "## Respostas finais",
        "",
        f"1. **Modelo campeão:** {champ.get('name')}",
        f"2. **ROC AUC (test):** {test_m.get('roc_auc')}",
        f"3. **Precision (test):** {test_m.get('precision')}",
        f"4. **Recall (test):** {test_m.get('recall')}",
        f"5. **Brier Score (test):** {test_m.get('brier_score')}",
        f"6. **Calibration Error (test):** {test_m.get('calibration_error')}",
        f"7. **Threshold recomendado:** {rec.get('threshold')}",
        f"8. **Pronto para Shadow?** {'Sim' if ready_shadow else 'Não — validar mais dados/métricas'}",
        f"9. **Pronto para Active?** {'Sim (com cautela)' if ready_active else 'Não — PF/WR delta insuficiente ou bloqueio de winners alto'}",
        f"10. **Ganho esperado vs RC1:** WR Δ={comparison.get('delta_win_rate')}, PF Δ={comparison.get('delta_profit_factor')}, Exp Δ={comparison.get('delta_expectancy')}",
        "",
        f"**Trades bloqueados pelo ML:** {comparison.get('trades_blocked')} "
        f"({comparison.get('blocked_losers')} ruins, {comparison.get('blocked_winners')} bons)",
        "",
        f"Pipeline time: {result.elapsed_ms:.0f} ms",
    ]
    path.write_text("\n".join(lines), encoding="utf-8")


def _latest_sel_dataset() -> tuple[Path, str, pd.DataFrame]:
    sel_files = sorted(DATASETS_DIR.glob("dataset_v*_sel.parquet"))
    if not sel_files:
        raise SystemExit("No dataset_v*_sel.parquet found — run full pipeline first.")
    path = sel_files[-1]
    df = pd.read_parquet(path)
    return path, path.stem, df


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser(description="Ultron champion training pipeline")
    parser.add_argument(
        "--skip-dataset",
        action="store_true",
        help="Reuse latest dataset_v*_sel.parquet (skip JSONL scan)",
    )
    args = parser.parse_args()

    t0 = time.perf_counter()
    sessions_dir = SESSIONS_ROOT / "sessions"
    print("=== ETAPA 1: audit sessions ===", flush=True)
    audit = audit_sessions(sessions_dir)
    print(json.dumps({k: v for k, v in audit.items() if k != "sessions"}, indent=2))

    selection_path = OUTPUT_ROOT / "feature_selection_report.json"

    if args.skip_dataset:
        print("=== ETAPA 2-3: skipped (reusing latest sel parquet) ===", flush=True)
        export2_path, sel_version, bundle_df = _latest_sel_dataset()
        export2 = type("Export", (), {"version": sel_version, "parquet_path": export2_path})()
        df = bundle_df
        selection_report = json.loads(selection_path.read_text(encoding="utf-8"))
        fm = build_feature_matrix(df)
        feat_report = validate_features(fm, min_rows=80)
    else:
        print("=== ETAPA 2: build dataset (outcomes-first) ===", flush=True)
        df = build_dataset_outcomes_first(SESSIONS_ROOT)
        if len(df) < 80:
            raise SystemExit(f"Insufficient trades: {len(df)} < 80")

        DATASETS_DIR.mkdir(parents=True, exist_ok=True)
        export = DatasetVersionManager(DATASETS_DIR).export(df, validate=True)
        print(f"Exported {export.version}: {export.row_count} rows -> {export.parquet_path}", flush=True)

        print("=== ETAPA 3: feature selection ===", flush=True)
        fm = build_feature_matrix(df)
        X_sel, selection_report = select_features(fm.X, fm.y)
        fm = type(fm)(X=X_sel, y=fm.y, feature_names=list(X_sel.columns), pnl=fm.pnl)
        feat_report = validate_features(fm, min_rows=80)
        selection_path.parent.mkdir(parents=True, exist_ok=True)
        selection_path.write_text(json.dumps(selection_report, indent=2), encoding="utf-8")

        label_cols = [c for c in df.columns if c in (
            "label", "is_win", "is_loss", "realized_pnl_ticks", "timestamp_utc",
            "trade_id", "session", "has_outcome",
        )]
        slim = pd.concat([fm.X.reset_index(drop=True), df[label_cols].reset_index(drop=True)], axis=1)
        sel_version = f"{export.version}_sel"
        export2 = DatasetVersionManager(DATASETS_DIR).export(slim, version=sel_version)

    print("=== ETAPA 4–8: training pipeline ===")
    pipeline = TrainingPipeline(
        TrainingConfig(
            datasets_dir=DATASETS_DIR,
            output_dir=OUTPUT_ROOT,
            dataset_version=export2.version,
            min_rows=80,
            threshold_grid=THRESHOLD_GRID,
            run_shap=True,
            run_walk_forward=True,
        )
    )
    result = pipeline.run()

    # Move champion → champion_candidate
    src = OUTPUT_ROOT / "champion"
    if CHAMPION_DIR.exists():
        shutil.rmtree(CHAMPION_DIR)
    shutil.copytree(src, CHAMPION_DIR)

    # Extended analysis on champion test split
    from ultrontraining.split.temporal import temporal_split
    bundle_df = pd.read_parquet(export2.parquet_path)
    fm2 = build_feature_matrix(bundle_df)
    split = temporal_split(fm2, 0.6, 0.2, 0.2)
    champ = result.champion
    from ultrontraining.models.base import get_trainer_by_name
    trainer = get_trainer_by_name(champ["name"])
    model = champ["model"]
    test_proba = trainer.predict_proba(model, split.test.X.values)[:, 1]
    from ultrontraining.calibration.calibrator import apply_calibration
    cal = champ.get("calibrator")
    if cal is not None:
        test_proba = apply_calibration(cal, test_proba)
    pnl = split.test.pnl.values if split.test.pnl is not None else np.zeros(len(split.test.y))
    threshold_rows = threshold_analysis(split.test.y.values, test_proba, pnl)
    rec_t = (champ.get("thresholds") or {}).get("recommended") or {}
    comparison = rc1_vs_ml_comparison(
        split.test.y.values, test_proba, pnl, float(rec_t.get("threshold") or 0.70)
    )
    perm_rank = permutation_importance_ranking(model, split.test.X, split.test.y)
    perm_path = CHAMPION_DIR / "permutation_importance.json"
    perm_path.write_text(json.dumps(perm_rank[:30], indent=2), encoding="utf-8")

    report_path = ROOT / "ML_CHAMPION_REPORT.md"
    write_report(
        report_path,
        audit,
        export2.version,
        df,
        feat_report.to_dict(),
        selection_report,
        result,
        comparison,
        threshold_rows,
        perm_rank,
    )
    elapsed = (time.perf_counter() - t0) * 1000
    print(f"\nDone in {elapsed:.0f} ms")
    print(f"Champion: {champ['name']} -> {CHAMPION_DIR}", flush=True)
    print(f"Report: {report_path}")


if __name__ == "__main__":
    main()
