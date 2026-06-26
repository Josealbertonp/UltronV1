"""Model registry — records trained models; never auto-promotes."""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path


class ModelRegistry:
    def __init__(self, registry_root: Path):
        self.registry_root = registry_root
        self.registry_root.mkdir(parents=True, exist_ok=True)
        self.index_path = registry_root / "registry.json"

    def load_index(self) -> dict:
        if not self.index_path.exists():
            return {"version": 1, "promotionPolicy": "manual_only", "models": []}
        with self.index_path.open("r", encoding="utf-8") as f:
            return json.load(f)

    def register_model(self, model_dir: Path, metrics: dict) -> dict:
        index = self.load_index()
        model_id = model_dir.name
        try:
            rel_path = str(model_dir.relative_to(self.registry_root.parent))
        except ValueError:
            rel_path = str(model_dir)

        entry = {
            "modelId": model_id,
            "path": rel_path,
            "registeredUtc": datetime.now(timezone.utc).isoformat(),
            "status": "trained_not_promoted",
            "promoted": False,
            "active": False,
            "metricsSummary": {
                "auc": metrics.get("auc"),
                "f1": metrics.get("f1"),
                "profitFactor": metrics.get("profitFactor"),
                "winRate": metrics.get("winRate"),
                "tradeCount": metrics.get("tradeCount"),
            },
        }
        models = [m for m in index.get("models", []) if m.get("modelId") != model_id]
        models.append(entry)
        index["models"] = sorted(models, key=lambda m: m.get("registeredUtc", ""), reverse=True)
        index["lastRegistered"] = model_id
        index["activeModelId"] = index.get("activeModelId")  # unchanged — manual promote only

        with self.index_path.open("w", encoding="utf-8") as f:
            json.dump(index, f, indent=2)
        return entry

    def promote_model(self, model_id: str) -> dict:
        """Explicit manual promotion — never called by training pipeline."""
        index = self.load_index()
        found = False
        for m in index.get("models", []):
            if m.get("modelId") == model_id:
                m["promoted"] = True
                m["active"] = True
                m["promotedUtc"] = datetime.now(timezone.utc).isoformat()
                found = True
            else:
                m["active"] = False
        if not found:
            raise KeyError(f"Model not found: {model_id}")
        index["activeModelId"] = model_id
        with self.index_path.open("w", encoding="utf-8") as f:
            json.dump(index, f, indent=2)
        return index
