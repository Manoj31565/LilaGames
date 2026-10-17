# LILA BLACK Player Journey Visualizer

A Unity WebGL tool for exploring recorded player and bot activity on Ambrose Valley, Grand Rift, and Lockdown. It reads the supplied original Parquet files, groups their events into matches, and displays time-based movement, event markers, and heatmap overlays.

## Links and submission status

- **Hosted demo:** Pending deployment. Add the public URL here before submission.
- **GitHub repository:** Pending. Add the repository URL here before submission.
- Documentation prepared on 16 October 2026 against the current local project.

The assignment requires a single GitHub repository link containing the source, these documents, and a working deployed URL. These documents alone do not satisfy the hosting requirement. Place this README, ARCHITECTURE.md, and INSIGHTS.md in the repository root.

## Tech stack

- Unity **6000.3.14f1**, C#, Universal Render Pipeline 17.3.0.
- Unity UI and TextMeshPro for menus, filtering, progress, and playback controls.
- **Parquet.Net 5.1.0**, with dependencies restored through NuGetForUnity; package versions are recorded in `Assets/packages.config`.
- A JavaScript WebGL file picker and UnityWebRequest for browser-accessible file loading.
- Generated Texture2D/SpriteRenderer overlays and a custom unlit heatmap shader.
- Static WebGL hosting; no application backend, database, login, API key, or environment variables are required.

## Run locally

1. Install the matching Unity Editor through Unity Hub, including **Web Build Support**.
2. Open the repository folder containing `Assets`, `Packages`, and `ProjectSettings`.
3. Allow Unity Package Manager and NuGetForUnity to restore dependencies. Internet access may be needed for the initial restore. Keep the supplied Parquet dataset under `Assets/StreamingAssets/player_data`.
4. Open `Assets/Game/Scenes/Menu.unity`, then enter Play mode.
5. Choose the existing-data option. The loader displays file progress and creates the match list.

The browser file picker is a WebGL feature; use the existing-data path in the Editor. Begin from Menu rather than opening Game directly: Game requires the selected MatchData held in `Constants.currentMatchData`.

## Use the tool

1. Load the supplied dataset and wait for the match list.
2. Choose a map, enter a date such as `2026-02-11`, and/or enter part of a match ID, then press Search. Blank text filters leave that criterion unrestricted; filters combine with AND. Date and ID matching currently use substring search.
3. Select a match to open its minimap. Human and bot markers use separate prefabs.
4. Use Play, Pause, Restart, and the timeline slider to inspect progression. Movement interpolates between recorded Position/BotPosition samples; it is a reconstruction, not a recording of every frame.
5. Inspect the event markers: gold coin for loot, skull for kills, tombstone for deaths, and storm icon for storm deaths.
6. Inspect the independent kill (red), death (purple), and traffic (cyan) heatmaps. They show activity reached by playback, fade over a default 30-second window, and disable when no events are active. Death density includes storm deaths. Loot is not included in heatmaps.

The playback controller exposes speed adjustment in code; a speed selector is not currently wired in the scene. Heatmap radius, opacity, saturation, and activity window are Inspector settings, not browser controls. Setting the window to zero accumulates all elapsed activity.

### Upload an original file

In a WebGL build, select the upload option and choose a `.nakama-0` Parquet file. The browser reads its bytes locally, and the existing Parquet parser processes them. A file containing one match opens automatically. The upload path currently selects one file at a time; it does not combine uploads into a full multi-file match. A file containing multiple matches is parsed and retained, but its selection UI is not implemented.

Each supplied file contains one recorded player's events. The existing-data loader combines files sharing `match_id`; a single-file upload only contains the players and events present in that file.

## Build and host

1. Open Unity's Build Profiles and select the Web platform.
2. Keep Menu first and Game second in the included scenes.
3. Build and Run to check the browser version locally.
4. The pre-build hook generates `Assets/StreamingAssets/player_data/parquet-files.txt`. This contains only relative source-file paths. Match IDs and event data are still parsed from the original Parquet, not converted match JSON.
5. Upload the **complete generated build directory**, preserving `index.html`, `Build`, `TemplateData`, and `StreamingAssets` and their relative paths, to a static web host.
6. Configure the host to serve the generated WebAssembly and compressed files with matching content types/encoding, or choose compatible Unity compression/decompression settings and rebuild. Do not launch the output through `file://`.
7. Test the public URL in a fresh browser session: bundled-data loading, file upload, filters, playback, markers, and heatmaps. Then replace the pending links above.

The runtime expects `StreamingAssets/player_data/parquet-files.txt` and every listed original file to be available at the deployed relative URLs. HTTP failures are displayed in the loading status and logged to the browser console.

## Source layout

| Location | Purpose |
|---|---|
| `Assets/Game/Code` | Parsing, match model, menu, playback, markers, heatmaps |
| `Assets/Game/Scenes` | Menu and Game scenes |
| `Assets/Game/Resources` | Player/event prefabs and heatmap shader |
| `Assets/Game/Maps` | Minimap sprites |
| `Assets/Game/Assets` | Event-icon PNGs |
| `Assets/Game/Editor/ParquetManifestBuilder.cs` | Build-time source-file discovery |
| `Assets/Plugins/WebGL/FilePicker.jslib` | Browser file-read bridge |
| `Assets/StreamingAssets/player_data` | Original Parquet dataset and generated file list |

The historical `Assets/convert_player_data.py` and converted `StreamingAssets/PlayerData` files are not used by the current runtime. Note that `PlayerData` and `player_data` are different folder names on case-sensitive hosts.

## Data and validation

A fresh scan of the original files found **1,243 files, 89,104 events, 796 distinct matches, 339 distinct user IDs, and eight event types** across February 10-14, 2026. February 14 is a partial day. See INSIGHTS.md for the evidence and limitations.

Local C# compilation checks and focused checks for parser output, event-index boundaries, and heatmap timeline selection have passed during development. These checks do not establish browser compatibility or visual correctness of a newly built deployment. Public deployment verification is pending.

## Known limitations

- Event markers currently expire with Unity's `Destroy(marker, 5f)`, rather than replay time. Pausing or rewinding does not fully reconstruct marker visibility; already-spawned markers can persist briefly or duplicate after rewinding.
- A search with no results reports a message but leaves the previous match buttons visible. A failed initial load also leaves the filters disabled, and the current Back action is gated on successful loading.
- Match buttons are created together; large lists can cause a UI pause. Returning through a newly loaded Menu scene does not retain the previous menu's in-memory cache.
- Heatmap rendering currently refreshes whenever playback time changes; the serialized refresh interval is not enforced by the present FixedUpdate implementation.
- Browser startup requests the original files individually. High request counts, Parquet parsing, and WebGL memory use need testing on the final host/device.
- Sampled telemetry is incomplete evidence of the full game. No extraction/success outcome is available, and movement density is not a count of unique visitors.

AI assistance was used for implementation, debugging, documentation, and generated event icons.
