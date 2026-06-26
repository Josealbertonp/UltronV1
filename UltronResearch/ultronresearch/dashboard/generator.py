"""HTML dashboard generator."""

from __future__ import annotations

import base64
import io
import json
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from jinja2 import Template

from ultronresearch.analytics.metrics import equity_curve


DASHBOARD_TEMPLATE = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<title>Ultron Research Lab</title>
<style>
body { font-family: system-ui, sans-serif; margin: 0; background: #0f1419; color: #e6edf3; }
header { padding: 1.5rem 2rem; background: #161b22; border-bottom: 1px solid #30363d; }
nav { display: flex; flex-wrap: wrap; gap: .5rem; padding: 1rem 2rem; background: #161b22; position: sticky; top: 0; }
nav a { color: #58a6ff; text-decoration: none; padding: .25rem .5rem; border: 1px solid #30363d; border-radius: 4px; font-size: .85rem; }
main { padding: 2rem; max-width: 1400px; margin: 0 auto; }
section { margin-bottom: 3rem; padding: 1.5rem; background: #161b22; border-radius: 8px; border: 1px solid #30363d; }
h2 { margin-top: 0; color: #58a6ff; }
table { width: 100%; border-collapse: collapse; font-size: .9rem; }
th, td { border: 1px solid #30363d; padding: .5rem; text-align: left; }
th { background: #21262d; }
.metric-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 1rem; }
.metric { background: #21262d; padding: 1rem; border-radius: 6px; }
.metric .val { font-size: 1.4rem; font-weight: 600; }
.metric .lbl { font-size: .75rem; color: #8b949e; }
img.chart { max-width: 100%; border-radius: 6px; margin-top: 1rem; }
.note { color: #8b949e; font-size: .9rem; }
</style>
</head>
<body>
<header>
<h1>Ultron Research Lab</h1>
<p>Dataset: {{ dataset_version }} · {{ trade_count }} trades · RC2 Phase 2</p>
</header>
<nav>
<a href="#overview">Overview</a>
<a href="#performance">Performance</a>
<a href="#equity">Equity</a>
<a href="#hours">Horários</a>
<a href="#days">Dias</a>
<a href="#archetypes">Archetypes</a>
<a href="#score">Score</a>
<a href="#tiers">Tiers</a>
<a href="#gates">Gates</a>
<a href="#features">Features</a>
<a href="#mfe-mae">MFE/MAE</a>
<a href="#atm">ATM</a>
<a href="#monte-carlo">Monte Carlo</a>
<a href="#thresholds">Thresholds</a>
</nav>
<main>

<section id="overview">
<h2>Overview</h2>
<div class="metric-grid">
{% for m in overview_metrics %}
<div class="metric"><div class="val">{{ m.value }}</div><div class="lbl">{{ m.label }}</div></div>
{% endfor %}
</div>
</section>

<section id="performance">
<h2>Performance</h2>
{{ tables.performance | safe }}
</section>

<section id="equity">
<h2>Equity & Drawdown</h2>
{% if charts.equity %}<img class="chart" src="data:image/png;base64,{{ charts.equity }}" alt="equity"/>{% endif %}
</section>

<section id="hours">
<h2>Horários</h2>
<p>Best hour: {{ hours.best }} · Worst hour: {{ hours.worst }}</p>
{% if charts.hour_wr %}<img class="chart" src="data:image/png;base64,{{ charts.hour_wr }}" alt="hour wr"/>{% endif %}
{{ tables.hours | safe }}
</section>

<section id="days">
<h2>Dias da Semana</h2>
{{ tables.days | safe }}
</section>

<section id="archetypes">
<h2>Archetypes</h2>
{{ tables.archetypes | safe }}
</section>

<section id="score">
<h2>Score Analysis</h2>
<p>Optimal min score (PF): {{ score.optimal }}</p>
{% if charts.score_pf %}<img class="chart" src="data:image/png;base64,{{ charts.score_pf }}" alt="score pf"/>{% endif %}
</section>

<section id="tiers">
<h2>Tiers</h2>
{{ tables.tiers | safe }}
</section>

<section id="gates">
<h2>Gates</h2>
{{ tables.gates | safe }}
</section>

<section id="features">
<h2>Feature Analysis</h2>
<p class="note">Top features by mutual information (training prep only).</p>
{{ tables.features | safe }}
</section>

<section id="mfe-mae">
<h2>MFE / MAE</h2>
{% if charts.mfe_mae %}<img class="chart" src="data:image/png;base64,{{ charts.mfe_mae }}" alt="mfe mae"/>{% endif %}
</section>

<section id="atm">
<h2>ATM Comparison</h2>
<p class="note">{{ atm.note }}</p>
{{ tables.atm | safe }}
</section>

<section id="monte-carlo">
<h2>Monte Carlo</h2>
<div class="metric-grid">
{% for m in monte_metrics %}
<div class="metric"><div class="val">{{ m.value }}</div><div class="lbl">{{ m.label }}</div></div>
{% endfor %}
</div>
{% if charts.monte_carlo %}<img class="chart" src="data:image/png;base64,{{ charts.monte_carlo }}" alt="monte carlo"/>{% endif %}
</section>

<section id="thresholds">
<h2>Threshold Optimizer</h2>
{{ tables.thresholds | safe }}
</section>

</main>
</body>
</html>"""


def _df_to_html_table(rows: list[dict], columns: list[str] | None = None) -> str:
    if not rows:
        return "<p class='note'>No data</p>"
    import pandas as pd
    df = pd.DataFrame(rows)
    if columns:
        df = df[[c for c in columns if c in df.columns]]
    return df.to_html(index=False, classes="data-table")


def _fig_to_b64(fig) -> str:
    buf = io.BytesIO()
    fig.savefig(buf, format="png", dpi=100, facecolor="#161b22", edgecolor="none")
    plt.close(fig)
    return base64.b64encode(buf.getvalue()).decode("ascii")


class DashboardGenerator:
    def __init__(self, output_dir: Path | str):
        self._dir = Path(output_dir) / "dashboard"
        self._dir.mkdir(parents=True, exist_ok=True)

    def generate(self, results: dict, df) -> Path:
        charts = {}
        charts["equity"] = self._chart_equity(df)
        charts["hour_wr"] = self._chart_hour_heatmap(results)
        charts["score_pf"] = self._chart_score_pf(results)
        charts["mfe_mae"] = self._chart_mfe_mae_scatter(df)
        charts["monte_carlo"] = self._chart_monte_carlo(results)

        gs = results.get("general_stats", {})
        overview = [
            {"label": "Trades", "value": gs.get("trades", 0)},
            {"label": "Win Rate", "value": f"{(gs.get('win_rate', 0) or 0)*100:.1f}%"},
            {"label": "Profit Factor", "value": gs.get("profit_factor")},
            {"label": "Expectancy", "value": gs.get("expectancy")},
            {"label": "Sharpe", "value": gs.get("sharpe_simple")},
            {"label": "Max DD", "value": gs.get("max_drawdown_ticks")},
        ]

        hours = results.get("hours", {})
        best_h = hours.get("best_hour", {}) or {}
        worst_h = hours.get("worst_hour", {}) or {}

        mc = results.get("monte_carlo", {}).get("final_equity", {})
        monte_metrics = [
            {"label": "Mean Equity", "value": mc.get("mean")},
            {"label": "Median", "value": mc.get("median")},
            {"label": "95% CI low", "value": results.get("monte_carlo", {}).get("confidence_interval_95", [None, None])[0]},
            {"label": "95% CI high", "value": results.get("monte_carlo", {}).get("confidence_interval_95", [None, None])[1]},
            {"label": "Worst case", "value": mc.get("worst")},
            {"label": "Best case", "value": mc.get("best")},
        ]

        opt = results.get("score", {}).get("optimal_min_score") or {}
        score_opt = f"{opt.get('min_score')} (PF={opt.get('profit_factor')})" if opt else "N/A"

        ctx = {
            "dataset_version": results.get("dataset", {}).get("version", ""),
            "trade_count": gs.get("trades", 0),
            "overview_metrics": overview,
            "hours": {"best": best_h.get("hour"), "worst": worst_h.get("hour")},
            "score": {"optimal": score_opt},
            "atm": {"note": results.get("atm", {}).get("note", "")},
            "monte_metrics": monte_metrics,
            "charts": charts,
            "tables": {
                "performance": _df_to_html_table([gs]),
                "hours": _df_to_html_table(hours.get("by_hour", [])),
                "days": _df_to_html_table(results.get("days", {}).get("by_day", [])),
                "archetypes": _df_to_html_table(results.get("archetypes", {}).get("by_archetype", [])),
                "tiers": _df_to_html_table(results.get("tiers", {}).get("by_tier", [])),
                "gates": _df_to_html_table(results.get("gates", {}).get("ranking", [])[:15]),
                "features": _df_to_html_table(
                    results.get("features", {}).get("features", [])[:20],
                    ["feature", "mutual_information", "variance"],
                ),
                "atm": _df_to_html_table(results.get("atm", {}).get("by_atm", [])),
                "thresholds": _df_to_html_table(
                    results.get("thresholds", {}).get("score", {}).get("candidates", [])[:10]
                ),
            },
        }

        html = Template(DASHBOARD_TEMPLATE).render(**ctx)
        path = self._dir / "index.html"
        path.write_text(html, encoding="utf-8")
        (self._dir / "data.json").write_text(json.dumps(results, indent=2, default=str), encoding="utf-8")
        return path

    def _chart_equity(self, df) -> str | None:
        if df.empty:
            return None
        eq = equity_curve(df)
        fig, ax = plt.subplots(figsize=(10, 4))
        ax.plot(eq["trade_index"], eq["equity"], color="#58a6ff")
        ax.fill_between(eq["trade_index"], eq["drawdown"], 0, alpha=0.3, color="#f85149")
        ax.set_title("Equity Curve")
        ax.set_facecolor("#161b22")
        fig.patch.set_facecolor("#161b22")
        ax.tick_params(colors="#8b949e")
        return _fig_to_b64(fig)

    def _chart_hour_heatmap(self, results: dict) -> str | None:
        hm = results.get("hours", {}).get("heatmaps", {}).get("hour_x_win_rate", {})
        if not hm:
            return None
        hours = sorted(hm.keys())
        vals = [hm[h] for h in hours]
        fig, ax = plt.subplots(figsize=(10, 3))
        ax.bar([str(h) for h in hours], vals, color="#3fb950")
        ax.set_title("Hour × Win Rate")
        ax.set_facecolor("#161b22")
        fig.patch.set_facecolor("#161b22")
        return _fig_to_b64(fig)

    def _chart_score_pf(self, results: dict) -> str | None:
        curve = results.get("score", {}).get("curves", {}).get("score_x_profit_factor", {})
        if not curve:
            return None
        xs = sorted(curve.keys())
        ys = [curve[x] or 0 for x in xs]
        fig, ax = plt.subplots(figsize=(10, 4))
        ax.plot(xs, ys, color="#d2a8ff")
        ax.set_title("Score Threshold × Profit Factor")
        ax.set_xlabel("Min Score")
        ax.set_facecolor("#161b22")
        fig.patch.set_facecolor("#161b22")
        return _fig_to_b64(fig)

    def _chart_mfe_mae_scatter(self, df) -> str | None:
        if "mfe_ticks" not in df.columns or "mae_ticks" not in df.columns:
            return None
        fig, ax = plt.subplots(figsize=(8, 6))
        colors = ["#3fb950" if w else "#f85149" for w in df.get("is_win", False)]
        ax.scatter(df["mae_ticks"], df["mfe_ticks"], c=colors, alpha=0.5, s=12)
        ax.set_xlabel("MAE")
        ax.set_ylabel("MFE")
        ax.set_title("MFE vs MAE")
        ax.set_facecolor("#161b22")
        fig.patch.set_facecolor("#161b22")
        return _fig_to_b64(fig)

    def _chart_monte_carlo(self, results: dict) -> str | None:
        curves = results.get("monte_carlo", {}).get("sample_curves", [])
        if not curves:
            return None
        fig, ax = plt.subplots(figsize=(10, 4))
        for c in curves[:30]:
            ax.plot(c, alpha=0.15, color="#58a6ff")
        avg = results.get("monte_carlo", {}).get("average_curve", [])
        if avg:
            ax.plot(avg, color="#ffa657", linewidth=2, label="Historical")
        ax.set_title("Monte Carlo Equity Paths")
        ax.set_facecolor("#161b22")
        fig.patch.set_facecolor("#161b22")
        return _fig_to_b64(fig)
