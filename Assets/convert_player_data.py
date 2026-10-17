"""Convert LILA Parquet telemetry into Unity JsonUtility-compatible JSON.

Install: python -m pip install pyarrow
Run: python convert_player_data.py --input PATH_TO_PLAYER_DATA --output OUTPUT_FOLDER
Timestamp assumption: raw timestamp integers are Unix seconds despite timestamp[ms]
metadata. Original integers are preserved; elapsed times start at first observation.
"""
import argparse
import json
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

import pyarrow.compute as pc
import pyarrow.parquet as pq

MAPS = {
    "AmbroseValley": (900, -370, -473),
    "GrandRift": (581, -290, -290),
    "Lockdown": (1000, -500, -500),
}


def write_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, allow_nan=False,
                               separators=(",", ":")), encoding="utf-8")


def convert(source, output):
    files = sorted(source.glob("February_*/*.nakama-0"))
    if not files:
        raise ValueError(f"No gameplay files found in {source}")
    matches = defaultdict(list)
    row_count = 0
    for path in files:
        table = pq.read_table(path)
        raw_times = pc.cast(table["ts"], "int64").to_pylist()
        for row, raw_time in zip(table.to_pylist(), raw_times):
            # Allow a day either side of the folder date for timezone boundaries.
            date = datetime.fromtimestamp(raw_time, timezone.utc)
            folder_day = int(path.parent.name.split('_')[1])
            if date.year != 2026 or date.month != 2 or abs(date.day - folder_day) > 1:
                raise ValueError(f"Unexpected timestamp {raw_time} in {path.name}")
            row["rawTimestamp"] = raw_time
            row["sourceDate"] = f"2026-02-{int(path.parent.name.split('_')[1]):02d}"
            row["event"] = row["event"].decode("utf-8") if isinstance(row["event"], bytes) else row["event"]
            matches[row["match_id"]].append(row)
            row_count += 1

    output.mkdir(parents=True, exist_ok=True)
    match_dir = output / "matches"
    match_dir.mkdir(exist_ok=True)
    index = []
    for match_id, rows in sorted(matches.items()):
        map_ids = {r["map_id"] for r in rows}
        if len(map_ids) != 1:
            raise ValueError(f"Multiple maps in {match_id}")
        map_id = next(iter(map_ids))
        scale, origin_x, origin_z = MAPS[map_id]
        start = min(r["rawTimestamp"] for r in rows)
        end = max(r["rawTimestamp"] for r in rows)
        players = defaultdict(list)
        for r in sorted(rows, key=lambda r: r["rawTimestamp"]):
            players[r["user_id"]].append({
                "timeSeconds": r["rawTimestamp"] - start,
                "rawTimestamp": r["rawTimestamp"],
                "eventType": r["event"],
                "x": r["x"], "y": r["y"], "z": r["z"],
                "u": (r["x"] - origin_x) / scale,
                "v": (r["z"] - origin_z) / scale,
            })
        filename = match_id + ".json"
        if Path(filename).name != filename or "/" in filename or "\\" in filename:
            raise ValueError("Unsafe match ID")
        summary = {
            "matchId": match_id, "mapId": map_id,
            "dates": sorted({r["sourceDate"] for r in rows}),
            "durationSeconds": end - start,
            "playerCount": len(players),
            "eventCount": len(rows),
            "file": "matches/" + filename,
        }
        write_json(match_dir / filename, {
            **summary, "startRawTimestamp": start,
            "players": [{"userId": uid, "isBot": uid.isdigit(), "events": events}
                        for uid, events in sorted(players.items())],
        })
        index.append(summary)
    write_json(output / "index.json", {
        "schemaVersion": 1,
        "timestampAssumption": "Raw timestamp integers interpreted as Unix seconds; playback starts at first recorded event, not necessarily match start.",
        "maps": [{"mapId": name, "scale": values[0], "originX": values[1], "originZ": values[2]}
                 for name, values in MAPS.items()],
        "matches": index,
    })
    print(f"Converted {len(files)} files / {row_count} events / {len(index)} matches to {output}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    convert(args.input, args.output)
