"""Probability calibration — Platt vs Isotonic."""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
from sklearn.calibration import CalibratedClassifierCV
from sklearn.isotonic import IsotonicRegression
from sklearn.linear_model import LogisticRegression

from ultrontraining.metrics.classification import compute_classification_metrics


@dataclass
class CalibrationResult:
    method: str
    calibrator: object
    val_metrics: dict
    calibrated_proba: np.ndarray | None = None

    def predict(self, proba: np.ndarray) -> np.ndarray:
        if self.method == "platt":
            return self.calibrator.predict_proba(proba.reshape(-1, 1))[:, 1]
        if self.method == "isotonic":
            return self.calibrator.predict(proba)
        return proba


def fit_calibrators(
    y_val: np.ndarray,
    val_proba: np.ndarray,
) -> tuple[CalibrationResult, CalibrationResult]:
    platt_lr = LogisticRegression(max_iter=500)
    platt_lr.fit(val_proba.reshape(-1, 1), y_val)
    platt_proba = platt_lr.predict_proba(val_proba.reshape(-1, 1))[:, 1]
    platt = CalibrationResult(
        method="platt",
        calibrator=platt_lr,
        val_metrics=compute_classification_metrics(y_val, platt_proba),
        calibrated_proba=platt_proba,
    )

    iso = IsotonicRegression(out_of_bounds="clip")
    iso.fit(val_proba, y_val)
    iso_proba = iso.predict(val_proba)
    isotonic = CalibrationResult(
        method="isotonic",
        calibrator=iso,
        val_metrics=compute_classification_metrics(y_val, iso_proba),
        calibrated_proba=iso_proba,
    )

    return platt, isotonic


def select_best_calibration(platt: CalibrationResult, isotonic: CalibrationResult) -> CalibrationResult:
    p_brier = platt.val_metrics.get("brier_score", 1.0) or 1.0
    i_brier = isotonic.val_metrics.get("brier_score", 1.0) or 1.0
    p_ece = platt.val_metrics.get("calibration_error", 1.0) or 1.0
    i_ece = isotonic.val_metrics.get("calibration_error", 1.0) or 1.0
    p_score = p_brier + p_ece
    i_score = i_brier + i_ece
    return platt if p_score <= i_score else isotonic


def apply_calibration(cal: CalibrationResult, proba: np.ndarray) -> np.ndarray:
    return cal.predict(proba)
