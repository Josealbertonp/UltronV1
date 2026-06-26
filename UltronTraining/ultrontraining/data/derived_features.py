"""Derived features from existing Ultron dataset columns — offline only, no engine changes."""

from __future__ import annotations

import numpy as np
import pandas as pd


def _safe_div(a, b, eps: float = 1e-9):
    return a / (b.abs() + eps)


def enrich_derived_features(df: pd.DataFrame) -> pd.DataFrame:
    """
    Add derived columns using only pre-existing opportunity/outcome fields.
    Does not invent data; uses NaN where inputs missing.
    """
    out = df.sort_values("timestamp_utc").reset_index(drop=True).copy()

    atr = pd.to_numeric(out.get("feat_Atr"), errors="coerce")
    atr_ticks = pd.to_numeric(out.get("feat_AtrTicks"), errors="coerce")
    bar_range = pd.to_numeric(out.get("feat_BarRangeTicks"), errors="coerce")
    norm = atr_ticks.fillna(atr).fillna(bar_range).replace(0, np.nan)

    # Normalized distances
    if "feat_EmaSpreadTicks" in out.columns:
        out["der_ema_spread_atr"] = _safe_div(out["feat_EmaSpreadTicks"], norm)
    if "feat_VwapDistanceTicks" in out.columns:
        out["der_vwap_distance_atr"] = _safe_div(out["feat_VwapDistanceTicks"], norm)
    if "feat_Atr" in out.columns and "close" in out.columns:
        out["der_atr_pct_close"] = _safe_div(out["feat_Atr"], out["close"]) * 100.0

    # Normalized regime indicators (0-1 scale where applicable)
    if "feat_Adx" in out.columns:
        out["der_adx_norm"] = pd.to_numeric(out["feat_Adx"], errors="coerce") / 100.0
    if "feat_Er" in out.columns:
        out["der_er_norm"] = pd.to_numeric(out["feat_Er"], errors="coerce").clip(0, 1)
    if "feat_Rsi" in out.columns:
        out["der_rsi_norm"] = pd.to_numeric(out["feat_Rsi"], errors="coerce") / 100.0

    # Donchian / range position
    upper = pd.to_numeric(out.get("feat_DonchianUpper"), errors="coerce")
    lower = pd.to_numeric(out.get("feat_DonchianLower"), errors="coerce")
    close = pd.to_numeric(out.get("close"), errors="coerce")
    width = upper - lower
    out["der_donchian_position"] = _safe_div(close - lower, width).clip(0, 1)
    out["der_range_position"] = out["der_donchian_position"]

    # Score dynamics
    if "long_score" in out.columns and "short_score" in out.columns:
        ls = pd.to_numeric(out["long_score"], errors="coerce")
        ss = pd.to_numeric(out["short_score"], errors="coerce")
        out["der_score_spread"] = ls - ss
        out["der_score_dominance"] = _safe_div(ls - ss, ls + ss + 1)

    # Order flow aggregates
    buy = pd.to_numeric(out.get("feat_BuyVolume"), errors="coerce")
    sell = pd.to_numeric(out.get("feat_SellVolume"), errors="coerce")
    vol = buy.fillna(0) + sell.fillna(0)
    out["der_buy_ratio"] = _safe_div(buy, vol)
    out["der_delta_norm"] = _safe_div(out.get("feat_Delta"), vol.replace(0, np.nan))

    # Risk / reward from entry geometry (known at entry)
    entry = pd.to_numeric(out.get("entry_price"), errors="coerce")
    stop = pd.to_numeric(out.get("stop_price"), errors="coerce")
    target = pd.to_numeric(out.get("target_price"), errors="coerce")
    risk = (entry - stop).abs()
    reward = (target - entry).abs()
    out["der_rr_ratio"] = _safe_div(reward, risk)
    out["der_stop_distance_atr"] = _safe_div(risk, norm)
    out["der_target_distance_atr"] = _safe_div(reward, norm)

    # Cyclical time
    if "hour" in out.columns:
        h = pd.to_numeric(out["hour"], errors="coerce")
        out["der_hour_sin"] = np.sin(2 * np.pi * h / 24.0)
        out["der_hour_cos"] = np.cos(2 * np.pi * h / 24.0)
    if "minute" in out.columns:
        m = pd.to_numeric(out["minute"], errors="coerce")
        out["der_minute_sin"] = np.sin(2 * np.pi * m / 60.0)
        out["der_minute_cos"] = np.cos(2 * np.pi * m / 60.0)
    if "day_of_week" in out.columns:
        d = pd.to_numeric(out["day_of_week"], errors="coerce")
        out["der_dow_sin"] = np.sin(2 * np.pi * d / 7.0)
        out["der_dow_cos"] = np.cos(2 * np.pi * d / 7.0)

    # Session minute proxy
    if "hour" in out.columns and "minute" in out.columns:
        out["der_session_minute"] = pd.to_numeric(out["hour"], errors="coerce") * 60 + pd.to_numeric(
            out["minute"], errors="coerce"
        )

    # Regime encoding (existing column only)
    if "regime" in out.columns:
        regime_map = {str(v): i for i, v in enumerate(sorted(out["regime"].dropna().astype(str).unique()))}
        out["der_regime_code"] = out["regime"].astype(str).map(regime_map)

    # Temporal context within session (scores only — no label leakage)
    if "session" in out.columns and "score" in out.columns:
        g = out.groupby("session", sort=False)
        for w in (3, 5, 10):
            out[f"der_score_mean_{w}"] = g["score"].transform(
                lambda s: s.shift(1).rolling(w, min_periods=1).mean()
            )
            out[f"der_score_std_{w}"] = g["score"].transform(
                lambda s: s.shift(1).rolling(w, min_periods=1).std()
            )
        if "long_score" in out.columns:
            for w in (3, 5):
                out[f"der_long_score_mean_{w}"] = g["long_score"].transform(
                    lambda s: s.shift(1).rolling(w, min_periods=1).mean()
                )

    # Volatility proxy change (session-local)
    if "session" in out.columns and "feat_Rvol" in out.columns:
        out["der_rvol_change_3"] = out.groupby("session")["feat_Rvol"].transform(
            lambda s: s - s.shift(3)
        )
        out["der_rvol_mean_5"] = out.groupby("session")["feat_Rvol"].transform(
            lambda s: s.shift(1).rolling(5, min_periods=1).mean()
        )

    # Gate pass count
    gate_cols = [c for c in out.columns if c.startswith("gate_") and c.endswith("_long")]
    if gate_cols:
        out["der_gates_pass_long_count"] = out[gate_cols].apply(pd.to_numeric, errors="coerce").sum(axis=1)

    der_cols = [c for c in out.columns if c.startswith("der_")]
    for c in der_cols:
        out[c] = pd.to_numeric(out[c], errors="coerce")

    return out


def list_derived_feature_names(df: pd.DataFrame) -> list[str]:
    return sorted(c for c in df.columns if c.startswith("der_"))
