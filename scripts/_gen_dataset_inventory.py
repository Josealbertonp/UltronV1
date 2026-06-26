import json
from datetime import datetime, timezone
from pathlib import Path

root = Path.home() / "Documents/NinjaTrader 8/UltronV1/dataset/sessions"
sessions = []
for sd in sorted(root.iterdir()):
    if not sd.is_dir():
        continue
    opp = sd / "opportunities.jsonl"
    out = sd / "outcomes.jsonl"
    oc = 0
    if out.exists():
        with out.open(encoding="utf-8-sig") as f:
            oc = sum(1 for line in f if line.strip())
    sessions.append({
        "sessionId": sd.name,
        "opportunities_mb": round(opp.stat().st_size / 1e6, 2) if opp.exists() else 0,
        "outcomes_mb": round(out.stat().st_size / 1e6, 2) if out.exists() else 0,
        "outcome_count": oc,
        "has_opportunities": opp.exists(),
        "has_outcomes": out.exists() and oc > 0,
    })

inv = {
    "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
    "sourcePath": str(root.parent),
    "note": "Raw JSONL NOT in git (~33GB). Copy dataset folder manually before PC format.",
    "sessionCount": len(sessions),
    "sessionsWithOutcomes": sum(1 for s in sessions if s["has_outcomes"]),
    "totalOutcomes": sum(s["outcome_count"] for s in sessions),
    "sessions": sessions,
}
out = Path(__file__).resolve().parents[1] / "UltronML" / "datasets" / "dataset_sessions_inventory.json"
out.write_text(json.dumps(inv, indent=2), encoding="utf-8")
print(f"Wrote {out.name}: {len(sessions)} sessions, {inv['totalOutcomes']} outcomes")
