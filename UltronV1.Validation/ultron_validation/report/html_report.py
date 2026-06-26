"""Automatic HTML validation report."""

from __future__ import annotations

import base64
from datetime import datetime, timezone
from pathlib import Path

from jinja2 import Template

REPORT_TEMPLATE = """<!DOCTYPE html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8"/>
  <title>Ultron V1 — Validation Report (Phase 4.5)</title>
  <style>
    body { font-family: 'Segoe UI', system-ui, sans-serif; margin: 2rem; color: #1a1a2e; background: #f8f9fc; }
    h1 { color: #0f3460; border-bottom: 3px solid #e94560; padding-bottom: 0.5rem; }
    h2 { color: #16213e; margin-top: 2rem; }
    .meta { color: #666; font-size: 0.9rem; }
    .verdict { font-size: 1.3rem; padding: 1rem; border-radius: 8px; margin: 1rem 0; }
    .verdict.stable { background: #d4edda; border: 1px solid #28a745; }
    .verdict.warn { background: #fff3cd; border: 1px solid #ffc107; }
    .verdict.risk { background: #f8d7da; border: 1px solid #dc3545; }
    table { border-collapse: collapse; width: 100%; margin: 1rem 0; background: white; }
    th, td { border: 1px solid #dee2e6; padding: 0.5rem 0.75rem; text-align: left; }
    th { background: #0f3460; color: white; }
    tr:nth-child(even) { background: #f1f3f5; }
    .metric-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 1rem; }
    .metric-card { background: white; padding: 1rem; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
    .metric-card .value { font-size: 1.5rem; font-weight: 700; color: #e94560; }
    .metric-card .label { font-size: 0.85rem; color: #666; }
    img { max-width: 100%; border-radius: 8px; margin: 1rem 0; box-shadow: 0 2px 8px rgba(0,0,0,.1); }
    .question { background: white; padding: 1rem; margin: 0.5rem 0; border-left: 4px solid #0f3460; }
    .answer-yes { color: #28a745; font-weight: 600; }
    .answer-no { color: #dc3545; font-weight: 600; }
    .answer-partial { color: #ffc107; font-weight: 600; }
  </style>
</head>
<body>
  <h1>Ultron V1 — Validation Framework Report</h1>
  <p class="meta">Generated: {{ generated_at }} | Mode: {{ mode }} | Rows: {{ row_count }}</p>

  <div class="verdict {{ verdict_class }}">
    <strong>Verdict:</strong> {{ stability_verdict }}
  </div>

  <h2>Executive Questions</h2>
  {% for q in executive_questions %}
  <div class="question">
    <strong>{{ q.question }}</strong><br/>
    <span class="{{ q.class }}">{{ q.answer }}</span>
    {% if q.detail %}<br/><small>{{ q.detail }}</small>{% endif %}
  </div>
  {% endfor %}

  <h2>Signal Overlap — Ultron vs NightRanger</h2>
  <table>
    <tr><th>Match Type</th><th>Count</th><th>%</th></tr>
    {% for k, v in overlap.items() %}
    <tr><td>{{ k }}</td><td>{{ v.count }}</td><td>{{ "%.1f"|format(v.pct * 100) }}%</td></tr>
    {% endfor %}
  </table>

  <h2>Performance Comparison</h2>
  <div class="metric-grid">
    {% for label, val in comparison_metrics %}
    <div class="metric-card"><div class="label">{{ label }}</div><div class="value">{{ val }}</div></div>
    {% endfor %}
  </div>

  <table>
    <tr><th>System</th><th>Trades</th><th>Win Rate</th><th>Profit Factor</th><th>Expectancy</th><th>Max DD</th></tr>
    <tr><td>Ultron (accepted)</td>
      <td>{{ ultron_m.tradeCount }}</td><td>{{ "%.1f"|format(ultron_m.winRate * 100) }}%</td>
      <td>{{ "%.2f"|format(ultron_m.profitFactor) }}</td><td>{{ "%.2f"|format(ultron_m.expectancy) }}</td>
      <td>{{ "%.1f"|format(ultron_m.maxDrawdown) }}</td></tr>
    <tr><td>NightRanger (trades)</td>
      <td>{{ nr_m.tradeCount }}</td><td>{{ "%.1f"|format(nr_m.winRate * 100) }}%</td>
      <td>{{ "%.2f"|format(nr_m.profitFactor) }}</td><td>{{ "%.2f"|format(nr_m.expectancy) }}</td>
      <td>{{ "%.1f"|format(nr_m.maxDrawdown) }}</td></tr>
    <tr><td>Both (overlap)</td>
      <td>{{ both_m.tradeCount }}</td><td>{{ "%.1f"|format(both_m.winRate * 100) }}%</td>
      <td>{{ "%.2f"|format(both_m.profitFactor) }}</td><td>{{ "%.2f"|format(both_m.expectancy) }}</td>
      <td>—</td></tr>
  </table>

  {% if timing %}
  <h2>Timing Analysis</h2>
  <table>
    <tr><th>Metric</th><th>Value</th></tr>
    {% for k, v in timing.items() %}
    <tr><td>{{ k }}</td><td>{{ "%.2f"|format(v|float) }}</td></tr>
    {% endfor %}
  </table>
  {% endif %}

  <h2>Parameter Sweep — Baseline vs Best</h2>
  <table>
    <tr><th>Parameter</th><th>Baseline</th><th>Best (by PF)</th></tr>
    {% for param in sweep_params %}
    <tr><td>{{ param.name }}</td><td>{{ param.baseline }}</td><td>{{ param.best }}</td></tr>
    {% endfor %}
    <tr><td>Profit Factor</td><td>{{ "%.2f"|format(sweep_baseline_pf) }}</td><td>{{ "%.2f"|format(sweep_best_pf) }}</td></tr>
    <tr><td>Robustness Score</td><td colspan="2">{{ "%.1f"|format(robustness * 100) }}% of grid within 80% of best PF</td></tr>
  </table>

  {% for img in heatmap_images %}
  <h2>{{ img.title }}</h2>
  <img src="data:image/png;base64,{{ img.data }}" alt="{{ img.title }}"/>
  {% endfor %}

  <h2>Walk-Forward Windows</h2>
  {% if wf_windows %}
  <table>
    <tr><th>Window</th><th>Test Period</th><th>Ultron PF</th><th>NR PF</th><th>PF Delta</th></tr>
    {% for w in wf_windows %}
    <tr>
      <td>{{ w.window }}</td>
      <td>{{ w.test_start[:10] }} → {{ w.test_end[:10] }}</td>
      <td>{{ "%.2f"|format(w.ultron_pf) }}</td>
      <td>{{ "%.2f"|format(w.nr_pf) }}</td>
      <td>{{ "%+.2f"|format(w.pf_delta) }}</td>
    </tr>
    {% endfor %}
  </table>
  <p>Ultron wins {{ "%.0f"|format(wf_ultron_wins_pct * 100) }}% of walk-forward windows (PF delta &gt; 0).</p>
  {% else %}
  <p>Insufficient data for walk-forward analysis.</p>
  {% endif %}

  <h2>Monte Carlo Simulation ({{ mc_sims }} runs)</h2>
  <table>
    <tr><th>Metric</th><th>Ultron p5</th><th>Ultron p50</th><th>Ultron p95</th><th>NR p50</th></tr>
    <tr><td>Profit Factor</td>
      <td>{{ "%.2f"|format(mc_u_pf.p5) }}</td><td>{{ "%.2f"|format(mc_u_pf.p50) }}</td><td>{{ "%.2f"|format(mc_u_pf.p95) }}</td>
      <td>{{ "%.2f"|format(mc_n_pf.p50) }}</td></tr>
    <tr><td>Max Drawdown</td>
      <td>{{ "%.1f"|format(mc_u_dd.p5) }}</td><td>{{ "%.1f"|format(mc_u_dd.p50) }}</td><td>{{ "%.1f"|format(mc_u_dd.p95) }}</td>
      <td>{{ "%.1f"|format(mc_n_dd.p50) }}</td></tr>
  </table>
  <p>Probability of profitable sequence — Ultron: {{ "%.1f"|format(mc_u_prob * 100) }}% | NR: {{ "%.1f"|format(mc_n_prob * 100) }}%</p>

  <h2>Stability &amp; Overfitting</h2>
  <table>
    <tr><th>Test</th><th>Result</th></tr>
    <tr><td>Temporal stability (1st vs 2nd half PF)</td><td>{{ temporal_stable }}</td></tr>
    <tr><td>Parameter sensitivity</td><td>{{ param_sensitive }}</td></tr>
    <tr><td>Overfitting risk</td><td>{{ overfit_risk }}</td></tr>
    <tr><td>In-sample PF / OOS PF gap</td><td>{{ pf_gap }}</td></tr>
  </table>

  <p class="meta" style="margin-top:3rem;">
    Ultron V1 Phase 4.5 — Validation Framework. No engine logic modified. Manual promotion only.
  </p>
</body>
</html>
"""


