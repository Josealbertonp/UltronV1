"""JSONL helpers."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Iterator


def repair_json_line(raw: str) -> str:
    """Fix common serialization glitches in legacy Ultron opportunity JSONL."""
    s = raw.strip()
    while ",," in s:
        s = s.replace(",,", ",")
    s = re.sub(r"(\})(\s*)(\")", r"\1,\2\3", s)
    s = re.sub(r"(\])(\s*)(\")", r"\1,\2\3", s)
    return s


def parse_jsonl_line(raw: str) -> dict | None:
    line = raw.strip()
    if not line:
        return None
    try:
        return json.loads(line)
    except json.JSONDecodeError:
        try:
            return json.loads(repair_json_line(line))
        except json.JSONDecodeError:
            return None


def iter_jsonl(path: Path, *, tolerant: bool = False) -> Iterator[dict]:
    if not path.exists():
        return
    with path.open(encoding="utf-8-sig") as f:
        for line in f:
            if tolerant:
                rec = parse_jsonl_line(line)
                if rec is not None:
                    yield rec
            else:
                line = line.strip()
                if not line:
                    continue
                try:
                    yield json.loads(line)
                except json.JSONDecodeError:
                    continue
