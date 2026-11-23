using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public static class RoatanAndroidBuilder
{
    public const string DefaultApkOutputPath = "Builds/Android/RoatanDomino.apk";
    public const string FallbackScenePath = "Assets/Scenes/TutorialScene.unity";
    public const string SecondaryFallbackScenePath = "Assets/Scenes/MenuScene.unity";
    public const string AppIconAssetPath = "Assets/Resources/Images/RoatanAppIcon.png";

    [MenuItem("Roatan Domino/Build Android APK")]
    public static void BuildAndroidApk()
    {
        ConfigureMobilePlayerSettings();
        ConfigureAppIcon();
        bool releaseSigned = ConfigureReleaseSigning(out bool configuredFromEnv);

        string fullOutputPath = Path.GetFullPath(DefaultApkOutputPath);
        string outputDir = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        bool existedBeforeBuild = File.Exists(fullOutputPath);
        string[] scenes = ResolveBuildScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError($"[RoatanAndroidBuilder] Build aborted: no valid build scenes found (checked active scene, EditorBuildSettings, and fallback '{FallbackScenePath}').");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
            return;
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

        string signingLabel = releaseSigned ? "RELEASE-SIGNED" : "DEBUG-SIGNED";
        Debug.Log($"[RoatanAndroidBuilder] Starting Android ARM64/IL2CPP APK build ({signingLabel}) -> {fullOutputPath} ({scenes.Length} scenes: {string.Join(", ", scenes)})...");

        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            // Clear env-injected keystore path/secrets so ProjectSettings.asset never stores machine-specific paths or credentials
            if (configuredFromEnv)
            {
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
                PlayerSettings.Android.keystoreName = string.Empty;
                PlayerSettings.Android.keyaliasName = string.Empty;
                PlayerSettings.Android.useCustomKeystore = false;
            }
        }

        BuildSummary summary = report.summary;
        string commitHash = GetGitShortCommitHash();

        if (summary.result == BuildResult.Succeeded)
        {
            long fileBytes = File.Exists(fullOutputPath) ? new FileInfo(fullOutputPath).Length : (long)summary.totalSize;
            double sizeMb = fileBytes / (1024d * 1024d);
            string actionWord = existedBeforeBuild ? "Overwrote" : "Created";
            Debug.Log($"[RoatanAndroidBuilder] Build SUCCEEDED ({signingLabel} | commit {commitHash})! {actionWord} APK: {fullOutputPath} ({sizeMb:F2} MB / {fileBytes:N0} bytes)");
        }
        else
        {
            Debug.LogError($"[RoatanAndroidBuilder] Build FAILED ({signingLabel} | commit {commitHash}): {summary.result} (Errors: {summary.totalErrors})");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }

    public static string[] ResolveBuildScenes()
    {
        List<string> resolved = EditorBuildSettings.scenes
            .Where(s => s != null && s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path))
            .Select(s => s.path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Scene activeScene = EditorSceneManager.GetActiveScene();
        bool activeSceneIsSaved = activeScene.IsValid()
            && !string.IsNullOrEmpty(activeScene.path)
            && File.Exists(activeScene.path);

        if (resolved.Count == 0)
        {
            if (activeSceneIsSaved)
            {
                resolved.Add(activeScene.path);
            }
            else
            {
                Debug.LogWarning($"[RoatanAndroidBuilder] Active scene is unsaved/Untitled and EditorBuildSettings has no enabled scenes; defaulting to '{FallbackScenePath}'.");
                if (File.Exists(FallbackScenePath))
                {
                    resolved.Add(FallbackScenePath);
                }
                else if (File.Exists(SecondaryFallbackScenePath))
                {
                    resolved.Add(SecondaryFallbackScenePath);
                }
            }
        }
        else if (!activeSceneIsSaved)
        {
            // Never allow an unsaved/Untitled scene to sneak into the build; ensure fallback exists if needed
            Debug.Log($"[RoatanAndroidBuilder] Active scene is unsaved/Untitled; using {resolved.Count} saved scene(s) from EditorBuildSettings.");
        }

        return resolved.ToArray();
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

    private static bool ConfigureReleaseSigning(out bool configuredFromEnv)
    {
        configuredFromEnv = false;

        string keystorePath = Environment.GetEnvironmentVariable("ROATAN_KEYSTORE_PATH");
        string keystorePass = Environment.GetEnvironmentVariable("ROATAN_KEYSTORE_PASS");
        string keyAlias = Environment.GetEnvironmentVariable("ROATAN_KEY_ALIAS");
        string keyPass = Environment.GetEnvironmentVariable("ROATAN_KEY_PASS");

        bool anyEnvVarSet = !string.IsNullOrWhiteSpace(keystorePath)
            || !string.IsNullOrWhiteSpace(keystorePass)
            || !string.IsNullOrWhiteSpace(keyAlias)
            || !string.IsNullOrWhiteSpace(keyPass);

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
            configuredFromEnv = true;
            Debug.Log($"[RoatanAndroidBuilder] Using Release Keystore from environment: {keystorePath} (alias: {keyAlias})");
            return true;
        }

        if (anyEnvVarSet)
        {
            Debug.LogWarning($"[RoatanAndroidBuilder] WARNING: Incomplete Release Keystore environment configuration (path exists: {!string.IsNullOrWhiteSpace(keystorePath) && File.Exists(keystorePath)}, alias set: {!string.IsNullOrWhiteSpace(keyAlias)}, passwords set: {!string.IsNullOrWhiteSpace(keystorePass) && !string.IsNullOrWhiteSpace(keyPass)}). Verify ROATAN_KEYSTORE_PATH, ROATAN_KEYSTORE_PASS, ROATAN_KEY_ALIAS, and ROATAN_KEY_PASS.");
        }

        // Preserve and validate any Release Keystore already configured in Unity Publishing Settings
        if (PlayerSettings.Android.useCustomKeystore)
        {
            string configuredKeystore = PlayerSettings.Android.keystoreName;
            bool keystoreFileExists = !string.IsNullOrWhiteSpace(configuredKeystore) && File.Exists(configuredKeystore);
            bool hasCredentials = keystoreFileExists
                && !string.IsNullOrWhiteSpace(PlayerSettings.Android.keystorePass)
                && !string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName)
                && !string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasPass);

            if (hasCredentials)
            {
                Debug.Log($"[RoatanAndroidBuilder] Using ProjectSettings Release Keystore: {configuredKeystore} (alias: {PlayerSettings.Android.keyaliasName})");
                return true;
            }

            Debug.LogWarning($"[RoatanAndroidBuilder] WARNING: Custom Release Keystore is enabled in Project Settings, but keystore file or passwords are missing (keystore='{configuredKeystore}', alias='{PlayerSettings.Android.keyaliasName}'). Enter credentials in Project Settings -> Player -> Publishing Settings or set ROATAN_KEYSTORE_* environment variables; refusing to silently switch to debug signing.");
            return false;
        }

        Debug.LogWarning("[RoatanAndroidBuilder] WARNING: Release Keystore credentials are missing! Configure Project Settings -> Player -> Publishing Settings (Custom Keystore) or set ROATAN_KEYSTORE_PATH, ROATAN_KEYSTORE_PASS, ROATAN_KEY_ALIAS, and ROATAN_KEY_PASS. Building without release signing will produce a debug-signed APK whose signature will mismatch existing release installs.");
        return false;
    }

    private static string GetGitShortCommitHash()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --short HEAD",
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process proc = Process.Start(psi))
            {
                if (proc == null) return "unknown";
                string output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(3000);
                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    return output;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RoatanAndroidBuilder] Could not resolve Git commit hash: {ex.Message}");
        }

        return "unknown";
    }
}
