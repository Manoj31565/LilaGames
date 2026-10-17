using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [SerializeField] GameObject MainMenu;
    [SerializeField] GameObject ExistingMatchesMenu;

    public static PlayerData playerData = new PlayerData();

    [SerializeField] private TMP_Text loadingText;
    private bool isLoading;
    private bool dataLoaded;

    [SerializeField] GameObject MatchPrefab;

    [SerializeField] Transform matchesContent;

    [SerializeField] TMP_Dropdown mapsDropDown;

    [SerializeField] TMP_InputField dateInputField;
    [SerializeField] TMP_InputField matchIDInputField;
    private void Start()
    {
        MainMenu.SetActive(true);
        ExistingMatchesMenu.SetActive(false);
    }

    public void ShowMainMenu()
    {
        if (!dataLoaded) return;

        MainMenu.SetActive(true);
        ExistingMatchesMenu.SetActive(false);
    }

    public async void ShowExistingMatchesMenu()
    {
        MainMenu.SetActive(false);
        ExistingMatchesMenu.SetActive(true);

        if (isLoading || dataLoaded) return;

        if(playerData.matches.Count <= 0)
        {
            isLoading = true;
            
            loadingText.gameObject.SetActive(true);
            loadingText.text = "Loading Parquet data...";
            try
            {
                await Awaitable.NextFrameAsync();

                mapsDropDown.interactable = false;
                dateInputField.interactable = false;
                matchIDInputField.interactable = false;

                if (this == null) return;

                string folderPath = Path.Combine(Application.streamingAssetsPath, "player_data");

                playerData = await ParquetLoader.LoadAllAsync(folderPath, onProgress: (loaded, total) =>
                {
                    if (this == null || loadingText == null) return;

                    loadingText.text = $"{loaded}/{total} loaded";

                    if (loaded == total)
                        loadingText.text += "\nPreparing matches...";
                });
                if (this == null) return;

                dataLoaded = true;
                loadingText.text = $"Loading complete — {playerData.matches.Count} matches";

                mapsDropDown.interactable = true;
                dateInputField.interactable = true;
                matchIDInputField.interactable = true;
            }
            catch (Exception exception)
            {
                if (this != null && loadingText != null)
                    loadingText.text = $"Loading failed: {exception.Message}\nReopen this menu to retry.";

                Debug.LogException(exception);
                return;
            }
            finally
            {
                isLoading = false;
            }
        }
        else
        {
            dataLoaded = true;
            isLoading = false;
        }

        foreach (var match in playerData.matches)
        {
            var matchGO = Instantiate(MatchPrefab, matchesContent);

            var matchButton = matchGO.GetComponent<Button>();

            matchButton.onClick.AddListener(() => OpenMatch(match));

            var matchText = matchGO.GetComponentInChildren<TMP_Text>();

            string matchId = match.matchId.Replace(".nakama-0", "");

            matchText.text = $"{matchId}";
        }
    }

    private void OpenMatch(MatchData matchData)
    {
        Constants.currentMatchData = matchData;

        PlayerPrefs.SetString("SelectedMatchId", matchData.matchId);

        SceneManager.LoadScene("Game");
    }

    public void SearchButton()
    {
        if (!dataLoaded) return;

        List<MatchData> filteredMatches = new List<MatchData>(playerData.matches);

        switch (mapsDropDown.value)
        {
            case 1:
                filteredMatches = filteredMatches.Where(m => m.mapId == "AmbroseValley").ToList();
                break;
            case 2:
                filteredMatches = filteredMatches.Where(m => m.mapId == "GrandRift").ToList();
                break;
            case 3:
                filteredMatches = filteredMatches.Where(m => m.mapId == "Lockdown").ToList();
                break;
        }

        if (!string.IsNullOrEmpty(dateInputField.text))
        {
            filteredMatches = filteredMatches.Where(m => m.dates.Any(d => d.Contains(dateInputField.text))).ToList();
        }

        if (!string.IsNullOrEmpty(matchIDInputField.text))
        {
            filteredMatches = filteredMatches.Where(m => m.matchId.Contains(matchIDInputField.text)).ToList();
        }

        if (filteredMatches.Count == 0)
        {
            for (int i = 0; i < matchesContent.childCount; i++)
            {
                Destroy(matchesContent.GetChild(i).gameObject);
            }
            loadingText.text = "No matches found with the given criteria.";

            return;
        }
        else
        {
            loadingText.text = $"{filteredMatches.Count} Matches Found.";
        }

        for (int i = 0; i < matchesContent.childCount; i++)
        {
            Destroy(matchesContent.GetChild(i).gameObject);
        }

        foreach (var match in filteredMatches)
        {
            var matchGO = Instantiate(MatchPrefab, matchesContent);

            var matchButton = matchGO.GetComponent<Button>();

            matchButton.onClick.AddListener(() => OpenMatch(match));

            var matchText = matchGO.GetComponentInChildren<TMP_Text>();

            string matchId = match.matchId.Replace(".nakama-0", "");

            matchText.text = $"{matchId}";
        }
    }
}
