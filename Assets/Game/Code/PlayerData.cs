using System;
using System.Collections.Generic;

[Serializable]
public class PlayerData
{
    public List<MatchData> matches = new List<MatchData>();
}

[Serializable]
public class MatchData
{
    public string matchId;
    public string mapId;

    public List<string> dates = new List<string>();

    public double durationSeconds;
    public int playerCount;
    public int eventCount;

    public string file;
    public long startRawTimestamp;

    public List<PlayerMatchData> players = new List<PlayerMatchData>();
}

[Serializable]
public class PlayerMatchData
{
    public string userId;
    public bool isBot;

    public List<PlayerEventData> events = new List<PlayerEventData>();
}

[Serializable]
public class PlayerEventData
{
    public double timeSeconds;
    public long rawTimestamp;
    public string eventType;

    public float x;
    public float y;
    public float z;

    public float u;
    public float v;
}