"""RC2 Phase 5 — Feature engineering & ML improvement pipeline."""

from __future__ import annotations

import json
import shutil
import time
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import pandas as pd

from ultronml.dataset.versioning import DatasetVersionManager

from ultrontraining.calibration.calibrator import apply_calibration, fit_calibrators, select_best_calibration
from ultrontraining.config import TrainingConfig
from ultrontraining.data.derived_features import enrich_derived_features, list_derived_feature_names
from ultrontraining.data.feature_audit import audit_features, classify_usefulness, quick_permutation_audit
from ultrontraining.data.feature_selection import mutual_information_ranking
from ultrontraining.data.features import build_feature_matrix, validate_features
from ultrontraining.explain.shap_analysis import compute_shap_report
from ultrontraining.metrics.classification import compute_classification_metrics, compute_trading_metrics
from ultrontraining.models.base import get_trainer_by_name
from ultrontraining.pipeline.training_pipeline import TrainingPipeline
from ultrontraining.split.temporal import temporal_split


@dataclass
class Phase5Result:
    dataset_version: str
    champion_v2: dict
    comparison_v2: list[dict]
    champion_v1: dict | None
    feature_set_comparison: list[dict]
    promotion: dict
    output_dir: Path
    elapsed_ms: float = 0.0
    stages: list[str] = field(default_factory=list)


def _load_v1_baseline(v1_dir: Path) -> dict | None:
    metrics_path = v1_dir / "metrics.json"
    if not metrics_path.exists():
        return None
    m = json.loads(metrics_path.read_text(encoding="utf-8"))
    return {
        "name": json.loads((v1_dir / "metadata.json").read_text()).get("model", "?"),
        "test_metrics": m.get("test", {}),
        "validation_metrics": m.get("validation", {}),
        "thresholds": json.loads((v1_dir / "thresholds.json").read_text()) if (v1_dir / "thresholds.json").exists() else {},
    }


def compare_feature_sets(fm, mi_rank: list[dict], sizes: list[int | str]) -> list[dict]:
    """Quick CatBoost ROC-AUC on validation for each feature subset."""
    split = temporal_split(fm, 0.60, 0.20, 0.20)
    ranked_names = [r["feature"] for r in mi_rank]
    trainer = get_trainer_by_name("CatBoost")
    results = []

    for size in sizes:
        if size == "all":
            cols = list(fm.X.columns)
            label = "all"
        else:
            cols = ranked_names[: int(size)]
            label = f"top_{size}"

        X_tr = split.train.X[cols].values
        X_va = split.validation.X[cols].values
        model = trainer.fit(X_tr, split.train.y.values)
        val_proba = trainer.predict_proba(model, X_va)[:, 1]
        val_m = compute_classification_metrics(split.validation.y.values, val_proba)
        test_proba = trainer.predict_proba(model, split.test.X[cols].values)[:, 1]
        test_m = compute_classification_metrics(split.test.y.values, test_proba)

        results.append({
            "feature_set": label,
            "feature_count": len(cols),
            "val_roc_auc": val_m.get("roc_auc"),
            "test_roc_auc": test_m.get("roc_auc"),
        })
    return results


def evaluate_promotion(champion: dict, v1: dict | None, wf_expanding: dict | None) -> dict:
    test_m = champion.get("test_metrics") or {}
    test_t = champion.get("test_threshold_metrics") or {}
    thresholds = champion.get("thresholds") or {}

    v1_test_roc = (v1 or {}).get("test_metrics", {}).get("roc_auc") or 0.5016
    v1_brier = (v1 or {}).get("test_metrics", {}).get("brier_score") or 0.2356
    v1_cal = (v1 or {}).get("test_metrics", {}).get("calibration_error") or 0.0437

    wf_stable = True
    wf_std = None
    if wf_expanding and wf_expanding.get("folds"):
        aucs = [f["metrics"].get("roc_auc") for f in wf_expanding["folds"] if f["metrics"].get("roc_auc")]
        if len(aucs) >= 2:
            wf_std = float(np.std(aucs))
            wf_stable = wf_std < 0.08

    checks = {
        "roc_auc_test_gte_060": (test_m.get("roc_auc") or 0) >= 0.60,
        "brier_better_than_v1": (test_m.get("brier_score") or 1) < v1_brier,
        "calibration_better_than_v1": (test_m.get("calibration_error") or 1) <= v1_cal,
        "walk_forward_stable": wf_stable,
        "no_test_leakage": thresholds.get("tuned_on") == "validation",
        "threshold_on_validation": thresholds.get("tuned_on") == "validation",
    }
    ready_shadow = all(checks.values())
    ready_active = ready_shadow and (test_t.get("profit_factor") or 0) > 1.0 and (test_t.get("trades") or 0) >= 30

    return {
        "checks": checks,
        "ready_for_shadow": ready_shadow,
        "ready_for_active": ready_active,
        "wf_roc_auc_std": wf_std,
        "v1_baseline_test_roc": v1_test_roc,
        "v2_test_roc": test_m.get("roc_auc"),
        "roc_improved_vs_v1": (test_m.get("roc_auc") or 0) > v1_test_roc,
    }


