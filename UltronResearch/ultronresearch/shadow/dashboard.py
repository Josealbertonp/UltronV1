"""Shadow ML dashboard HTML."""

from __future__ import annotations

import base64
import io
import json
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from jinja2 import Template

from ultronresearch.shadow.analysis import analyze_shadow, load_shadow_predictions


TEMPLATE = """<!DOCTYPE html>
<html><head><meta charset="utf-8"/><title>Ultron Shadow ML Dashboard</title>
<style>
body{font-family:system-ui;background:#0f1419;color:#e6edf3;margin:0;padding:2rem}
section{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:1.5rem;margin-bottom:1.5rem}
h2{color:#58a6ff} table{width:100%;border-collapse:collapse} th,td{border:1px solid #30363d;padding:.5rem}
img{max-width:100%}
</style></head><body>
<h1>Shadow ML Dashboard</h1>
<p>{{ trade_count }} shadow predictions · RC2 Phase 4A</p>
<section><h2>Confidence Distribution</h2>{% if conf_img %}<img src="data:image/png;base64,{{ conf_img }}"/>{% endif %}</section>
<section><h2>Calibration / Reliability</h2><p>Brier Score: {{ brier }}</p>{% if cal_img %}<img src="data:image/png;base64,{{ cal_img }}"/>{% endif %}</section>
<section><h2>Latency</h2><pre>{{ latency }}</pre></section>
<section><h2>Threshold Study (counterfactual)</h2>{{ threshold_table }}</section>
</body></html>"""


def generate_shadow_dashboard(shadow_path: Path, output_dir: Path) -> Path:
    df = load_shadow_predictions(shadow_path)
    analysis = analyze_shadow(df)

    conf_img = _plot_hist(df["confidence"]) if not df.empty else None
    cal_img = _plot_calibration(analysis.get("calibration", [])) if analysis.get("calibration") else None

    html = Template(TEMPLATE).render(
        trade_count=analysis.get("trade_count", 0),
        brier=analysis.get("brier_score"),
        latency=json.dumps(analysis.get("latency", {}), indent=2),
        threshold_table=_table(analysis.get("threshold_study", {}).get("candidates", [])),
        conf_img=conf_img,
        cal_img=cal_img,
    )

    out = Path(output_dir) / "shadow_dashboard"
    out.mkdir(parents=True, exist_ok=True)
    path = out / "index.html"
    path.write_text(html, encoding="utf-8")
    (out / "analysis.json").write_text(json.dumps(analysis, indent=2, default=str), encoding="utf-8")
    return path


def _plot_hist(series):
    fig, ax = plt.subplots(figsize=(8, 4))
    ax.hist(series.dropna(), bins=20, color="#58a6ff")
    ax.set_title("Confidence Distribution")
    ax.set_facecolor("#161b22")
    fig.patch.set_facecolor("#161b22")
    return _b64(fig)


def _plot_calibration(points):
    if not points:
        return None
    xs = [p["mean_confidence"] for p in points]
    ys = [p["observed_win_rate"] for p in points]
    fig, ax = plt.subplots(figsize=(6, 6))
    ax.plot([0, 1], [0, 1], "--", color="#8b949e")
    ax.scatter(xs, ys, color="#3fb950")
    ax.set_title("Reliability Diagram")
    ax.set_xlabel("Mean Confidence")
    ax.set_ylabel("Observed Win Rate")
    ax.set_facecolor("#161b22")
    fig.patch.set_facecolor("#161b22")
    return _b64(fig)


def _b64(fig):
    buf = io.BytesIO()
    fig.savefig(buf, format="png", dpi=100, facecolor="#161b22")
    plt.close(fig)
    return base64.b64encode(buf.getvalue()).decode()


def _table(rows):
    if not rows:
        return "<p>No data</p>"
    import pandas as pd
    return pd.DataFrame(rows).to_html(index=False)
