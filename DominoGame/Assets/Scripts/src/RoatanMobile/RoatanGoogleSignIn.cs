using System;

using UnityEngine;

/// <summary>
/// Unity C# bridge for com.roatandomino.game.RoatanGoogleSignInHelper:
/// - Opens the native Android "Choose an account" sheet (Credential Manager when bundled,
///   or Android AccountManager Google account chooser) without manual email typing.
/// - Launches Chrome Custom Tabs on Android (or system browser on Editor/Desktop/iOS)
///   for Google and Facebook OAuth redirects returning via roatandomino://auth.
/// </summary>
public class RoatanGoogleSignIn : MonoBehaviour
{
    private static RoatanGoogleSignIn instance;

    private Action<string> pendingOnIdToken;
    private Action<string> pendingOnAccountEmailSelected;
    private Action pendingOnFallbackBrowser;
    private Action<string> pendingOnCancelOrError;

    public static bool IsNativeAndroidChooserAvailable =>
        Application.platform == RuntimePlatform.Android && !Application.isEditor;

    private static RoatanGoogleSignIn EnsureInstance()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("RoatanGoogleSignIn");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RoatanGoogleSignIn>();
        }
        return instance;
    }

    public static void LaunchGoogleAccountChooser(
        string serverClientId,
        Action<string> onIdToken,
        Action<string> onAccountEmailSelected,
        Action onFallbackBrowser,
        Action<string> onCancelOrError)
    {
        RoatanGoogleSignIn bridge = EnsureInstance();
        bridge.pendingOnIdToken = onIdToken;
        bridge.pendingOnAccountEmailSelected = onAccountEmailSelected;
        bridge.pendingOnFallbackBrowser = onFallbackBrowser;
        bridge.pendingOnCancelOrError = onCancelOrError;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass helper = new AndroidJavaClass("com.roatandomino.game.RoatanGoogleSignInHelper"))
            {
                helper.CallStatic(
                    "startGoogleAccountChooser",
                    currentActivity,
                    serverClientId ?? string.Empty,
                    bridge.gameObject.name,
                    nameof(OnGoogleIdTokenReceived),
                    nameof(OnGoogleAccountEmailSelected),
                    nameof(OnGoogleChooserFallback),
                    nameof(OnGoogleChooserCancelOrError));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RoatanGoogleSignIn] Native chooser unavailable, falling back to browser OAuth: {ex.Message}");
            bridge.InvokeFallbackBrowser();
        }
#else
        bridge.InvokeFallbackBrowser();
#endif
    }

    public static void OpenAuthBrowser(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass helper = new AndroidJavaClass("com.roatandomino.game.RoatanGoogleSignInHelper"))
            {
                bool launched = helper.CallStatic<bool>("openCustomTab", currentActivity, url);
                if (launched)
                {
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RoatanGoogleSignIn] Chrome Custom Tab failed, opening default browser: {ex.Message}");
        }
#endif

        Application.OpenURL(url);
    }

    // Invoked by com.roatandomino.game.RoatanGoogleSignInHelper via UnityPlayer.UnitySendMessage
    public void OnGoogleIdTokenReceived(string idToken)
    {
        Action<string> cb = pendingOnIdToken;
        ClearCallbacks();
        if (!string.IsNullOrWhiteSpace(idToken))
        {
            cb?.Invoke(idToken.Trim());
        }
    }

    // Invoked by com.roatandomino.game.RoatanGoogleSignInHelper via UnityPlayer.UnitySendMessage
    public void OnGoogleAccountEmailSelected(string accountEmail)
    {
        Action<string> cb = pendingOnAccountEmailSelected;
        ClearCallbacks();
        if (!string.IsNullOrWhiteSpace(accountEmail))
        {
            cb?.Invoke(accountEmail.Trim());
        }
    }

    // Invoked by com.roatandomino.game.RoatanGoogleSignInHelper via UnityPlayer.UnitySendMessage
    public void OnGoogleChooserFallback(string reason)
    {
        InvokeFallbackBrowser();
    }

    // Invoked by com.roatandomino.game.RoatanGoogleSignInHelper via UnityPlayer.UnitySendMessage
    public void OnGoogleChooserCancelOrError(string message)
    {
        Action<string> cb = pendingOnCancelOrError;
        ClearCallbacks();
        cb?.Invoke(string.IsNullOrWhiteSpace(message) ? "CANCELLED" : message.Trim());
    }

    private void InvokeFallbackBrowser()
    {
        Action cb = pendingOnFallbackBrowser;
        ClearCallbacks();
        cb?.Invoke();
    }

    private void ClearCallbacks()
    {
        pendingOnIdToken = null;
        pendingOnAccountEmailSelected = null;
        pendingOnFallbackBrowser = null;
        pendingOnCancelOrError = null;
    }
}