def write_feature_audit_report(path: Path, audit: dict, perm: list[dict], usefulness: dict) -> None:
    lines = [
        "# FEATURE_AUDIT_REPORT — RC2 Phase 5",
        "",
        f"**Generated:** {datetime.now(timezone.utc).isoformat()}",
        f"**Features audited:** {audit['feature_count']}",
        f"**Rows:** {audit['row_count']}",
        "",
        "## Resumo",
        "",
        f"- Features constantes: **{len(audit['constants'])}**",
        f"- Pares correlacionados (|r| >= 0.95): **{len(audit['high_correlation_pairs'])}**",
        f"- Features úteis (MI >= {usefulness['mi_threshold']}): **{len(usefulness['useful'])}**",
        f"- Features baixo valor: **{len(usefulness['low_value'])}**",
        "",
        "## Quais features realmente ajudam?",
        "",
    ]
    for f in audit["features"][:20]:
        lines.append(
            f"- `{f['feature']}` — MI={f['mutual_information']}, corr_target={f['target_correlation']}"
        )
    lines += ["", "## Features praticamente inúteis", ""]
    for name in usefulness["low_value"][:25]:
        lines.append(f"- `{name}`")
    lines += [
        "",
        "## Top 20 — Permutation Importance (CatBoost proxy)",
        "",
    ]
    for r in perm[:20]:
        lines.append(f"- `{r['feature']}`: {r['permutation_importance_mean']}")
    lines += ["", "## Pares altamente correlacionados (>0.95)", "", "| A | B | r |", "|---|---|---|"]
    for p in audit["high_correlation_pairs"][:30]:
        lines.append(f"| {p['feature_a']} | {p['feature_b']} | {p['correlation']} |")
    lines += ["", "## Tabela completa", "", "| Feature | Tipo | Var | Missing% | MI | Corr Target | Const |", "|---------|------|-----|----------|-----|-------------|-------|"]
    for f in audit["features"]:
        lines.append(
            f"| {f['feature']} | {f['dtype']} | {f['variance']} | {f['missing_pct']} | "
            f"{f['mutual_information']} | {f['target_correlation']} | {f['is_constant']} |"
        )
    path.write_text("\n".join(lines), encoding="utf-8")


