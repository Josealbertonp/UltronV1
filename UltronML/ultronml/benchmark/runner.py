"""ML infrastructure benchmarks."""

from __future__ import annotations

import json
import time
import tracemalloc
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable

import pandas as pd


@dataclass
class BenchmarkResult:
    name: str
    elapsed_ms: float
    peak_memory_mb: float | None = None
    details: dict = field(default_factory=dict)

    def to_dict(self) -> dict:
        return {
            "name": self.name,
            "elapsedMs": self.elapsed_ms,
            "peakMemoryMb": self.peak_memory_mb,
            "details": self.details,
        }


@dataclass
class BenchmarkSuite:
    results: list[BenchmarkResult] = field(default_factory=list)

    def to_dict(self) -> dict:
        return {"results": [r.to_dict() for r in self.results]}

    def save(self, path: Path | str) -> None:
        Path(path).write_text(json.dumps(self.to_dict(), indent=2), encoding="utf-8")


class MlBenchmarkRunner:
    def run(self, name: str, fn: Callable[[], None]) -> BenchmarkResult:
        tracemalloc.start()
        t0 = time.perf_counter()
        fn()
        elapsed_ms = (time.perf_counter() - t0) * 1000.0
        _, peak = tracemalloc.get_traced_memory()
        tracemalloc.stop()
        return BenchmarkResult(
            name=name,
            elapsed_ms=round(elapsed_ms, 3),
            peak_memory_mb=round(peak / (1024 * 1024), 3),
        )

    def benchmark_export(self, export_fn: Callable[[], pd.DataFrame]) -> BenchmarkSuite:
        suite = BenchmarkSuite()
        suite.results.append(self.run("dataset_build", lambda: export_fn()))
        return suite
