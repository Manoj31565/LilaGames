# Architecture

## Stack and rationale

Unity 6000.3.14f1/C#, URP, Unity UI/TextMeshPro, and Parquet.Net 5.1.0 provide a browser-delivered WebGL replay. Unity reuses game-engine transforms, sprites, and playback workflows; the tradeoff is a larger runtime than a lightweight JavaScript frontend. Cloudflare Pages hosts the static build at https://lilagames.pages.dev/. Source: https://github.com/Manoj31565/LilaGames. No application server, database, secrets, or environment variables are required.

## Data flow

`Original Parquet -> ParquetLoader -> PlayerData.matches -> menu filters -> selected MatchData -> playback -> players, markers, heatmaps`

A pre-build hook creates a path-only manifest of original files. WebGL downloads it and loads up to six files concurrently through UnityWebRequest and MemoryStream; parsing stays on the main thread. Editor/desktop uses up to four workers. The loader reads all row groups and eight columns, decodes binary event labels as UTF-8, groups by match_id/user_id, merges matching identities across files, and sorts events by timestamp. It recalculates counts, relative times, and observed duration. No converted match JSON is required. A browser upload follows FileReader -> Base64 -> MemoryStream and contains only that file's records.

MenuManager caches the dataset across scenes and filters by map, UTC date, and match ID. Constants.currentMatchData transfers the selection. MatchPlaybackController owns time; Player interpolates Position/BotPosition samples. Event markers are cached per event and hidden before their timestamps on rewind. HeatmapTimeline selects elapsed events: kills/deaths persist, while traffic fades over 30 seconds. Three Gaussian-density textures overlay the map; empty layers disable.

## Coordinate mapping

| Map | Scale | Origin X | Origin Z |
|---|---:|---:|---:|
| AmbroseValley | 900 | -370 | -473 |
| GrandRift | 581 | -290 | -290 |
| Lockdown | 1000 | -500 | -500 |

Compute `u=(x-originX)/scale`, `v=(z-originZ)/scale`; world y is elevation. Map UV into sprite-local bounds using `(min.x+u*size.x, min.y+v*size.y, 0)`, then apply TransformPoint to account for map position and scale. Unity sprites use bottom-left coordinates, so the dataset README's top-left image conversion `(1-v)*height` is unnecessary here. Player mapping handles flips; current maps are unflipped. Heatmaps exclude out-of-range UV rather than clamping false hotspots onto map edges.

## Assumptions and tradeoffs

| Decision | Reason / consequence |
|---|---|
| Raw timestamps interpreted as Unix seconds despite timestamp[ms] metadata | Produces February 2026 dates and plausible durations; standard-millisecond mode remains available. |
| Numeric user IDs identify bots | Follows the dataset convention; combat labels alone do not identify the actor. |
| Replay begins at earliest observed event | Missing earlier telemetry cannot be reconstructed; duration is observed span, not guaranteed match length. |
| Client parsing and session cache | Simple static deployment; initial requests, CPU work, and memory remain costs. |
| Cumulative combat; rolling traffic | Preserves death/kill locations while showing recent movement; traffic counts samples, not unique visitors. |

Current limits: multi-match uploads lack a selector, empty searches retain old buttons, failed loads lack a usable Back action, and the heatmap refresh interval is not enforced. These are documented in the README rather than presented as completed behavior.
