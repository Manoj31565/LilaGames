# Three insights from the supplied LILA BLACK telemetry

## Evidence and scope

These findings use a direct scan of all 1,243 original Parquet files in `Assets/StreamingAssets/player_data/February_*`, covering 89,104 event rows, 796 distinct match IDs and 339 distinct user IDs. They do not use converted match JSON. The supplied period is February 10-14, 2026; February 14 is partial. Counts describe this telemetry sample, not the whole live population. Each row is an event, not necessarily a unique interaction or unique player.

The replay tool provides map/match filters and time-based inspection. The quantitative summaries below were calculated from the same raw inputs; aggregate cross-match statistics are not currently displayed in its UI. Named match examples can be searched in the tool for visual follow-up. Heatmaps use a 30-second rolling window, whereas these counts cover all recorded times.

## 1. Ambrose Valley has a concentrated loot-and-combat area

**Observation.** A small area near the map center attracts a disproportionate share of recorded loot and kill activity.

**Evidence.** Divide each minimap into a 10 by 10 UV grid. Ambrose Valley's cell `0.5 <= u < 0.6`, `0.4 <= v < 0.5` occupies 1% of the rectangular minimap area and contains:

| Measure | Cell count | Whole-map count | Share |
|---|---:|---:|---:|
| Loot events | 1,317 | 9,955 | 13.23% |
| Position + BotPosition samples | 3,548 | 48,754 | 7.28% |
| Kill + BotKill events | 224 | 1,799 | 12.45% |

The cell's loot events occur across 141 distinct sampled matches, and its kill events across 96. This is not solely one unusually active replay. Using the supplied mapping, its world-coordinate bounds are `80 <= x < 170` and `-113 <= z < -23`.

**Inspect in the tool.** Filter AmbroseValley and search match `9dee72b8-a9c8-42b8-8cc2-20961c16758a` (33 Loot rows in this cell), or `c8b2fabd-ca84-4f80-bbf8-0e7938af41b7` (23). Scrub the replay and inspect markers near that UV region.

**Action and metrics.** Review loot placement, entrances, sightlines, and cover in this region. Test distributing a portion of rewards to an alternate route rather than immediately reducing rewards. Track unique human visitors per region, loot concentration, combat events per visitor-minute, route diversity, and extraction success once outcome telemetry is available.

**Why a level designer should care.** A repeatedly attractive area can create useful encounter pressure, but can also constrain viable routes. Co-location does not prove that loot causes fighting; geometry, objectives, bot placement, and repeated position sampling are possible contributors. The 1% denominator is map image area, not measured traversable area.

## 2. Bot-related labels dominate recorded combat

**Observation.** Combat review in this sample should prioritize bot encounters, while checking event semantics before interpreting who killed whom.

**Evidence.** There are 2,415 BotKill and 700 BotKilled rows, compared with 3 Kill and 3 Killed rows. Bot-labelled events therefore account for **3,115 / 3,121 = 99.81%** of these four combat labels. Storm deaths are excluded from this calculation. These are event records, not 3,121 distinct fights; paired kill/death records must not be counted as independent engagements.

The pattern is present on all three maps:

| Map | BotKill | BotKilled | Kill | Killed |
|---|---:|---:|---:|---:|
| AmbroseValley | 1,797 | 486 | 2 | 2 |
| GrandRift | 192 | 46 | 1 | 1 |
| Lockdown | 426 | 168 | 0 | 0 |

**Inspect in the tool.** Open matches across all three map filters and compare the separate human/bot prefabs with kill and death markers. Use the event categories rather than assuming every skull represents human-versus-human combat.

**Action and metrics.** Audit bot patrols, spawn exposure, and cover around combat hotspots. First confirm event ownership with the telemetry producer: 183 BotKill rows and 297 BotKilled rows have numeric user IDs, so the label alone does not uniquely identify the actor. Then evaluate bot-encounter rate, human deaths per bot encounter, encounter duration, and extraction completion. Add encounter or victim IDs if unique engagement metrics are needed.

**Why a level designer should care.** Bot placement may be a major source of the recorded combat experience, but the sample does not establish that PvP is rare across the whole game. Sparse PvP labels can also reflect collection coverage or naming conventions. Avoid rebalancing PvP routes based solely on this label ratio.

## 3. Storm-death involvement is higher in sampled Lockdown matches

**Observation.** Lockdown deserves a targeted review of escape-route readability and storm pressure relative to Ambrose Valley.

**Evidence.** Count distinct sampled matches with at least one KilledByStorm event, divided by all distinct sampled matches on that map:

| Map | Matches with storm deaths | Sampled matches | Share |
|---|---:|---:|---:|
| Lockdown | 17 | 171 | 9.94% |
| GrandRift | 5 | 59 | 8.47% |
| AmbroseValley | 17 | 566 | 3.00% |

There are 39 storm-death rows across these 39 matches. Lockdown's observed share is about 3.31 times Ambrose Valley's, but this is a descriptive sample comparison, not an estimated causal map effect or a per-player death probability. GrandRift's denominator is especially small.

**Inspect in the tool.** Filter Lockdown and `2026-02-10`, then search `00b34c64-8746-44aa-b527-72ac0d971251`. Its storm-death row is near world `(48.73, 252.45)` on the x/z plane. Another example is `3770e5eb-0fdb-408f-a8df-5471c6c8782f`, near `(84.85, 25.11)`. Scrub toward the storm marker and inspect the preceding player route; the actual storm boundary is not reconstructed by the tool.

**Action and metrics.** Review exit visibility, route length, potential dead ends, and the timing/readability of storm warnings in those replays. Test clearer directional cues or alternate escape routes before changing storm speed. Track storm deaths per exposed player, time from warning to extraction, escape-route completion, and extraction rate, segmented by map and player experience. Several of these require additional telemetry beyond the supplied schema.

**Why a level designer should care.** This identifies a specific area of investigation for navigation pressure. Missing players, different map populations, exposure durations, skill, and partial collection can affect the observed share; the evidence does not establish that Lockdown is unfair.

## Reproduction rules

Read all row groups in every `February_*/*.nakama-0` file. Decode the binary `event` column as UTF-8. Preserve match_id including its `.nakama-0` suffix. Group matches by distinct match_id, not filename count. For the spatial insight, calculate UV with the supplied map origin/scale, then apply the stated half-open cell bounds; count rows by event label and distinct match IDs separately. For storm involvement, deduplicate match IDs within each map before dividing. No out-of-bounds UV events were found among the movement, loot, kill, or storm rows used for these spatial checks.