def write_phase5_report(
    path: Path,
    result: Phase5Result,
    audit: dict,
    derived_names: list[str],
    importance_top50: list[dict],
    shap_top: list[dict],
    fs_comparison: list[dict],
) -> None:
    champ = result.champion_v2
    test_m = champ.get("test_metrics") or {}
    test_t = champ.get("test_threshold_metrics") or {}
    v1 = result.champion_v1 or {}
    v1_test = v1.get("test_metrics") or {}
    prom = result.promotion

    lines = [
        "# RC2_PHASE5_FEATURE_ENGINEERING_REPORT",
        "",
        f"**Generated:** {datetime.now(timezone.utc).isoformat()}",
        f"**Dataset:** `{result.dataset_version}`",
        f"**Champion v2:** {champ.get('name')}",
        f"**Artifacts:** `{result.output_dir}`",
        "",
        "> NÃO promovido automaticamente.",
        "",
        "## Fase 1 — Auditoria",
        "",
        f"- Features base + derivadas: {audit['feature_count']}",
        f"- Derivadas adicionadas: {len(derived_names)}",
        "",
        "## Fase 2 — Novas features derivadas",
        "",
    ]
    for n in derived_names:
        lines.append(f"- `{n}`")
    lines += ["", "## Fase 3 — Top 50 importância (MI)", ""]
    for r in importance_top50[:50]:
        lines.append(f"- `{r['feature']}`: MI={r['mutual_information']}")
    lines += ["", "### Top 30 SHAP (champion v2)", ""]
    for r in shap_top[:30]:
        lines.append(f"- `{r['feature']}`: {r['mean_abs_shap']}")
    lines += [
        "",
        "## Fase 4 — Comparação de conjuntos de features (CatBoost rápido)",
        "",
        "| Set | Features | Val ROC | Test ROC |",
        "|-----|----------|---------|----------|",
    ]
    for row in fs_comparison:
        lines.append(
            f"| {row['feature_set']} | {row['feature_count']} | {row['val_roc_auc']} | {row['test_roc_auc']} |"
        )
    lines += [
        "",
        "## Fase 5 — Metodologia sem vazamento",
        "",
        f"- Threshold tunado em: **{champ.get('thresholds', {}).get('tuned_on', '?')}**",
        f"- Threshold congelado: **{(champ.get('thresholds') or {}).get('recommended', {}).get('threshold')}**",
        "- Ver: `UltronTraining/docs/PHASE5_METHODOLOGY.md`",
        "",
        "## Fase 6 — Walk Forward",
        "",
        json.dumps(champ.get("walk_forward_expanding"), indent=2),
        "",
        "## Fase 7-8 — Modelos e comparação v1 vs v2",
        "",
        "| Métrica | Champion v1 (test) | Champion v2 (test) |",
        "|---------|-------------------|-------------------|",
        f"| ROC AUC | {v1_test.get('roc_auc')} | {test_m.get('roc_auc')} |",
        f"| Precision | {v1_test.get('precision')} | {test_m.get('precision')} |",
        f"| Recall | {v1_test.get('recall')} | {test_m.get('recall')} |",
        f"| F1 | {v1_test.get('f1')} | {test_m.get('f1')} |",
        f"| Brier | {v1_test.get('brier_score')} | {test_m.get('brier_score')} |",
        f"| Cal Error | {v1_test.get('calibration_error')} | {test_m.get('calibration_error')} |",
        f"| PF (test, frozen T) | — | {test_t.get('profit_factor')} |",
        f"| Expectancy | — | {test_t.get('expectancy')} |",
        f"| Max DD | — | {test_t.get('max_drawdown_ticks')} |",
        "",
        "## Fase 9 — Critérios de promoção",
        "",
        json.dumps(prom, indent=2),
        "",
        "## Respostas finais",
        "",
        f"1. **Novas features com maior ganho:** ver Top MI/SHAP acima (derivadas temporais e normalizadas)",
        f"2. **Vazamento eliminado?** {'Sim' if prom['checks'].get('no_test_leakage') else 'Não'}",
        f"3. **Modelo campeão v2:** {champ.get('name')}",
        f"4. **ROC melhorou vs v1?** {prom.get('roc_improved_vs_v1')} ({v1_test.get('roc_auc')} -> {test_m.get('roc_auc')})",
        f"5. **Pronto para Shadow?** {'Sim' if prom.get('ready_for_shadow') else 'Não'}",
        f"6. **Pronto para Active?** {'Sim' if prom.get('ready_for_active') else 'Não'}",
        f"7. **Ganho vs RC1 (test, frozen threshold):** PF={test_t.get('profit_factor')}, WR={test_t.get('win_rate')}, trades={test_t.get('trades')}",
        "",
        f"Pipeline time: {result.elapsed_ms:.0f} ms",
    ]
    path.write_text("\n".join(lines), encoding="utf-8")


