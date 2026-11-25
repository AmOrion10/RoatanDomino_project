package com.roatandomino.game;

import android.accounts.AccountManager;
import android.app.Activity;
import android.app.Fragment;
import android.app.FragmentManager;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Bundle;
import android.os.CancellationSignal;
import android.os.IBinder;

import com.unity3d.player.UnityPlayer;

import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.util.concurrent.Executor;

/**
 * Native Android bridge for Google Account Chooser & Chrome Custom Tabs OAuth:
 * 1. When AndroidX Credential Manager + Google ID libraries are present, launches the native
 *    Google "Choose an account" bottom sheet and returns a verified Google ID token.
 * 2. Otherwise immediately opens Android's built-in AccountManager "Choose an account" sheet
 *    listing all Google accounts signed into the device (no email typing required), and returns
 *    the selected account email to launch Google OAuth (prompt=select_account&login_hint=...)
 *    inside a Chrome Custom Tab.
 * 3. Provides Chrome Custom Tab launching for both Google and Facebook OAuth redirects so users
 *    authenticate cleanly and return via roatandomino://auth.
 */
public class RoatanGoogleSignInHelper extends Fragment {
    private static final int REQUEST_CHOOSE_GOOGLE_ACCOUNT = 9422;
    private static final String FRAGMENT_TAG = "RoatanGoogleSignInFragment";
    private static final String EXTRA_CUSTOM_TABS_SESSION = "android.support.customtabs.extra.SESSION";
    private static final String EXTRA_CUSTOM_TABS_TOOLBAR_COLOR = "android.support.customtabs.extra.TOOLBAR_COLOR";
    private static final String EXTRA_CUSTOM_TABS_TITLE_VISIBILITY = "android.support.customtabs.extra.TITLE_VISIBILITY";

    private String serverClientId;
    private String targetGameObject;
    private String onIdTokenMethod;
    private String onAccountSelectedMethod;
    private String onFallbackBrowserMethod;
    private String onCancelOrErrorMethod;

