"""Metric heatmaps from parameter sweep."""

from __future__ import annotations

from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import pandas as pd
import seaborn as sns


def generate_heatmaps(grid_df: pd.DataFrame, output_dir: Path) -> dict[str, Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    paths: dict[str, Path] = {}

    if grid_df.empty:
        return paths

    pairs = [
        ("score_min", "rvol_min", "m_profitFactor", "heatmap_score_rvol_pf.png"),
        ("score_min", "rvol_min", "m_winRate", "heatmap_score_rvol_wr.png"),
        ("score_min", "adx_min", "m_profitFactor", "heatmap_score_adx_pf.png"),
        ("delta_min", "rvol_min", "m_expectancy", "heatmap_delta_rvol_exp.png"),
    ]

    for x, y, metric, filename in pairs:
        if x not in grid_df.columns or y not in grid_df.columns or metric not in grid_df.columns:
            continue
        pivot = grid_df.pivot_table(index=y, columns=x, values=metric, aggfunc="mean")
        if pivot.empty:
            continue

        fig, ax = plt.subplots(figsize=(10, 6))
        sns.heatmap(pivot, annot=True, fmt=".2f", cmap="RdYlGn", ax=ax)
        ax.set_title(f"{metric} — {x} vs {y}")
        path = output_dir / filename
        fig.tight_layout()
        fig.savefig(path, dpi=120)
        plt.close(fig)
        paths[filename] = path

    return paths