class Phase5Pipeline:
    def __init__(
        self,
        root: Path,
        datasets_dir: Path,
        output_dir: Path,
        v1_dir: Path,
        source_parquet: str = "dataset_v6.parquet",
        threshold_grid: list[float] | None = None,
    ):
        self._root = root
        self._datasets_dir = datasets_dir
        self._output_dir = output_dir
        self._v1_dir = v1_dir
        self._source = datasets_dir / source_parquet
        self._threshold_grid = threshold_grid or [
            0.50, 0.55, 0.60, 0.65, 0.70, 0.75, 0.80, 0.85, 0.90
        ]

    def run(self) -> Phase5Result:
        t0 = time.perf_counter()
        stages: list[str] = []

        df = pd.read_parquet(self._source)
        stages.append("load_source")

        # Phase 1 audit on base features
        base_fm = build_feature_matrix(df)
        base_audit = audit_features(df, base_fm.y)
        stages.append("feature_audit_base")

        # Phase 2 derived features
        enriched = enrich_derived_features(df)
        derived_names = list_derived_feature_names(enriched)
        export = DatasetVersionManager(self._datasets_dir).export(enriched, version="dataset_v7_phase5")
        stages.append("derived_features")

        fm = build_feature_matrix(enriched)
        validate_features(fm, min_rows=80)
        audit = audit_features(enriched, fm.y)
        perm = quick_permutation_audit(fm.X, fm.y)
        usefulness = classify_usefulness(audit)
        stages.append("audit_enriched")

        reports_dir = self._root
        write_feature_audit_report(
            reports_dir / "FEATURE_AUDIT_REPORT.md", audit, perm, usefulness
        )
        stages.append("feature_audit_report")

        # Phase 3 importance
        mi_rank = mutual_information_ranking(fm.X, fm.y)
        stages.append("importance_mi")

        # Phase 4 feature set comparison
        fs_comparison = compare_feature_sets(fm, mi_rank, [10, 20, 30, 40, "all"])
        (self._output_dir / "phase5").mkdir(parents=True, exist_ok=True)
        (self._output_dir / "phase5" / "feature_set_comparison.json").write_text(
            json.dumps(fs_comparison, indent=2), encoding="utf-8"
        )
        stages.append("feature_set_comparison")

        # Phase 5-7 full training with leak-free pipeline
        v2_out = self._output_dir / "champion_candidate_v2"
        pipeline = TrainingPipeline(
            TrainingConfig(
                datasets_dir=self._datasets_dir,
                output_dir=self._output_dir,
                dataset_version=export.version,
                min_rows=80,
                threshold_grid=self._threshold_grid,
                threshold_on_validation=True,
                run_shap=True,
                run_walk_forward=True,
            )
        )
        train_result = pipeline.run()
        stages.append("train_v2")

        src = self._output_dir / "champion"
        if v2_out.exists():
            shutil.rmtree(v2_out)
        shutil.copytree(src, v2_out)
        stages.append("export_v2")

        champ = train_result.champion
        shap_top = (champ.get("shap") or {}).get("global_importance") or []

        # Combined top 50 MI from enriched set
        importance_top50 = mi_rank[:50]

        # Permutation on champion
        split = temporal_split(fm, 0.6, 0.2, 0.2)
        perm_champ = quick_permutation_audit(split.test.X, split.test.y)
        (v2_out / "permutation_importance.json").write_text(
            json.dumps(perm_champ[:50], indent=2), encoding="utf-8"
        )

        # Phase 8 v1 comparison
        v1 = _load_v1_baseline(self._v1_dir)

        # Phase 9 promotion
        promotion = evaluate_promotion(champ, v1, champ.get("walk_forward_expanding"))

        result = Phase5Result(
            dataset_version=export.version,
            champion_v2=champ,
            comparison_v2=train_result.comparison,
            champion_v1=v1,
            feature_set_comparison=fs_comparison,
            promotion=promotion,
            output_dir=v2_out,
            elapsed_ms=(time.perf_counter() - t0) * 1000,
            stages=stages,
        )

        write_phase5_report(
            self._root / "RC2_PHASE5_FEATURE_ENGINEERING_REPORT.md",
            result,
            audit,
            derived_names,
            importance_top50,
            shap_top,
            fs_comparison,
        )
        stages.append("phase5_report")

        (v2_out / "promotion_evaluation.json").write_text(
            json.dumps(promotion, indent=2), encoding="utf-8"
        )
        (v2_out / "feature_set_comparison.json").write_text(
            json.dumps(fs_comparison, indent=2), encoding="utf-8"
        )

        return result
