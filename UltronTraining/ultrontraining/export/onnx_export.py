"""ONNX export for champion model."""

from __future__ import annotations

from pathlib import Path

import numpy as np
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType


def export_to_onnx(
    model: object,
    model_name: str,
    feature_count: int,
    output_path: Path,
) -> Path:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    name = model_name.lower()

    if "catboost" in name:
        return _export_catboost(model, output_path)

    if "xgboost" in name:
        return _export_xgboost(model, feature_count, output_path)

    initial_type = [("features", FloatTensorType([None, feature_count]))]
    options = {}
    try:
        options = {type(model): {"zipmap": False}}
    except TypeError:
        pass

    onnx_model = convert_sklearn(
        model,
        initial_types=initial_type,
        target_opset=12,
        options=options,
    )
    with output_path.open("wb") as f:
        f.write(onnx_model.SerializeToString())
    return output_path


def _export_catboost(model, output_path: Path) -> Path:
    model.save_model(str(output_path), format="onnx")
    return output_path


def _export_xgboost(model, feature_count: int, output_path: Path) -> Path:
    try:
        from onnxmltools.convert import convert_xgboost
        from onnxmltools.convert.common.data_types import FloatTensorType as OmlFloat

        onnx_model = convert_xgboost(
            model.get_booster(),
            initial_types=[("features", OmlFloat([None, feature_count]))],
            target_opset=12,
        )
        with output_path.open("wb") as f:
            f.write(onnx_model.SerializeToString())
        return output_path
    except Exception:
        initial_type = [("features", FloatTensorType([None, feature_count]))]
        onnx_model = convert_sklearn(model, initial_types=initial_type, target_opset=12)
        with output_path.open("wb") as f:
            f.write(onnx_model.SerializeToString())
        return output_path


def verify_onnx(path: Path, feature_count: int) -> dict:
    try:
        import onnx
        import onnxruntime as ort
    except ImportError:
        return {"verified": False, "note": "onnxruntime not installed for verification"}

    model = onnx.load(str(path))
    sess = ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])
    dummy = np.zeros((1, feature_count), dtype=np.float32)
    input_name = sess.get_inputs()[0].name
    out = sess.run(None, {input_name: dummy})
    return {
        "verified": True,
        "input_name": input_name,
        "output_shape": [list(o.shape) for o in out],
        "opset": model.opset_import[0].version if model.opset_import else None,
    }
