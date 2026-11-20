using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RoatanAndroidBuilder
{
    public const string DefaultApkOutputPath = "Builds/Android/RoatanDomino.apk";
    public const string AppIconAssetPath = "Assets/Resources/Images/RoatanAppIcon.png";

    [MenuItem("Roatan Domino/Build Android APK")]
    public static void BuildAndroidApk()
    {
        ConfigureMobilePlayerSettings();
        ConfigureAppIcon();
        bool releaseSigned = ConfigureReleaseSigningFromEnv();

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
        EditorUserBuildSettings.development = false;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = DefaultApkOutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log($"[RoatanAndroidBuilder] Starting Android APK build ({(releaseSigned ? "RELEASE-SIGNED" : "DEBUG-SIGNED")}) -> {DefaultApkOutputPath} ({scenes.Length} scenes)...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        // Clear local keystore path/secrets so ProjectSettings.asset never stores machine-specific paths
        if (releaseSigned)
        {
            PlayerSettings.Android.keystorePass = string.Empty;
            PlayerSettings.Android.keyaliasPass = string.Empty;
            PlayerSettings.Android.keystoreName = string.Empty;
            PlayerSettings.Android.keyaliasName = string.Empty;
            PlayerSettings.Android.useCustomKeystore = false;
        }

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[RoatanAndroidBuilder] Build SUCCEEDED ({(releaseSigned ? "RELEASE-SIGNED" : "DEBUG-SIGNED")})! Size: {summary.totalSize / (1024f * 1024f):F2} MB -> {Path.GetFullPath(DefaultApkOutputPath)}");
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

        // Android settings compatible with modern 64-bit Android phones, APKPure, and Play Store
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
    }

    private static void ConfigureAppIcon()
    {
        if (!File.Exists(AppIconAssetPath))
        {
            Debug.LogWarning($"[RoatanAndroidBuilder] App icon not found at {AppIconAssetPath}");
            return;
        }

        AssetDatabase.ImportAsset(AppIconAssetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(AppIconAssetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Texture2D iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconAssetPath);
        if (iconTex == null)
        {
            Debug.LogWarning($"[RoatanAndroidBuilder] Failed to load Texture2D from {AppIconAssetPath}");
            return;
        }

        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { iconTex }, IconKind.Any);

        int[] androidSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Android, IconKind.Application);
        if (androidSizes != null && androidSizes.Length > 0)
        {
            Texture2D[] androidIcons = new Texture2D[androidSizes.Length];
            for (int i = 0; i < androidSizes.Length; i++)
            {
                androidIcons[i] = iconTex;
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Android, androidIcons, IconKind.Application);
        }

        Debug.Log($"[RoatanAndroidBuilder] Configured Android app icon from {AppIconAssetPath} ({iconTex.width}x{iconTex.height})");
    }

    private static bool ConfigureReleaseSigningFromEnv()
    {
        string keystorePath = Environment.GetEnvironmentVariable("ROATAN_KEYSTORE_PATH");
        string keystorePass = Environment.GetEnvironmentVariable("ROATAN_KEYSTORE_PASS");
        string keyAlias = Environment.GetEnvironmentVariable("ROATAN_KEY_ALIAS");
        string keyPass = Environment.GetEnvironmentVariable("ROATAN_KEY_PASS");

        if (!string.IsNullOrWhiteSpace(keystorePath) &&
            File.Exists(keystorePath) &&
            !string.IsNullOrWhiteSpace(keystorePass) &&
            !string.IsNullOrWhiteSpace(keyAlias) &&
            !string.IsNullOrWhiteSpace(keyPass))
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = keyAlias;
            PlayerSettings.Android.keyaliasPass = keyPass;
            Debug.Log($"[RoatanAndroidBuilder] Using Release Keystore: {keystorePath} (alias: {keyAlias})");
            return true;
        }

        PlayerSettings.Android.useCustomKeystore = false;
        Debug.LogWarning("[RoatanAndroidBuilder] Release keystore env vars not set; falling back to debug signing.");
        return false;
    }
}
