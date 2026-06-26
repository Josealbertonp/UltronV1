"""Export LightGBM classifier to ONNX."""

from __future__ import annotations

from pathlib import Path

import lightgbm as lgb
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType

from ultron_ml.constants import FEATURE_COUNT


def export_lightgbm_onnx(model: lgb.LGBMClassifier, output_path: Path) -> Path:
    initial_type = [("features", FloatTensorType([None, FEATURE_COUNT]))]
    onnx_model = convert_sklearn(
        model,
        initial_types=initial_type,
        target_opset=12,
        options={type(model): {"zipmap": False}},
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("wb") as f:
        f.write(onnx_model.SerializeToString())
    return output_path
