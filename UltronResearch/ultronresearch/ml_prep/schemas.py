"""ML preparation schemas — no training."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone


@dataclass
class FeatureImportancePlaceholder:
    status: str = "placeholder"
    model: str | None = None
    features: list[dict] = field(default_factory=list)

    def to_dict(self) -> dict:
        return {"status": self.status, "model": self.model, "features": self.features}


@dataclass
class ShapReportPlaceholder:
    status: str = "placeholder"
    base_value: float | None = None
    shap_values: list[dict] = field(default_factory=list)

    def to_dict(self) -> dict:
        return {
            "status": self.status,
            "baseValue": self.base_value,
            "shapValues": self.shap_values,
        }


@dataclass
class ModelComparisonPlaceholder:
    status: str = "placeholder"
    models: list[dict] = field(default_factory=list)

    def to_dict(self) -> dict:
        return {"status": self.status, "models": self.models}


@dataclass
class ThresholdReport:
    generated_at_utc: str
    score: dict
    tier: dict
    confidence: dict

    def to_dict(self) -> dict:
        return {
            "generatedAtUtc": self.generated_at_utc,
            "score": self.score,
            "tier": self.tier,
            "confidence": self.confidence,
        }


@dataclass
class DatasetQualityReport:
    row_count: int
    validation_passed: bool
    issues: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    def to_dict(self) -> dict:
        return {
            "rowCount": self.row_count,
            "validationPassed": self.validation_passed,
            "issues": self.issues,
            "warnings": self.warnings,
        }


def build_ml_prep_bundle(thresholds: dict, df_row_count: int, metadata: dict) -> dict:
    validation = metadata.get("validationPassed", metadata.get("validation", {}).get("passed"))
    quality = DatasetQualityReport(
        row_count=df_row_count,
        validation_passed=bool(validation) if validation is not None else True,
        issues=metadata.get("validation", {}).get("errors", []) if isinstance(metadata.get("validation"), dict) else [],
    )
    return {
        "feature_importance": FeatureImportancePlaceholder().to_dict(),
        "shap": ShapReportPlaceholder().to_dict(),
        "model_comparison": ModelComparisonPlaceholder().to_dict(),
        "threshold_report": ThresholdReport(
            generated_at_utc=datetime.now(timezone.utc).isoformat(),
            score=thresholds.get("score", {}),
            tier=thresholds.get("tier", {}),
            confidence=thresholds.get("confidence", {}),
        ).to_dict(),
        "dataset_quality": quality.to_dict(),
    }
