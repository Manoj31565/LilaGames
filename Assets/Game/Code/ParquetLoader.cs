using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Parquet;

public static class ParquetLoader
{
    // The supplied dataset stores Unix-second integers in a timestamp[ms] column.
    // Pass false for normal timestamp[ms] files whose integers are Unix milliseconds.
    public static async Task<List<MatchData>> LoadAsync(string filePath, bool rawTimestampsAreUnixSeconds = true)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        using var stream = new MemoryStream(await DownloadAsync(filePath), writable: false);
#else
        using var stream = File.OpenRead(filePath);
#endif

        return await LoadAsync(stream, filePath, rawTimestampsAreUnixSeconds);
    }

    // Also accepts a MemoryStream containing bytes downloaded in WebGL.
    public static async Task<List<MatchData>> LoadAsync(Stream stream, string sourceFile, bool rawTimestampsAreUnixSeconds = true)
    {
        using var reader = await ParquetReader.CreateAsync(stream, leaveStreamOpen: true);

        string[] names = { "user_id", "match_id", "map_id", "x", "y", "z", "ts", "event" };

        var fields = reader.Schema.GetDataFields();

        var required = names.Select(name => fields.FirstOrDefault(field => field.Name == name)
            ?? throw new InvalidDataException($"Missing Parquet column: {name}")).ToArray();

        var matches = new Dictionary<string, MatchData>(StringComparer.Ordinal);
        var players = new Dictionary<string, Dictionary<string, PlayerMatchData>>(StringComparer.Ordinal);
        var dates = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int group = 0; group < reader.RowGroupCount; group++)
        {
            using var rowGroup = reader.OpenRowGroupReader(group);

            var columns = new Array[names.Length];

            for (int c = 0; c < required.Length; c++)
                columns[c] = (await rowGroup.ReadColumnAsync(required[c])).Data;

            if (columns.Any(column => column.Length != columns[0].Length))
                throw new InvalidDataException("Parquet column row counts differ.");

            for (int row = 0; row < columns[0].Length; row++)
            {
                string userId = Read<string>(columns[0], row, "user_id");
                string matchId = Read<string>(columns[1], row, "match_id");
                string mapId = Read<string>(columns[2], row, "map_id");

                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(matchId))
                    throw new InvalidDataException("A row has an empty user_id or match_id.");

                var map = GetMap(mapId);

                float x = Read<float>(columns[3], row, "x");
                float y = Read<float>(columns[4], row, "y");
                float z = Read<float>(columns[5], row, "z");

                DateTime timestamp = Read<DateTime>(columns[6], row, "ts");

                // Recover the integer from the dataset's timestamp[ms] representation.
                long rawTimestamp = (timestamp.Ticks - epoch.Ticks) / TimeSpan.TicksPerMillisecond;

                DateTimeOffset date = rawTimestampsAreUnixSeconds
                    ? DateTimeOffset.FromUnixTimeSeconds(rawTimestamp)
                    : DateTimeOffset.FromUnixTimeMilliseconds(rawTimestamp);

                object eventValue = columns[7].GetValue(row);
                string eventType = eventValue is byte[] bytes ? Encoding.UTF8.GetString(bytes)
                    : eventValue as string;

                if (eventType == null) throw new InvalidDataException("Invalid event column value.");

                if (!matches.TryGetValue(matchId, out var match))
                {
                    match = new MatchData
                    {
                        matchId = matchId,
                        mapId = mapId,
                        file = sourceFile,
                        startRawTimestamp = rawTimestamp
                    };
                    matches.Add(matchId, match);
                    players.Add(matchId, new Dictionary<string, PlayerMatchData>(StringComparer.Ordinal));
                    dates.Add(matchId, new HashSet<string>(StringComparer.Ordinal));
                }
                if (match.mapId != mapId)
                    throw new InvalidDataException($"Match {matchId} contains multiple map IDs.");

                if (!players[matchId].TryGetValue(userId, out var player))
                {
                    player = new PlayerMatchData
                    {
                        userId = userId,
                        isBot = userId.All(character => character >= '0' && character <= '9')
                    };
                    players[matchId].Add(userId, player);
                    match.players.Add(player);
                }
                player.events.Add(new PlayerEventData
                {
                    rawTimestamp = rawTimestamp,
                    eventType = eventType,
                    x = x, y = y, z = z,
                    u = (x - map.originX) / map.scale,
                    v = (z - map.originZ) / map.scale
                });
                match.startRawTimestamp = Math.Min(match.startRawTimestamp, rawTimestamp);
                match.eventCount++;
                dates[matchId].Add(date.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }

        double unitsPerSecond = rawTimestampsAreUnixSeconds ? 1.0 : 1000.0;

        foreach (var match in matches.Values)
        {
            match.playerCount = match.players.Count;
            match.dates = dates[match.matchId].OrderBy(date => date, StringComparer.Ordinal).ToList();
            match.players = match.players.OrderBy(player => player.userId, StringComparer.Ordinal).ToList();

            foreach (var player in match.players)
            {
                // Stable ordering preserves source order when events share a timestamp.
                player.events = player.events.OrderBy(playerEvent => playerEvent.rawTimestamp).ToList();

                foreach (var playerEvent in player.events)
                {
                    playerEvent.timeSeconds = (playerEvent.rawTimestamp - match.startRawTimestamp) / unitsPerSecond;
                    match.durationSeconds = Math.Max(match.durationSeconds, playerEvent.timeSeconds);
                }
            }
        }
        return matches.Values.OrderBy(match => match.matchId, StringComparer.Ordinal).ToList();
    }

    private static T Read<T>(Array column, int row, string name)
    {
        if (column.GetValue(row) is T value) return value;

        throw new InvalidDataException($"Invalid or null {name} at row {row}; expected {typeof(T).Name}.");
    }

    private static (float scale, float originX, float originZ) GetMap(string mapId)
    {
        switch (mapId)
        {
            case "AmbroseValley": return (900f, -370f, -473f);
            case "GrandRift": return (581f, -290f, -290f);
            case "Lockdown": return (1000f, -500f, -500f);
            default: throw new InvalidDataException($"Unknown map: {mapId}");
        }
    }

    public static async Task<PlayerData> LoadAllAsync(string folderPath, bool rawTimestampsAreUnixSeconds = true, Action<int, int> onProgress = null)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL has no worker threads; retain cooperative loading there.
        return await LoadAllCoreAsync(folderPath, rawTimestampsAreUnixSeconds, onProgress);
