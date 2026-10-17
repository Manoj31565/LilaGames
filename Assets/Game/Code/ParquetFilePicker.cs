using System;
using System.IO;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ParquetFilePicker : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;
    public PlayerData uploadedData = new PlayerData();
    private bool isParsing;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OpenPlayerFilePicker(string objectName);
#endif

    [Serializable]
    public class SelectedParquetFile
    {
        public string name;
        public string base64;
    }

    public void OpenFilePicker()
    {
        if (isParsing) return;
#if UNITY_WEBGL && !UNITY_EDITOR
        OpenPlayerFilePicker(gameObject.name);
#else
        SetStatus("Test the file picker using a Web Build And Run.");
#endif
    }

    // Called by the JavaScript plugin with the selected file's bytes.
    public async void OnParquetFileLoaded(string json)
    {
        if (isParsing) return;
        isParsing = true;
        uploadedData = new PlayerData();

        try
        {
            SetStatus("Parsing selected Parquet file...");
            await Awaitable.NextFrameAsync();
            if (this == null) return;

            var selectedFile = JsonUtility.FromJson<SelectedParquetFile>(json);
            if (selectedFile == null || string.IsNullOrEmpty(selectedFile.base64))
                throw new InvalidDataException("The selected file is empty.");

            byte[] bytes = Convert.FromBase64String(selectedFile.base64);
            using var stream = new MemoryStream(bytes, writable: false);
            var matches = await ParquetLoader.LoadAsync(stream, selectedFile.name);
            if (this == null) return;

            uploadedData = new PlayerData { matches = matches };
            if (matches.Count == 0)
            {
                SetStatus("No match data found in this file.");
                return;
            }
            if (matches.Count > 1)
            {
                SetStatus($"Loaded {matches.Count} matches. Select a match to open.");
                // Available for a match-selection UI through uploadedData.matches.
                return;
            }

            OpenUploadedMatch(0);
        }
        catch (Exception exception)
        {
            if (this != null) SetStatus($"Could not load Parquet: {exception.Message}");
            Debug.LogException(exception);
        }
        finally
        {
            isParsing = false;
        }
    }

    public void OpenUploadedMatch(int index)
    {
        if (index < 0 || index >= uploadedData.matches.Count)
        {
            SetStatus("Select a valid uploaded match.");
            return;
        }
        MatchData match = uploadedData.matches[index];
        Constants.currentMatchData = match;
        PlayerPrefs.SetString("SelectedMatchId", match.matchId);
        SceneManager.LoadScene("Game");
    }

    public void OnFilePickerError(string message)
    {
        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.richText = false;
            statusText.text = message;
        }
        Debug.Log(message);
    }
}