def _encode_image(path: Path) -> str:
    return base64.b64encode(path.read_bytes()).decode("ascii")


def _empty_metrics():
    return {"tradeCount": 0, "winRate": 0, "profitFactor": 0, "expectancy": 0, "maxDrawdown": 0}


def _executive_questions(comp, stability, mc, sweep) -> list[dict]:
    u_pf = comp.ultron_metrics.get("profitFactor", 0)
    n_pf = comp.nr_metrics.get("profitFactor", 0)
    u_dd = abs(comp.ultron_metrics.get("maxDrawdown", 0))
    n_dd = abs(comp.nr_metrics.get("maxDrawdown", 0))

    timing = comp.timing or {}
    before_pct = timing.get("ultronBeforeNrPct", 0)

    questions = [
        {
            "question": "O Ultron realmente entrou antes?",
            "answer": "Sim" if before_pct > 0.4 else "Parcial" if before_pct > 0.2 else "Não / dados insuficientes",
            "class": "answer-yes" if before_pct > 0.4 else "answer-partial" if before_pct > 0.2 else "answer-no",
            "detail": f"Ultron antes do NR em {before_pct:.0%} dos casos sobrepostos.",
        },
        {
            "question": "Entrou melhor (maior Profit Factor)?",
            "answer": "Sim" if u_pf > n_pf * 1.05 else "Parcial" if u_pf >= n_pf else "Não",
            "class": "answer-yes" if u_pf > n_pf * 1.05 else "answer-partial" if u_pf >= n_pf else "answer-no",
            "detail": f"Ultron PF={u_pf:.2f} vs NR PF={n_pf:.2f}",
        },
        {
            "question": "Reduziu drawdown?",
            "answer": "Sim" if u_dd < n_dd * 0.9 else "Parcial" if u_dd <= n_dd else "Não",
            "class": "answer-yes" if u_dd < n_dd * 0.9 else "answer-partial" if u_dd <= n_dd else "answer-no",
            "detail": f"Ultron maxDD={u_dd:.1f} vs NR maxDD={n_dd:.1f}",
        },
        {
            "question": "Quais parâmetros são mais robustos?",
            "answer": f"Robustness score: {sweep.robustness_score:.0%}" if sweep else "N/A",
            "class": "answer-yes" if sweep and sweep.robustness_score > 0.5 else "answer-partial",
            "detail": str(sweep.best_by_pf.params) if sweep and sweep.best_by_pf else "",
        },
        {
            "question": "Existe overfitting?",
            "answer": stability.overfitting.get("overfitRisk", "unknown").upper(),
            "class": "answer-no" if stability.overfitting.get("overfitRisk") == "low" else "answer-partial" if stability.overfitting.get("overfitRisk") == "moderate" else "answer-no" if stability.overfitting.get("overfitRisk") == "high" else "answer-partial",
            "detail": f"IS/OOS PF gap: {stability.overfitting.get('pfGap', 0):.2f}",
        },
    ]
    return questions


