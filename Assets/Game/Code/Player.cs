using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI userIdText;
    [SerializeField] Canvas playerCanvas;

    private PlayerEventData[] movementEvents = Array.Empty<PlayerEventData>();
    private PlayerEventData[] lootEvents = Array.Empty<PlayerEventData>();
    private PlayerEventData[] killEvents = Array.Empty<PlayerEventData>();
    private PlayerEventData[] deathEvents = Array.Empty<PlayerEventData>();
    private PlayerEventData[] stormDeathEvents = Array.Empty<PlayerEventData>();
    private SpriteRenderer mapRenderer;
    private double playbackStartTime;
    private int eventIndex;

    GameObject lootCoin;
    GameObject deathAvatar;
    GameObject deathMarker;
    GameObject stormDeathMarker;

    private int lootEventIndex;
    private int killEventIndex;
    private int deathEventIndex;
    private int stormDeathEventIndex;

    MatchPlaybackController playback;

    private readonly Dictionary<PlayerEventData, GameObject> spawnedMarkers = new();
    private void Start()
    {
        if (playerCanvas != null) playerCanvas.worldCamera = Camera.main;

        lootCoin = Resources.Load<GameObject>("LootCoin");
        deathAvatar = Resources.Load<GameObject>("KillMarker");
        deathMarker = Resources.Load<GameObject>("DeathMarker");
        stormDeathMarker = Resources.Load<GameObject>("StormDeathMarker");

        playback = GameObject.FindFirstObjectByType<MatchPlaybackController>();
    }

    public void SetUserID(string userId)
    {
        if (userIdText != null) userIdText.text = userId;
    }

    public void Initialize(PlayerMatchData data, SpriteRenderer map, double startTime, Vector3 scale)
    {
        if(data.isBot)
        {
            playerCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(15f, 3.5f);
        }
        else
        {
            playerCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(30f, 3.5f);
        }
        this.transform.localScale = scale;

        SetUserID(data.userId);

        mapRenderer = map;

        playbackStartTime = startTime;

        eventIndex = 0;

        movementEvents = data.events == null ? Array.Empty<PlayerEventData>() : data.events.Where(e => e != null && (e.eventType == "Position" || e.eventType == "BotPosition")).OrderBy(e => e.timeSeconds).ToArray();

        if (movementEvents.Length > 0 && mapRenderer != null && mapRenderer.sprite != null)
            transform.position = MapPosition(movementEvents[0]);
        else
        {
            var first = data.events?.Where(e => e != null).OrderBy(e => e.timeSeconds).FirstOrDefault();

            if (first != null && mapRenderer != null && mapRenderer.sprite != null)
                transform.position = MapPosition(first);
        }
        lootEventIndex = 0;
        killEventIndex = 0;
        deathEventIndex = 0;
        stormDeathEventIndex = 0;

        var events = data.events?.Where(e => e != null).OrderBy(e => e.timeSeconds).ToArray() ?? Array.Empty<PlayerEventData>();

        lootEvents = events.Where(e => e.eventType == "Loot").ToArray();

        killEvents = events.Where(e => e.eventType == "Kill" || e.eventType == "BotKill").ToArray();
        
        deathEvents = events.Where(e => e.eventType == "Killed" || e.eventType == "BotKilled").ToArray();

        stormDeathEvents = events.Where(e => e.eventType == "KilledByStorm").ToArray();
    }

    private void FixedUpdate()
    {
        if (playback == null || mapRenderer == null || mapRenderer.sprite == null) return;

        double elapsed = playback.CurrentTime;

        // Marker indices point to the next unprocessed event (and may equal Length).
        // Rewind by inspecting the last processed event, not the next index.
        while (lootEventIndex > 0 && lootEvents[lootEventIndex - 1].timeSeconds > elapsed)
            lootEventIndex--;

        while (lootEventIndex < lootEvents.Length && lootEvents[lootEventIndex].timeSeconds <= elapsed)
        {
            SpawnEventMarker(lootCoin, lootEvents[lootEventIndex]);

            lootEventIndex++;
        }

        while (killEventIndex > 0 && killEvents[killEventIndex - 1].timeSeconds > elapsed)
            killEventIndex--;

        while (killEventIndex < killEvents.Length && killEvents[killEventIndex].timeSeconds <= elapsed)
        {
            SpawnEventMarker(deathAvatar, killEvents[killEventIndex]);

            killEventIndex++;
        }

        while (deathEventIndex > 0 && deathEvents[deathEventIndex - 1].timeSeconds > elapsed)
            deathEventIndex--;

        while (deathEventIndex < deathEvents.Length && deathEvents[deathEventIndex].timeSeconds <= elapsed)
        {
            SpawnEventMarker(deathMarker, deathEvents[deathEventIndex]);

            deathEventIndex++;
        }

        while (stormDeathEventIndex > 0 && stormDeathEvents[stormDeathEventIndex - 1].timeSeconds > elapsed)
            stormDeathEventIndex--;

        while (stormDeathEventIndex < stormDeathEvents.Length && stormDeathEvents[stormDeathEventIndex].timeSeconds <= elapsed)
        {
            SpawnEventMarker(stormDeathMarker, stormDeathEvents[stormDeathEventIndex]);

            stormDeathEventIndex++;
        }
        foreach (var entry in spawnedMarkers)
        {
            GameObject marker = entry.Value;

            if (marker == null) continue;

            bool shouldBeVisible = entry.Key.timeSeconds <= elapsed;

            if (marker.activeSelf != shouldBeVisible)
                marker.SetActive(shouldBeVisible);
        }
        if (movementEvents.Length == 0) return;

        // Rewind to the sample before the requested time when the slider moves back.
        while (eventIndex > 0 && movementEvents[eventIndex].timeSeconds > elapsed)
            eventIndex--;

        while (eventIndex + 1 < movementEvents.Length && movementEvents[eventIndex + 1].timeSeconds <= elapsed)
            eventIndex++;

        var current = movementEvents[eventIndex];

        if (elapsed <= current.timeSeconds || eventIndex == movementEvents.Length - 1)
        {
            transform.position = MapPosition(current);
            return;
        }
        var next = movementEvents[eventIndex + 1];

        double interval = next.timeSeconds - current.timeSeconds;

        float fraction = interval > 0 ? (float)((elapsed - current.timeSeconds) / interval) : 1f;

        transform.position = Vector3.Lerp(MapPosition(current), MapPosition(next), fraction);
    }

    private Vector3 MapPosition(PlayerEventData playerEvent)
    {
        Bounds bounds = mapRenderer.sprite.bounds;

        float u = mapRenderer.flipX ? 1f - playerEvent.u : playerEvent.u;
        float v = mapRenderer.flipY ? 1f - playerEvent.v : playerEvent.v;

        return mapRenderer.transform.TransformPoint(new Vector3(bounds.min.x + u * bounds.size.x, bounds.min.y + v * bounds.size.y, 0f));
    }

    private void SpawnEventMarker(GameObject prefab, PlayerEventData playerEvent)
    {
        if (prefab == null || spawnedMarkers.ContainsKey(playerEvent))
            return;

        GameObject marker = Instantiate(prefab, MapPosition(playerEvent), mapRenderer.transform.rotation);

        switch(mapRenderer.transform.name)
        {
            case "AmbroseValley":
                {
                    marker.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                    break;
                }
            case "GrandRift":
                {
                    marker.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
                    break;
                }
            case "Lockdown":
                {
                    marker.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                    break;
                }
        }
        spawnedMarkers.Add(playerEvent, marker);
    }

    private void OnDestroy()
    {
        foreach (var marker in spawnedMarkers.Values)
        {
            if (marker != null)
                Destroy(marker);
        }
        spawnedMarkers.Clear();
    }
}
