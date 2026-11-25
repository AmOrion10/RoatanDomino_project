using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Cross-platform profile avatar utility:
/// - Picks images from Android gallery (via Intent.ACTION_PICK / ACTION_GET_CONTENT) or Unity Editor file panel
/// - Downloads & caches Google/Facebook social profile photos
/// - Center-crops & downscales images to 128x128 JPEG (<= 32 KB) Base64 for fast WebSocket transfer
/// - Renders and caches anti-aliased circular avatar sprites for Profile, Hub, Table Seats, and Players Roster
/// </summary>
public class RoatanAndroidPhotoPicker : MonoBehaviour
{
    public const int TargetAvatarSize = 128;
    public const int MaxAvatarBytes = 32 * 1024;

    private static RoatanAndroidPhotoPicker instance;
    private static readonly Dictionary<string, Sprite> CircularSpriteCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Texture2D> CircularTextureCache = new Dictionary<string, Texture2D>();

    private Action<string> pendingSuccess;
    private Action<string> pendingError;

    private static RoatanAndroidPhotoPicker EnsureInstance()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("RoatanAndroidPhotoPicker");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RoatanAndroidPhotoPicker>();
        }
        return instance;
    }

    public static void PickPhoto(Action<string> onBase64Ready, Action<string> onError)
    {
        RoatanAndroidPhotoPicker picker = EnsureInstance();
        picker.pendingSuccess = onBase64Ready;
        picker.pendingError = onError;

#if UNITY_EDITOR
        try
        {
            string path = UnityEditor.EditorUtility.OpenFilePanel("Choose Profile Photo", "", "png,jpg,jpeg");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            byte[] raw = File.ReadAllBytes(path);
            if (TryResizeAndCompressToBase64(raw, out string base64, out string err))
            {
                picker.pendingSuccess?.Invoke(base64);
            }
            else
            {
                picker.pendingError?.Invoke(err);
            }
        }
        catch (Exception ex)
        {
            picker.pendingError?.Invoke($"Could not load photo: {ex.Message}");
        }
#elif UNITY_ANDROID
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass helper = new AndroidJavaClass("com.roatandomino.game.RoatanPhotoPickerHelper"))
            {
                helper.CallStatic(
                    "pickImage",
                    currentActivity,
                    picker.gameObject.name,
                    nameof(OnNativePhotoPicked),
                    nameof(OnNativePhotoError));
            }
        }
        catch (Exception ex)
        {
            picker.pendingError?.Invoke($"Could not open Android gallery: {ex.Message}");
        }
#else
        picker.pendingError?.Invoke("Photo upload is supported on Android devices and Unity Editor.");
