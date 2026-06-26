"""Feature metadata registry for training and inference."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from ultronml.constants import FEATURE_NAMES, LABEL_COLUMNS


@dataclass(frozen=True)
class FeatureMeta:
    name: str
    description: str
    dtype: str
    expected_min: float | None
    expected_max: float | None
    normalization: str
    use_in_training: bool
    use_in_inference: bool
    group: str = "feature"


# Extended metadata for score/gate columns added by DatasetBuilder.
_EXTENDED_META: dict[str, FeatureMeta] = {
    "score": FeatureMeta("score", "Primary signal score at entry", "int", 0, 100, "none", True, True, "signal"),
    "tier": FeatureMeta("tier", "Signal tier A/B/C", "category", None, None, "ordinal", True, True, "signal"),
    "archetype": FeatureMeta("archetype", "Signal archetype", "category", None, None, "onehot", True, True, "signal"),
    "regime": FeatureMeta("regime", "Market regime", "category", None, None, "onehot", True, True, "signal"),
    "direction": FeatureMeta("direction", "Trade direction", "category", None, None, "onehot", True, True, "signal"),
}


def _default_feature_meta(name: str) -> FeatureMeta:
    ranges = {
        "Rsi": (0, 100),
        "Adx": (0, 100),
        "Er": (0, 1),
        "Rvol": (0, 20),
    }
    lo, hi = ranges.get(name, (None, None))
    return FeatureMeta(
        name=name,
        description=f"Ultron feature vector: {name}",
        dtype="float",
        expected_min=lo,
        expected_max=hi,
        normalization="zscore",
        use_in_training=True,
        use_in_inference=True,
        group="feature",
    )


class FeatureMetadataRegistry:
    def __init__(self) -> None:
        self._entries: dict[str, FeatureMeta] = {}
        for name in FEATURE_NAMES:
            col = f"feat_{name}"
            self._entries[col] = _default_feature_meta(name)
        self._entries.update(_EXTENDED_META)
        for label in LABEL_COLUMNS:
            self._entries[label] = FeatureMeta(
                label, f"Label/outcome: {label}", "float", None, None, "none",
                use_in_training=False, use_in_inference=False, group="label",
            )

    def get(self, name: str) -> FeatureMeta | None:
        return self._entries.get(name)

    def training_features(self) -> list[str]:
        return [k for k, v in self._entries.items() if v.use_in_training and v.group != "label"]

    def inference_features(self) -> list[str]:
        return [k for k, v in self._entries.items() if v.use_in_inference and v.group != "label"]

    def to_json(self) -> str:
        payload = {
            k: {
                "name": v.name,
                "description": v.description,
                "dtype": v.dtype,
                "expectedMin": v.expected_min,
                "expectedMax": v.expected_max,
                "normalization": v.normalization,
                "useInTraining": v.use_in_training,
                "useInInference": v.use_in_inference,
                "group": v.group,
            }
            for k, v in self._entries.items()
        }
        return json.dumps(payload, indent=2)

    def save(self, path: Path | str) -> None:
        Path(path).write_text(self.to_json(), encoding="utf-8")

    @classmethod
    def load(cls, path: Path | str) -> FeatureMetadataRegistry:
        reg = cls()
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        for key, meta in data.items():
            reg._entries[key] = FeatureMeta(
                name=meta["name"],
                description=meta.get("description", ""),
                dtype=meta.get("dtype", "float"),
                expected_min=meta.get("expectedMin"),
                expected_max=meta.get("expectedMax"),
                normalization=meta.get("normalization", "none"),
                use_in_training=meta.get("useInTraining", True),
                use_in_inference=meta.get("useInInference", True),
                group=meta.get("group", "feature"),
            )
        return reg
