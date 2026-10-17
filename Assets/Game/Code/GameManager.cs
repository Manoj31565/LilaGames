using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] GameObject[] maps;
    [SerializeField] GameObject PlayerPrefab;
    [SerializeField] GameObject BotPrefab;
    [SerializeField] HeatmapRenderer heatmapRenderer;

    GameObject selectedMap;

    [SerializeField] private MatchPlaybackController playback;

    private void Start()
    {
        var match = Constants.currentMatchData;

        if (match == null || maps == null || maps.Length == 0)
        {
            Debug.LogError("Select a match and assign maps before starting playback.");
            return;
        }
        selectedMap = System.Array.Find(maps, map => map != null && map.name == match.mapId);

        if (selectedMap == null)
        {
            selectedMap = System.Array.Find(maps, map => map != null);

            Debug.LogWarning($"Map '{match.mapId}' not found. Using the default map.");
        }
        if (selectedMap == null) return;

        foreach (var map in maps)
        {
            if (map != null) map.SetActive(map == selectedMap);
        }
        Vector3 scale = selectedMap.transform.localScale;

        switch (selectedMap.name)
        {
            case "AmbroseValley":
                scale = new Vector3(1f, 1f, 1f);
                break;
            case "GrandRift":
                scale = new Vector3(0.5f, 0.5f, 0.5f);
                break;
            case "Lockdown":
                scale = new Vector3(1.5f, 1.5f, 1.5f);
                break;
        }

        var mapRenderer = selectedMap.GetComponent<SpriteRenderer>();

        if (mapRenderer == null || mapRenderer.sprite == null)
        {
            Debug.LogError("Selected map needs a SpriteRenderer with a sprite.");

            return;
        }
        double playbackStartTime = Time.fixedTimeAsDouble;

        if (heatmapRenderer == null)
            heatmapRenderer = FindFirstObjectByType<HeatmapRenderer>();
        if (heatmapRenderer != null)
            heatmapRenderer.Initialize(match, mapRenderer, playbackStartTime);

        foreach (var playerData in match.players)
        {
            var prefab = playerData.isBot ? BotPrefab : PlayerPrefab;

            if (prefab == null || prefab.GetComponent<Player>() == null)
            {
                Debug.LogError("Player and bot prefabs must have a Player component.");

                continue;
            }
            var playerObject = Instantiate(prefab);
            playerObject.name = $"Player_{playerData.userId}";
            playerObject.GetComponent<Player>().Initialize(playerData, mapRenderer, playbackStartTime, scale);
        }
        playback.Initialize(match);
    }

    public void MainMenuButton()
    {
        SceneManager.LoadScene("Menu");
    }
}