#endif
    }

    // Invoked by com.roatandomino.game.RoatanPhotoPickerHelper via UnityPlayer.UnitySendMessage
    public void OnNativePhotoPicked(string rawBase64)
    {
        if (string.IsNullOrWhiteSpace(rawBase64))
        {
            pendingError?.Invoke("No photo was selected.");
            return;
        }

        try
        {
            byte[] raw = Convert.FromBase64String(rawBase64);
            if (TryResizeAndCompressToBase64(raw, out string compressedBase64, out string err))
            {
                pendingSuccess?.Invoke(compressedBase64);
            }
            else
            {
                pendingError?.Invoke(err);
            }
        }
        catch (Exception ex)
        {
            pendingError?.Invoke($"Failed to process selected photo: {ex.Message}");
        }
    }

    // Invoked by com.roatandomino.game.RoatanPhotoPickerHelper via UnityPlayer.UnitySendMessage
    public void OnNativePhotoError(string errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            pendingError?.Invoke(errorMessage);
        }
    }

    public static IEnumerator DownloadAndCompressUrlCoroutine(string url, Action<string> onBase64Ready, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            onError?.Invoke("No social profile picture URL is available.");
            yield break;
        }

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            req.timeout = 12;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Could not download social photo: {req.error}");
                yield break;
            }

            Texture2D downloaded = DownloadHandlerTexture.GetContent(req);
            if (downloaded == null)
            {
                onError?.Invoke("Downloaded social photo was empty.");
                yield break;
            }

            try
            {
                if (TryCompressTextureToBase64(downloaded, out string base64, out string err))
                {
                    onBase64Ready?.Invoke(base64);
                }
                else
                {
                    onError?.Invoke(err);
                }
            }
            finally
            {
                Destroy(downloaded);
            }
        }
    }

    public static bool TryResizeAndCompressToBase64(byte[] rawBytes, out string base64, out string error)
    {
        base64 = string.Empty;
        error = string.Empty;

        if (rawBytes == null || rawBytes.Length == 0)
        {
            error = "Selected image file is empty.";
            return false;
        }

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!source.LoadImage(rawBytes))
            {
                error = "Unsupported image format. Please pick a PNG or JPEG photo.";
                return false;
            }

            return TryCompressTextureToBase64(source, out base64, out error);
        }
        finally
        {
            Destroy(source);
        }
    }

    public static bool TryCompressTextureToBase64(Texture2D source, out string base64, out string error)
    {
        base64 = string.Empty;
        error = string.Empty;

        if (source == null || source.width <= 0 || source.height <= 0)
        {
            error = "Invalid image dimensions.";
            return false;
        }

        Texture2D square = CenterCropAndResize(source, TargetAvatarSize);
        try
        {
            int[] qualities = { 80, 68, 56, 44, 32, 22 };
            byte[] best = null;
            foreach (int q in qualities)
            {
                byte[] encoded = square.EncodeToJPG(q);
                if (encoded != null && encoded.Length > 0)
                {
                    best = encoded;
                    if (encoded.Length <= MaxAvatarBytes)
                    {
                        base64 = Convert.ToBase64String(encoded);
                        return true;
                    }
                }
            }

            if (best != null && best.Length <= MaxAvatarBytes)
            {
                base64 = Convert.ToBase64String(best);
                return true;
            }

            error = "Could not compress image under 32 KB.";
            return false;
        }
        finally
        {
            Destroy(square);
        }
    }

    private static Texture2D CenterCropAndResize(Texture2D source, int targetSize)
    {
        // Use GPU RenderTexture blit so even non-readable textures work reliably
        int srcW = source.width;
        int srcH = source.height;
        int cropSide = Mathf.Min(srcW, srcH);

        RenderTexture rt = RenderTexture.GetTemporary(targetSize, targetSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture prev = RenderTexture.active;
        try
        {
            float scaleX = (float)cropSide / srcW;
            float scaleY = (float)cropSide / srcH;
            Vector2 scale = new Vector2(scaleX, scaleY);
            Vector2 offset = new Vector2((1f - scaleX) * 0.5f, (1f - scaleY) * 0.5f);

            Graphics.Blit(source, rt, scale, offset);
            RenderTexture.active = rt;

            Texture2D result = new Texture2D(targetSize, targetSize, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, targetSize, targetSize), 0, 0);
            result.Apply(false, false);
            return result;
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    public static bool HasValidBase64(string base64)
    {
        return !string.IsNullOrWhiteSpace(base64) && base64.Length >= 32;
    }

    public static string GetInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "?";
        string cleaned = displayName.Trim();
        string[] parts = cleaned.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpperInvariant();
        }
        return cleaned.Length >= 2
            ? cleaned.Substring(0, 2).ToUpperInvariant()
            : cleaned.Substring(0, 1).ToUpperInvariant();
    }

    public static Color GetFallbackAvatarColor(string seedText)
    {
        Color[] palette =
        {
            new Color(0.11f, 0.56f, 0.58f, 1f),
            new Color(0.18f, 0.54f, 0.34f, 1f),
            new Color(0.74f, 0.48f, 0.14f, 1f),
            new Color(0.24f, 0.42f, 0.68f, 1f),
            new Color(0.62f, 0.28f, 0.44f, 1f),
            new Color(0.54f, 0.36f, 0.18f, 1f)
        };

        if (string.IsNullOrWhiteSpace(seedText)) return palette[0];
        int hash = 17;
        for (int i = 0; i < seedText.Length; i++)
        {
            hash = unchecked(hash * 31 + char.ToLowerInvariant(seedText[i]));
        }
        return palette[Mathf.Abs(hash) % palette.Length];
    }

    public static Sprite GetOrCreateCircularAvatarSprite(string cacheKey, string base64, Color fallbackBg, out bool hasPhoto)
    {
        hasPhoto = false;
        string effectiveKey = !string.IsNullOrWhiteSpace(base64)
            ? $"photo_{cacheKey}_{base64.Length}_{base64.GetHashCode()}"
            : $"disc_{ColorUtility.ToHtmlStringRGBA(fallbackBg)}";

        if (CircularSpriteCache.TryGetValue(effectiveKey, out Sprite cached) && cached != null)
        {
            hasPhoto = !string.IsNullOrWhiteSpace(base64);
            return cached;
        }

        Texture2D tex = null;
        if (!string.IsNullOrWhiteSpace(base64))
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (decoded.LoadImage(bytes))
                {
                    Texture2D square = CenterCropAndResize(decoded, TargetAvatarSize);
                    Destroy(decoded);
                    tex = ApplyCircularMask(square, new Color(0.97f, 0.79f, 0.28f, 1f));
                    Destroy(square);
                    hasPhoto = true;
                }
                else
                {
                    Destroy(decoded);
                }
            }
            catch
            {
                tex = null;
            }
        }

        if (tex == null)
        {
            tex = CreateCircularSolidTexture(TargetAvatarSize, fallbackBg, new Color(0.97f, 0.79f, 0.28f, 0.85f));
            hasPhoto = false;
        }

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        CircularTextureCache[effectiveKey] = tex;
        CircularSpriteCache[effectiveKey] = sprite;
        return sprite;
    }

    public static void ClearCache()
    {
        foreach (Sprite s in CircularSpriteCache.Values)
        {
            if (s != null) Destroy(s);
        }
        foreach (Texture2D t in CircularTextureCache.Values)
        {
            if (t != null) Destroy(t);
        }
        CircularSpriteCache.Clear();
        CircularTextureCache.Clear();
    }

    private static Texture2D ApplyCircularMask(Texture2D squareRgb, Color borderColor)
    {
        int size = squareRgb.width;
        Texture2D masked = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] src = squareRgb.GetPixels();
        Color[] dst = new Color[src.Length];

        float half = (size - 1) * 0.5f;
        float outerR = half;
        float borderInnerR = half - 3.0f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                int idx = y * size + x;

                if (dist > outerR)
                {
                    dst[idx] = Color.clear;
                }
                else if (dist > borderInnerR)
                {
                    float edgeAlpha = Mathf.Clamp01(outerR - dist);
                    dst[idx] = new Color(borderColor.r, borderColor.g, borderColor.b, borderColor.a * edgeAlpha);
                }
                else
                {
                    Color p = src[idx];
                    p.a = 1f;
                    dst[idx] = p;
                }
            }
        }

        masked.SetPixels(dst);
        masked.Apply(false, true);
        return masked;
    }

    private static Texture2D CreateCircularSolidTexture(int size, Color fillColor, Color borderColor)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] dst = new Color[size * size];

        float half = (size - 1) * 0.5f;
        float outerR = half;
        float borderInnerR = half - 3.0f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                int idx = y * size + x;

                if (dist > outerR)
                {
                    dst[idx] = Color.clear;
                }
                else if (dist > borderInnerR)
                {
                    float edgeAlpha = Mathf.Clamp01(outerR - dist);
                    dst[idx] = new Color(borderColor.r, borderColor.g, borderColor.b, borderColor.a * edgeAlpha);
                }
                else
                {
                    dst[idx] = fillColor;
                }
            }
        }

        tex.SetPixels(dst);
        tex.Apply(false, true);
        return tex;
    }
}
