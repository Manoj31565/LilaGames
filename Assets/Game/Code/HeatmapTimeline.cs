using System;
using System.Collections.Generic;
using System.Linq;

public enum HeatmapZone { Kills, Deaths, Traffic }

// Timeline selection is independent of rendering, and supports forward/backward seeking.
public sealed class HeatmapTimeline
{
    private readonly PlayerEventData[] events;
    public HeatmapTimeline(MatchData match, HeatmapZone zone)
    {
        events = (match.players ?? new List<PlayerMatchData>())
            .Where(p => p?.events != null).SelectMany(p => p.events)
            .Where(e => e != null && Matches(e.eventType, zone) &&
                !double.IsNaN(e.timeSeconds) && !double.IsInfinity(e.timeSeconds) &&
                !float.IsNaN(e.u) && !float.IsNaN(e.v) &&
                e.u >= 0f && e.u <= 1f && e.v >= 0f && e.v <= 1f)
            .OrderBy(e => e.timeSeconds).ToArray();
    }

    public IEnumerable<PlayerEventData> Active(double time, double window)
    {
        int low = 0, high = events.Length;
        double earliest = window > 0 ? time - window : double.NegativeInfinity;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (events[middle].timeSeconds <= earliest) low = middle + 1;
            else high = middle;
        }
        for (int i = low; i < events.Length && events[i].timeSeconds <= time; i++)
            yield return events[i];
    }

    public static float Weight(double eventTime, double time, double window)
    {
        if (eventTime > time) return 0f;
        return window <= 0 ? 1f : (float)Math.Max(0, Math.Min(1, 1 - (time - eventTime) / window));
    }

    private static bool Matches(string type, HeatmapZone zone)
    {
        switch (zone)
        {
            case HeatmapZone.Kills: return type == "Kill" || type == "BotKill";
            case HeatmapZone.Deaths: return type == "Killed" || type == "BotKilled" || type == "KilledByStorm";
            case HeatmapZone.Traffic: return type == "Position" || type == "BotPosition";
            default: return false;
        }
    }
}
