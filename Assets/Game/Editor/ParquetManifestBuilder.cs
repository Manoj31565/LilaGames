using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// File discovery only: every match is still parsed from original Parquet at runtime.
public sealed class ParquetManifestBuilder : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => Generate();

    [MenuItem("Tools/Lila/Refresh Parquet File List")]
    public static void Generate()
    {
        string root = Path.Combine(Application.streamingAssetsPath, "player_data");
        if (!Directory.Exists(root))
            throw new BuildFailedException("Missing StreamingAssets/player_data folder.");
        string[] paths = Directory.GetFiles(root, "*.nakama-0", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (paths.Length == 0) throw new BuildFailedException("No original Parquet files found.");
        File.WriteAllLines(Path.Combine(root, "parquet-files.txt"), paths, new UTF8Encoding(false));
        AssetDatabase.ImportAsset("Assets/StreamingAssets/player_data/parquet-files.txt");
    }
}
