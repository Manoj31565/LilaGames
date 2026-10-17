# Architecture

## Stack and intent

Unity 6000.3.14f1/C# with URP, SpriteRenderers, TextMeshPro, and Unity UI provides a single codebase for Editor iteration and a browser-delivered WebGL replay. Parquet.Net 5.1.0 reads the original telemetry. This choice reuses the project's game-engine workflow and coordinate transforms; its cost is a heavier download and browser runtime than a lightweight web UI. Deployment is static; no backend, credentials, or environment variables are required. Hosting is pending.

## Data flow

`Original Parquet -> ParquetLoader -> PlayerData.matches -> Menu filters -> selected MatchData -> Game scene -> players / markers / heatmaps`

In Editor/desktop, the loader enumerates source files and uses up to four workers. In WebGL, a pre-build hook writes a path-only manifest; UnityWebRequest fetches that list and the original files, which are parsed through MemoryStream. Uploads instead use FileReader -> Base64 -> C# MemoryStream. There is no runtime dependency on converted match JSON.

ParquetLoader reads all row groups and eight source columns, decodes binary event labels as UTF-8, groups by match_id and user_id, and sorts events by time. The all-files path merges matching identities across files and recomputes counts, start time, duration, and relative event times. MenuManager filters map/date/match and passes the selection through Constants.currentMatchData. MatchPlaybackController owns replay time; Player interpolates movement samples, while HeatmapTimeline selects events reached within the rolling window. Three independent textures accumulate Gaussian contributions for kills, deaths, and traffic; old contributions fade and empty layers disable.

## Coordinate mapping

| Map | Scale | Origin X | Origin Z |
|---|---:|---:|---:|
| AmbroseValley | 900 | -370 | -473 |
| GrandRift | 581 | -290 | -290 |
| Lockdown | 1000 | -500 | -500 |

Calculate `u = (x - originX) / scale`, `v = (z - originZ) / scale`. World y is elevation and is not used for the top-down position. Convert UV into the map sprite's local bounds: `(min.x + u*size.x, min.y + v*size.y, 0)`, then use TransformPoint. This handles the map's transform and scale. Unity sprite coordinates use a bottom-left origin, so the README's top-left image formula `(1-v)*height` is not applied here. Player mapping handles sprite flips; heatmap sprites align to the map bounds and copy flip settings. Out-of-range UV events are excluded from heatmaps rather than clamped into false edge hotspots.

## Assumptions and tradeoffs

| Decision | Reason and consequence |
|---|---|
| Interpret raw timestamp integers as Unix seconds despite timestamp[ms] metadata | Values map to February 2026 and plausible replay durations; the supplied README conflicts. The loader documents this assumption and offers standard-millisecond mode. |
| Numeric user IDs denote bots | Dataset convention; event labels alone do not identify the actor. |
| Start at earliest observed event | Missing earlier telemetry cannot be reconstructed; duration is observed span, not guaranteed full match length. Dates use UTC. |
| Client-side parsing and in-memory grouping | Simple static hosting and direct raw-data use; many requests and memory/CPU cost in WebGL. |
| Rolling 30-second density, not permanent clusters | Highlights recent activity; traffic measures recorded samples, not unique-player flow. |

Known gaps: markers still use wall-clock destruction and are not fully rewind-safe; browser deployment remains to be verified. The architecture separates loading, playback, and rendering so these can be corrected independently.
