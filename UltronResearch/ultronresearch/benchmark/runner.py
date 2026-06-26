"""Research Lab benchmarks."""

from __future__ import annotations

import json
import time
import tracemalloc
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable


@dataclass
class BenchResult:
    name: str
    elapsed_ms: float
    peak_memory_mb: float | None = None
    details: dict = field(default_factory=dict)


class ResearchBenchmarkRunner:
    def __init__(self) -> None:
        self._results: list[BenchResult] = []

    def run(self, name: str, fn: Callable) -> BenchResult:
        tracemalloc.start()
        t0 = time.perf_counter()
        value = fn()
        elapsed = (time.perf_counter() - t0) * 1000
        _, peak = tracemalloc.get_traced_memory()
        tracemalloc.stop()
        result = BenchResult(
            name=name,
            elapsed_ms=round(elapsed, 3),
            peak_memory_mb=round(peak / (1024 * 1024), 3),
            details={"dataset": value} if name == "data_load" else {},
        )
        self._results.append(result)
        return result

    def to_dict(self) -> dict:
        return {"results": [
            {"name": r.name, "elapsedMs": r.elapsed_ms, "peakMemoryMb": r.peak_memory_mb}
            for r in self._results
        ]}

    def save(self, path: Path | str) -> None:
        Path(path).write_text(json.dumps(self.to_dict(), indent=2), encoding="utf-8")
