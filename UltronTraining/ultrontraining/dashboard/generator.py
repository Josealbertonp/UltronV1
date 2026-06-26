"""Training dashboard HTML."""

from __future__ import annotations

import base64
import io
import json
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from jinja2 import Template


TEMPLATE = """<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8"/>
<title>Ultron Training Dashboard</title>
<style>
body{font-family:system-ui;background:#0f1419;color:#e6edf3;margin:0}
header{padding:1.5rem 2rem;background:#161b22;border-bottom:1px solid #30363d}
section{margin:1.5rem 2rem;padding:1.5rem;background:#161b22;border-radius:8px;border:1px solid #30363d}
h2{color:#58a6ff;margin-top:0}
table{width:100%;border-collapse:collapse;font-size:.9rem}
th,td{border:1px solid #30363d;padding:.5rem}th{background:#21262d}
img{max-width:100%;border-radius:6px;margin-top:1rem}
.champion{color:#3fb950;font-weight:600}
</style></head><body>
<header><h1>Ultron Training Dashboard</h1>
<p>Champion: <span class="champion">{{ champion }}</span> · Dataset: {{ dataset }} · RC2 Phase 3</p></header>
<section><h2>Model Comparison</h2>{{ comparison_table }}</section>
<section><h2>ROC Curve (Champion)</h2>{% if roc_img %}<img src="data:image/png;base64,{{ roc_img }}"/>{% endif %}</section>
<section><h2>PR Curve (Champion)</h2>{% if pr_img %}<img src="data:image/png;base64,{{ pr_img }}"/>{% endif %}</section>
<section><h2>Calibration</h2>{{ calibration_info }}</section>
<section><h2>Thresholds</h2>{{ thresholds_table }}</section>
<section><h2>SHAP Top Features</h2>{{ shap_table }}</section>
<section><h2>Confusion Matrix (Test)</h2>{{ confusion }}</section>
</body></html>"""


class TrainingDashboard:
    def __init__(self, output_dir: Path):
        self._dir = output_dir / "dashboard"
        self._dir.mkdir(parents=True, exist_ok=True)

    def generate(self, report: dict) -> Path:
        champion = report.get("champion", {})
        roc_img = self._plot_roc(champion.get("roc_curve", {}))
        pr_img = self._plot_pr(champion.get("pr_curve", {}))

        html = Template(TEMPLATE).render(
            champion=champion.get("name", "N/A"),
            dataset=report.get("dataset_version", ""),
            comparison_table=_table_html(report.get("comparison", [])),
            roc_img=roc_img,
            pr_img=pr_img,
            calibration_info=json.dumps(champion.get("calibration", {}), indent=2),
            thresholds_table=_table_html(report.get("thresholds", {}).get("candidates", [])[:15]),
            shap_table=_table_html(champion.get("shap", {}).get("top_20", [])),
            confusion=str(champion.get("test_metrics", {}).get("confusion_matrix", [])),
        )
        path = self._dir / "index.html"
        path.write_text(html, encoding="utf-8")
        return path

    def _plot_roc(self, curve: dict) -> str | None:
        if not curve.get("fpr"):
            return None
        fig, ax = plt.subplots(figsize=(6, 5))
        ax.plot(curve["fpr"], curve["tpr"], color="#58a6ff")
        ax.plot([0, 1], [0, 1], "--", color="#8b949e")
        ax.set_title("ROC")
        ax.set_facecolor("#161b22")
        fig.patch.set_facecolor("#161b22")
        return _fig_b64(fig)

    def _plot_pr(self, curve: dict) -> str | None:
        if not curve.get("recall"):
            return None
        fig, ax = plt.subplots(figsize=(6, 5))
        ax.plot(curve["recall"], curve["precision"], color="#d2a8ff")
        ax.set_title("Precision-Recall")
        ax.set_facecolor("#161b22")
        fig.patch.set_facecolor("#161b22")
        return _fig_b64(fig)


def _fig_b64(fig) -> str:
    buf = io.BytesIO()
    fig.savefig(buf, format="png", dpi=100, facecolor="#161b22")
    plt.close(fig)
    return base64.b64encode(buf.getvalue()).decode()


def _table_html(rows: list[dict]) -> str:
    if not rows:
        return "<p>No data</p>"
    import pandas as pd
    return pd.DataFrame(rows).to_html(index=False)
