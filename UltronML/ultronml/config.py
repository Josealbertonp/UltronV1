"""RC2 ML filter configuration — independent of UltronV1.Core."""

from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class MlFilterConfig:
    """ML confidence filter (future RC2 integration). Default off = RC1 behavior."""

    enable_ml_filter: bool = False
    min_confidence: float = 0.50
    model_directory: str = ""
    provider: str = "Dummy"

    def is_active(self) -> bool:
        return self.enable_ml_filter
