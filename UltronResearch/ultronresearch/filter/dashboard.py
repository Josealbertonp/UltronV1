"""ML Filter Dashboard HTML."""

from __future__ import annotations

import json
from pathlib import Path

import pandas as pd
from jinja2 import Template

TEMPLATE = """<!DOCTYPE html>
<html><head><meta charset="utf-8"/><title>ML Filter Dashboard</title>
<style>
body{font-family:system-ui;background:#0f1419;color:#e6edf3;padding:2rem}
section{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:1.5rem;margin-bottom:1.5rem}
h2{color:#58a6ff} table{width:100%;border-collapse:collapse} th,td{border:1px solid #30363d;padding:.5rem}
</style></head><body>
<h1>ML Filter Dashboard — RC2 Phase 4B</h1>
<section><h2>PASS vs BLOCK</h2>
<p>Passed: {{ passed }} · Blocked: {{ blocked }}</p>
{{ pass_block_table }}
</section>
<section><h2>Shadow Comparison</h2><pre>{{ shadow }}</pre></section>
<section><h2>Filter Quality</h2>
<p>False blocks (blocked winners): {{ false_blocks }}</p>
<p>True blocks (blocked losers): {{ true_blocks }}</p>
<p>Precision: {{ precision }}</p>
<p>Brier: {{ brier }}</p>
</section>
<section><h2>Improvement vs All Trades</h2><pre>{{ improvement }}</pre></section>
<section><h2>Threshold Optimizer</h2>{{ threshold_table }}</section>
</body></html>"""


def render_filter_dashboard(analysis: dict, output_dir: Path) -> Path:
    out = Path(output_dir) / "ml_filter_dashboard"
    out.mkdir(parents=True, exist_ok=True)

    rows = [
        {"segment": "PASSED", **analysis.get("passed_metrics", {})},
        {"segment": "BLOCKED", **analysis.get("blocked_metrics", {})},
        {"segment": "ALL", **analysis.get("shadow_comparison", {}).get("all_metrics", {})},
    ]

    html = Template(TEMPLATE).render(
        passed=analysis.get("passed_count", 0),
        blocked=analysis.get("blocked_count", 0),
        pass_block_table=pd.DataFrame(rows).to_html(index=False) if rows else "",
        shadow=json.dumps(analysis.get("shadow_comparison", {}), indent=2, default=str),
        false_blocks=analysis.get("false_blocks", 0),
        true_blocks=analysis.get("true_blocks", 0),
        precision=analysis.get("filter_precision"),
        brier=analysis.get("brier_score"),
        improvement=json.dumps(analysis.get("improvement", {}), indent=2),
        threshold_table=pd.DataFrame(
            analysis.get("threshold_study", {}).get("candidates", [])
        ).to_html(index=False),
    )

    path = out / "index.html"
    path.write_text(html, encoding="utf-8")
    (out / "analysis.json").write_text(json.dumps(analysis, indent=2, default=str), encoding="utf-8")
    return path
