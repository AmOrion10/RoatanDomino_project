using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RoatanAndroidBuilder
{
    public const string DefaultApkOutputPath = "Builds/Android/RoatanDomino.apk";

    [MenuItem("Roatan Domino/Build Android APK")]
    public static void BuildAndroidApk()
    {
        ConfigureMobilePlayerSettings();

        string outputDir = Path.GetDirectoryName(DefaultApkOutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled && File.Exists(s.path))
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            scenes = new[] { "Assets/Scenes/MenuScene.unity" };
        }

        EditorUserBuildSettings.buildAppBundle = false;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = DefaultApkOutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log($"[RoatanAndroidBuilder] Starting Android APK build -> {DefaultApkOutputPath} ({scenes.Length} scenes)...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[RoatanAndroidBuilder] Build SUCCEEDED! Size: {summary.totalSize / (1024f * 1024f):F2} MB -> {Path.GetFullPath(DefaultApkOutputPath)}");
        }
        else
        {
            Debug.LogError($"[RoatanAndroidBuilder] Build FAILED: {summary.result} (Errors: {summary.totalErrors})");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }

    public static void ConfigureMobilePlayerSettings()
    {
        PlayerSettings.companyName = "RoatanDomino";
        PlayerSettings.productName = "Roatan Domino";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.roatandomino.game");
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.roatandomino.game");

        // Portrait lock (Jogatina-style one-handed phone play)
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        // Android settings compatible with modern 64-bit Android phones and Play Store
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
    }
}