def generate_html_report(
    *,
    comp,
    sweep,
    wf,
    mc,
    stability,
    heatmap_paths: dict[str, Path],
    output_path: Path,
    mode: str,
    row_count: int,
) -> Path:
    u_mc = mc.get("ultron", {})
    n_mc = mc.get("nightRanger", {})

    verdict = stability.verdict
    verdict_class = "stable" if verdict in ("stable_evolution", "improving") else "warn" if verdict == "needs_more_data" else "risk"

    heatmap_images = []
    titles = {
        "heatmap_score_rvol_pf.png": "Heatmap — Score × RVOL → Profit Factor",
        "heatmap_score_rvol_wr.png": "Heatmap — Score × RVOL → Win Rate",
        "heatmap_score_adx_pf.png": "Heatmap — Score × ADX → Profit Factor",
        "heatmap_delta_rvol_exp.png": "Heatmap — Delta × RVOL → Expectancy",
    }
    for fname, path in heatmap_paths.items():
        if path.exists():
            heatmap_images.append({"title": titles.get(fname, fname), "data": _encode_image(path)})

    baseline_p = sweep.baseline.params if sweep else {}
    best_p = sweep.best_by_pf.params if sweep and sweep.best_by_pf else {}
    sweep_params = [
        {"name": k, "baseline": baseline_p.get(k, "—"), "best": best_p.get(k, "—")}
        for k in ("score_min", "rvol_min", "delta_min", "adx_min")
    ]

    wf_windows = []
    for w in (wf.windows if wf else []):
        wf_windows.append({
            "window": w.window,
            "test_start": w.test_start,
            "test_end": w.test_end,
            "ultron_pf": w.ultron_metrics.get("profitFactor", 0),
            "nr_pf": w.nr_metrics.get("profitFactor", 0),
            "pf_delta": w.pf_delta,
        })

    ctx = {
        "generated_at": datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC"),
        "mode": mode,
        "row_count": row_count,
        "stability_verdict": verdict.replace("_", " ").title(),
        "verdict_class": verdict_class,
        "executive_questions": _executive_questions(comp, stability, mc, sweep),
        "overlap": comp.overlap,
        "comparison_metrics": [
            ("Ultron Signals", comp.ultron_metrics.get("tradeCount", 0)),
            ("NR Trades", comp.nr_metrics.get("tradeCount", 0)),
            ("Both Match", comp.overlap.get("Both", {}).get("count", 0)),
            ("Score Δ (both)", f"{comp.score_delta_when_both.get('mean', 0):+.1f}" if comp.score_delta_when_both else "—"),
        ],
        "ultron_m": comp.ultron_metrics or _empty_metrics(),
        "nr_m": comp.nr_metrics or _empty_metrics(),
        "both_m": comp.both_metrics or _empty_metrics(),
        "timing": comp.timing,
        "sweep_params": sweep_params,
        "sweep_baseline_pf": sweep.baseline.metrics.get("profitFactor", 0) if sweep else 0,
        "sweep_best_pf": sweep.best_by_pf.metrics.get("profitFactor", 0) if sweep and sweep.best_by_pf else 0,
        "robustness": sweep.robustness_score if sweep else 0,
        "heatmap_images": heatmap_images,
        "wf_windows": wf_windows,
        "wf_ultron_wins_pct": wf.aggregate.get("ultronWinsPct", 0) if wf else 0,
        "mc_sims": u_mc.get("simulations", 0),
        "mc_u_pf": u_mc.get("pf", {"p5": 0, "p50": 0, "p95": 0}),
        "mc_n_pf": n_mc.get("pf", {"p5": 0, "p50": 0, "p95": 0}),
        "mc_u_dd": u_mc.get("max_drawdown", {"p5": 0, "p50": 0, "p95": 0}),
        "mc_n_dd": n_mc.get("max_drawdown", {"p5": 0, "p50": 0, "p95": 0}),
        "mc_u_prob": u_mc.get("probability_profitable", 0),
        "mc_n_prob": n_mc.get("probability_profitable", 0),
        "temporal_stable": "Stable" if stability.temporal_stability.get("stable") else "Unstable",
        "param_sensitive": "Robust" if stability.parameter_sensitivity.get("robust") else "Sensitive",
        "overfit_risk": stability.overfitting.get("overfitRisk", "unknown"),
        "pf_gap": f"{stability.overfitting.get('pfGap', 0):.2f}",
    }

    html = Template(REPORT_TEMPLATE).render(**ctx)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(html, encoding="utf-8")
    return output_path