    public static void startGoogleAccountChooser(
            final Activity activity,
            final String serverClientId,
            final String gameObject,
            final String onIdToken,
            final String onAccountSelected,
            final String onFallbackBrowser,
            final String onCancelOrError) {
        if (activity == null) {
            UnityPlayer.UnitySendMessage(gameObject, onFallbackBrowser, "ACTIVITY_UNAVAILABLE");
            return;
        }

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    FragmentManager fm = activity.getFragmentManager();
                    RoatanGoogleSignInHelper existing = (RoatanGoogleSignInHelper) fm.findFragmentByTag(FRAGMENT_TAG);
                    if (existing != null) {
                        fm.beginTransaction().remove(existing).commitAllowingStateLoss();
                    }

                    RoatanGoogleSignInHelper fragment = new RoatanGoogleSignInHelper();
                    fragment.serverClientId = serverClientId;
                    fragment.targetGameObject = gameObject;
                    fragment.onIdTokenMethod = onIdToken;
                    fragment.onAccountSelectedMethod = onAccountSelected;
                    fragment.onFallbackBrowserMethod = onFallbackBrowser;
                    fragment.onCancelOrErrorMethod = onCancelOrError;
                    fm.beginTransaction().add(fragment, FRAGMENT_TAG).commitAllowingStateLoss();
                } catch (Throwable ex) {
                    UnityPlayer.UnitySendMessage(gameObject, onFallbackBrowser, "FRAGMENT_ERROR:" + ex.getMessage());
                }
            }
        });
    }

    /**
     * Opens an OAuth authorization URL in a Chrome Custom Tab (if Chrome is installed)
     * or falls back seamlessly to the system browser.
     */
    public static boolean openCustomTab(final Activity activity, final String url) {
        if (activity == null || url == null || url.trim().isEmpty()) {
            return false;
        }

        try {
            activity.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    try {
                        Uri uri = Uri.parse(url);
                        Intent intent = new Intent(Intent.ACTION_VIEW, uri);

                        // Standard Chrome Custom Tabs protocol extras (no androidx.browser dependency required)
                        Bundle customTabExtras = new Bundle();
                        customTabExtras.putBinder(EXTRA_CUSTOM_TABS_SESSION, (IBinder) null);
                        intent.putExtras(customTabExtras);
                        intent.putExtra(EXTRA_CUSTOM_TABS_TOOLBAR_COLOR, 0xFF08202A);
                        intent.putExtra(EXTRA_CUSTOM_TABS_TITLE_VISIBILITY, 1);

                        PackageManager pm = activity.getPackageManager();
                        Intent chromeCheck = new Intent(intent);
                        chromeCheck.setPackage("com.android.chrome");
                        if (chromeCheck.resolveActivity(pm) != null) {
                            intent.setPackage("com.android.chrome");
                        }

                        activity.startActivity(intent);
                    } catch (Throwable ignored) {
                        try {
                            Intent fallback = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
                            activity.startActivity(fallback);
                        } catch (Throwable ignoredAgain) {
                        }
                    }
                }
            });
            return true;
        } catch (Throwable ex) {
            return false;
        }
    }

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // Tier 1: Try AndroidX Credential Manager + Google ID Token bottom sheet via reflection (if bundled)
        if (serverClientId != null && !serverClientId.trim().isEmpty() && tryCredentialManagerGoogleSignIn()) {
            return;
        }

        // Tier 2: Launch native Android AccountManager "Choose an account" sheet for Google accounts
        launchNativeAccountManagerChooser();
    }

    private boolean tryCredentialManagerGoogleSignIn() {
        final Activity activity = getActivity();
        if (activity == null) {
            return false;
        }

        try {
            Class<?> cmClass = Class.forName("androidx.credentials.CredentialManager");
            Class<?> googleIdBuilderClass = Class.forName("com.google.android.libraries.identity.googleid.GetGoogleIdOption$Builder");
            Class<?> credOptionClass = Class.forName("androidx.credentials.CredentialOption");
            Class<?> reqBuilderClass = Class.forName("androidx.credentials.GetCredentialRequest$Builder");
            Class<?> reqClass = Class.forName("androidx.credentials.GetCredentialRequest");
            Class<?> callbackClass = Class.forName("androidx.credentials.CredentialManagerCallback");

            Method createMethod = cmClass.getMethod("create", android.content.Context.class);
            Object credentialManager = createMethod.invoke(null, activity);

            Object googleIdBuilder = googleIdBuilderClass.getConstructor().newInstance();
            googleIdBuilderClass.getMethod("setFilterByAuthorizedAccounts", boolean.class).invoke(googleIdBuilder, false);
            googleIdBuilderClass.getMethod("setServerClientId", String.class).invoke(googleIdBuilder, serverClientId.trim());
            googleIdBuilderClass.getMethod("setAutoSelectEnabled", boolean.class).invoke(googleIdBuilder, false);
            Object googleIdOption = googleIdBuilderClass.getMethod("build").invoke(googleIdBuilder);

            Object reqBuilder = reqBuilderClass.getConstructor().newInstance();
            reqBuilderClass.getMethod("addCredentialOption", credOptionClass).invoke(reqBuilder, googleIdOption);
            Object request = reqBuilderClass.getMethod("build").invoke(reqBuilder);

            Executor uiExecutor = new Executor() {
                @Override
                public void execute(Runnable command) {
                    activity.runOnUiThread(command);
                }
            };

            Object callbackProxy = Proxy.newProxyInstance(
                    callbackClass.getClassLoader(),
                    new Class<?>[]{callbackClass},
                    new InvocationHandler() {
                        @Override
                        public Object invoke(Object proxy, Method method, Object[] args) {
                            String name = method.getName();
                            if ("onResult".equals(name) && args != null && args.length > 0 && args[0] != null) {
                                handleCredentialManagerSuccess(args[0]);
                            } else if ("onError".equals(name) && args != null && args.length > 0 && args[0] != null) {
                                handleCredentialManagerError(args[0]);
                            }
                            return null;
                        }
                    });

            Method getCredAsync = cmClass.getMethod(
                    "getCredentialAsync",
                    android.content.Context.class,
                    reqClass,
                    CancellationSignal.class,
                    Executor.class,
                    callbackClass);
            getCredAsync.invoke(credentialManager, activity, request, new CancellationSignal(), uiExecutor, callbackProxy);
            return true;
        } catch (Throwable unavailable) {
            return false;
        }
    }

    private void handleCredentialManagerSuccess(Object getCredentialResponse) {
        try {
            Object credential = getCredentialResponse.getClass().getMethod("getCredential").invoke(getCredentialResponse);
            Bundle data = (Bundle) credential.getClass().getMethod("getData").invoke(credential);
            Class<?> googleCredClass = Class.forName("com.google.android.libraries.identity.googleid.GoogleIdTokenCredential");
            Object googleCred = googleCredClass.getMethod("createFrom", Bundle.class).invoke(null, data);
            String idToken = (String) googleCredClass.getMethod("getIdToken").invoke(googleCred);

            if (idToken != null && !idToken.trim().isEmpty()) {
                sendMessage(onIdTokenMethod, idToken.trim());
                cleanupSelf();
                return;
            }
        } catch (Throwable ignored) {
        }

        // Fall back to Android's native account chooser if ID token extraction failed
        launchNativeAccountManagerChooser();
    }

    private void handleCredentialManagerError(Object errorObj) {
        String className = errorObj.getClass().getName();
        if (className.contains("CancellationException")) {
            sendMessage(onCancelOrErrorMethod, "CANCELLED");
            cleanupSelf();
            return;
        }

        // NoCredentialException or unsupported Play Services -> fall back to AccountManager chooser + browser OAuth
        launchNativeAccountManagerChooser();
    }

    private void launchNativeAccountManagerChooser() {
        Activity activity = getActivity();
        if (activity == null) {
            sendMessage(onFallbackBrowserMethod, "ACTIVITY_UNAVAILABLE");
            cleanupSelf();
            return;
        }

        try {
            Intent chooserIntent = AccountManager.newChooseAccountIntent(
                    null,
                    null,
                    new String[]{"com.google"},
                    "Choose your Google account for Roatan Domino",
                    null,
                    null,
                    null);

            if (chooserIntent != null && chooserIntent.resolveActivity(activity.getPackageManager()) != null) {
                startActivityForResult(chooserIntent, REQUEST_CHOOSE_GOOGLE_ACCOUNT);
            } else {
                sendMessage(onFallbackBrowserMethod, "NO_ACCOUNT_CHOOSER");
                cleanupSelf();
            }
        } catch (Throwable ex) {
            sendMessage(onFallbackBrowserMethod, "CHOOSER_ERROR:" + ex.getMessage());
            cleanupSelf();
        }
    }

    @Override
    public void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST_CHOOSE_GOOGLE_ACCOUNT) {
            return;
        }

        try {
            if (resultCode == Activity.RESULT_OK && data != null) {
                String accountEmail = data.getStringExtra(AccountManager.KEY_ACCOUNT_NAME);
                if (accountEmail != null && !accountEmail.trim().isEmpty()) {
                    sendMessage(onAccountSelectedMethod, accountEmail.trim());
                } else {
                    sendMessage(onFallbackBrowserMethod, "EMPTY_ACCOUNT_SELECTION");
                }
            } else if (resultCode == Activity.RESULT_CANCELED) {
                sendMessage(onCancelOrErrorMethod, "CANCELLED");
            } else {
                sendMessage(onFallbackBrowserMethod, "UNKNOWN_RESULT");
            }
        } catch (Throwable ex) {
            sendMessage(onFallbackBrowserMethod, "RESULT_ERROR:" + ex.getMessage());
        } finally {
            cleanupSelf();
        }
    }

    private void sendMessage(String methodName, String payload) {
        if (targetGameObject != null && methodName != null) {
            UnityPlayer.UnitySendMessage(targetGameObject, methodName, payload != null ? payload : "");
        }
    }

    private void cleanupSelf() {
        try {
            Activity act = getActivity();
            if (act != null && !act.isFinishing()) {
                act.getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
            }
        } catch (Throwable ignored) {
        }
    }
}