#else
        int completed = 0;
        int total = 0;
        int reported = -1;
        // Parsing and grouping use plain C# objects and run off Unity's main thread.
        var work = Task.Run(() => LoadAllCoreAsync(folderPath, rawTimestampsAreUnixSeconds,
            (count, fileCount) =>
            {
                Volatile.Write(ref total, fileCount);
                Volatile.Write(ref completed, count);
            }));

        // UI callbacks stay on the caller's Unity context, at most once per tick.
        while (!work.IsCompleted)
        {
            int current = Volatile.Read(ref completed);
            if (current != reported)
            {
                onProgress?.Invoke(current, Volatile.Read(ref total));
                reported = current;
            }
            await Task.WhenAny(work, Task.Delay(33));
        }
        var data = await work;
        onProgress?.Invoke(Volatile.Read(ref completed), Volatile.Read(ref total));
        return data;
#endif
    }

    private static async Task<PlayerData> LoadAllCoreAsync(string folderPath, bool rawTimestampsAreUnixSeconds, Action<int, int> onProgress)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string root = folderPath.Replace('\\', '/').TrimEnd('/');
        byte[] manifestBytes = await DownloadAsync(root + "/parquet-files.txt");
        string[] paths = Encoding.UTF8.GetString(manifestBytes).TrimStart('\uFEFF')
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string[] files = paths.Select(path =>
        {
            if (path.StartsWith("/") || path.Contains("\\") || path.Contains(":") ||
                path.Split('/').Any(part => part == ".." || part == ".") ||
                !path.EndsWith(".nakama-0", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid path in player data manifest: " + path);
            return root + "/" + string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
        }).ToArray();
#else
        string[] files = Directory.GetFiles(folderPath, "*.nakama-0", SearchOption.AllDirectories);
#endif

        Array.Sort(files, StringComparer.Ordinal);

        var fileMatches = new List<MatchData>[files.Length];

        onProgress?.Invoke(0, files.Length);

#if UNITY_WEBGL && !UNITY_EDITOR
        var slice = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < files.Length; i++)
        {
            fileMatches[i] = await LoadAsync(files[i], rawTimestampsAreUnixSeconds);
            onProgress?.Invoke(i + 1, files.Length);
            if (slice.ElapsedMilliseconds >= 8)
            {
                await Task.Yield();
                slice.Restart();
            }
        }
#else
        int nextFile = -1;
        int completed = 0;
        int workerCount = Math.Min(files.Length, Math.Min(4, Math.Max(1, Environment.ProcessorCount)));
        var workers = new Task[workerCount];
        object progressLock = new object();
        for (int worker = 0; worker < workers.Length; worker++)
        {
            workers[worker] = Task.Run(async () =>
            {
                int index;
                while ((index = Interlocked.Increment(ref nextFile)) < files.Length)
                {
                    fileMatches[index] = await LoadAsync(files[index], rawTimestampsAreUnixSeconds);
                    lock (progressLock)
                        onProgress?.Invoke(++completed, files.Length);
                }
            });
        }
        await Task.WhenAll(workers);
#endif
        // Preserve source order regardless of which worker finished first.
        var loadedMatches = fileMatches.SelectMany(matches => matches);

        var data = new PlayerData();

        double unitsPerSecond = rawTimestampsAreUnixSeconds ? 1.0 : 1000.0;

        foreach (var matchGroup in loadedMatches.GroupBy(m => m.matchId))
        {
            var mapIds = matchGroup.Select(m => m.mapId).Distinct().ToList();

            if (mapIds.Count != 1)
                throw new InvalidDataException($"Match {matchGroup.Key} contains multiple maps.");

            var match = new MatchData
            {
                matchId = matchGroup.Key,
                mapId = mapIds[0],

                dates = matchGroup.SelectMany(m => m.dates).Distinct().OrderBy(date => date).ToList(),

                startRawTimestamp = matchGroup.Min(m => m.startRawTimestamp),

                file = string.Empty
            };

            var playerGroups = matchGroup.SelectMany(m => m.players).GroupBy(p => p.userId);

            foreach (var playerGroup in playerGroups)
            {
                var player = new PlayerMatchData
                {
                    userId = playerGroup.Key,
                    isBot = playerGroup.First().isBot,

                    events = playerGroup.SelectMany(p => p.events).OrderBy(e => e.rawTimestamp).ToList()
                };

                foreach (var playerEvent in player.events)
                {
                    playerEvent.timeSeconds = (playerEvent.rawTimestamp - match.startRawTimestamp) / unitsPerSecond;

                    match.durationSeconds = Math.Max(match.durationSeconds, playerEvent.timeSeconds);
                }
                match.eventCount += player.events.Count;
                match.players.Add(player);
            }
            match.playerCount = match.players.Count;
            data.matches.Add(match);
        }
        return data;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static async Task<byte[]> DownloadAsync(string url)
    {
        using var request = UnityEngine.Networking.UnityWebRequest.Get(url);
        request.timeout = 60;
        var operation = request.SendWebRequest();
        while (!operation.isDone) await Task.Yield();
        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            throw new IOException($"Could not load {url}: HTTP {request.responseCode}, {request.error}");
        return request.downloadHandler.data;
    }
#endif
}
