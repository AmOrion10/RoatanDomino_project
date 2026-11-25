using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

using Application.Exceptions.Connection;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Interfaces.Repositories;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

using Infrastructure.Services.Repositories;

using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed record OAuthStartResult(
    bool Configured,
    bool IdTokenConfigured,
    string Provider,
    string? ClientId,
    string State,
    string? AuthorizeUrl,
    string? Message);

public class SocialAuthService
{
    private static readonly Regex GoogleSizeSuffixRegex = new(@"=s\d+(-c)?$", RegexOptions.Compiled);
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly IUserRepository userRepository;
    private readonly IClientManager clientManager;
    private readonly ITokenManager tokenManager;
    private readonly IChatManager chatManager;
    private readonly OAuthStateStore oauthStateStore;
    private readonly ILogger<SocialAuthService> logger;
    private readonly HttpClient httpClient;

    public SocialAuthService(
        IUserRepository userRepository,
        IClientManager clientManager,
        ITokenManager tokenManager,
        IChatManager chatManager,
        OAuthStateStore oauthStateStore,
        ILogger<SocialAuthService> logger)
        : this(userRepository, clientManager, tokenManager, chatManager, oauthStateStore, logger, SharedHttpClient)
    {
    }

    internal SocialAuthService(
        IUserRepository userRepository,
        IClientManager clientManager,
        ITokenManager tokenManager,
        IChatManager chatManager,
        OAuthStateStore oauthStateStore,
        ILogger<SocialAuthService> logger,
        HttpClient httpClient)
    {
        this.userRepository = userRepository;
        this.clientManager = clientManager;
        this.tokenManager = tokenManager;
        this.chatManager = chatManager;
        this.oauthStateStore = oauthStateStore;
        this.logger = logger;
        this.httpClient = httpClient;
    }

    public static string NormalizeProvider(string? provider)
    {
        if (string.Equals(provider, "Facebook", StringComparison.OrdinalIgnoreCase)) return "Facebook";
        if (string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase)) return "Google";
        return string.Empty;
    }

    public static bool IsGoogleIdTokenConfigured()
    {
        return !string.IsNullOrWhiteSpace(GetGoogleClientId());
    }

    public static bool IsProviderConfigured(string provider)
    {
        string normalized = NormalizeProvider(provider);
        if (normalized == "Google")
        {
            string? clientId = GetGoogleClientId();
            string? clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
            return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
        }
        if (normalized == "Facebook")
        {
            string? appId = Environment.GetEnvironmentVariable("FACEBOOK_APP_ID");
            string? appSecret = Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET");
            return !string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(appSecret);
        }
        return false;
    }

    public static string? GetGoogleClientId()
    {
        string? raw = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string first = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        return string.IsNullOrWhiteSpace(first) ? null : first;
    }

    public static IReadOnlyList<string> GetAllowedGoogleClientIds()
    {
        List<string> ids = [];
        string?[] sources =
        [
            Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID"),
            Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID"),
            Environment.GetEnvironmentVariable("GOOGLE_ANDROID_CLIENT_ID")
        ];
        foreach (string? src in sources)
        {
            if (string.IsNullOrWhiteSpace(src)) continue;
            foreach (string part in src.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!ids.Contains(part, StringComparer.Ordinal))
                {
                    ids.Add(part);
                }
            }
        }
        return ids;
    }

    public static string GetMissingConfigurationMessage(string provider)
    {
        string normalized = NormalizeProvider(provider);
        if (normalized == "Google")
        {
            if (string.IsNullOrWhiteSpace(GetGoogleClientId()))
            {
                return "Google Sign-In is not configured on this server yet — add GOOGLE_CLIENT_ID to .env";
            }
            return "Google Sign-In is not configured on this server yet — add GOOGLE_CLIENT_SECRET to .env";
        }
        if (normalized == "Facebook")
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FACEBOOK_APP_ID")))
            {
                return "Facebook Sign-In is not configured on this server yet — add FACEBOOK_APP_ID to .env";
            }
            return "Facebook Sign-In is not configured on this server yet — add FACEBOOK_APP_SECRET to .env";
        }
        return "Unsupported OAuth provider. Use Google or Facebook.";
    }

    public OAuthStartResult StartOAuthFlow(
        string provider,
        string publicBaseUrl,
        string? requestedState = null,
        string? loginHint = null,
        bool forceReauthenticate = true)
    {
        string normalized = NormalizeProvider(provider);
        if (string.IsNullOrEmpty(normalized))
        {
            return new OAuthStartResult(false, false, provider, null, string.Empty, null, "Unsupported OAuth provider. Use Google or Facebook.");
        }

        string? googleClientId = normalized == "Google" ? GetGoogleClientId() : null;
        bool idTokenReady = normalized == "Google" && !string.IsNullOrWhiteSpace(googleClientId);

        if (!IsProviderConfigured(normalized))
        {
            return new OAuthStartResult(
                Configured: false,
                IdTokenConfigured: idTokenReady,
                Provider: normalized,
                ClientId: googleClientId,
                State: string.Empty,
                AuthorizeUrl: null,
                Message: GetMissingConfigurationMessage(normalized));
        }

        OAuthPendingSession pending = oauthStateStore.CreatePending(normalized, requestedState);
        string cleanBase = publicBaseUrl.TrimEnd('/');
        string redirectUri = $"{cleanBase}/auth/oauth/{normalized.ToLowerInvariant()}/callback";

        string authorizeUrl;
        if (normalized == "Google")
        {
            string clientId = googleClientId!;
            authorizeUrl =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&response_type=code" +
                "&scope=" + Uri.EscapeDataString("openid email profile") +
                $"&state={Uri.EscapeDataString(pending.State)}" +
                "&prompt=select_account";

            if (!string.IsNullOrWhiteSpace(loginHint))
            {
                authorizeUrl += $"&login_hint={Uri.EscapeDataString(loginHint.Trim())}";
            }
        }
        else
        {
            string appId = Environment.GetEnvironmentVariable("FACEBOOK_APP_ID")!;
            authorizeUrl =
                "https://www.facebook.com/v19.0/dialog/oauth" +
                $"?client_id={Uri.EscapeDataString(appId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&scope=" + Uri.EscapeDataString("email,public_profile") +
                $"&state={Uri.EscapeDataString(pending.State)}" +
                "&response_type=code" +
                "&display=popup";

            if (forceReauthenticate)
            {
                authorizeUrl += "&auth_type=reauthenticate";
            }
        }

        return new OAuthStartResult(
            Configured: true,
            IdTokenConfigured: idTokenReady,
            Provider: normalized,
            ClientId: normalized == "Google" ? googleClientId : Environment.GetEnvironmentVariable("FACEBOOK_APP_ID"),
            State: pending.State,
            AuthorizeUrl: authorizeUrl,
            Message: null);
    }

    public async Task<(bool Ok, string OneTimeCode, string Provider, string? ErrorMessage)> CompleteOAuthCallbackAsync(
        string provider,
        string? code,
        string? state,
        string publicBaseUrl,
        CancellationToken ct = default)
    {
        string normalized = NormalizeProvider(provider);
        if (string.IsNullOrEmpty(normalized) || string.IsNullOrWhiteSpace(state))
        {
            return (false, string.Empty, normalized, "Missing OAuth provider or state.");
        }

        OAuthPendingSession? pending = oauthStateStore.GetByState(state);
        if (pending is null || !string.Equals(pending.Provider, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return (false, string.Empty, normalized, "OAuth sign-in session expired. Please try again.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            const string cancelMsg = "Sign-in was cancelled before completing.";
            oauthStateStore.MarkError(state, cancelMsg);
            return (false, string.Empty, normalized, cancelMsg);
        }

        try
        {
            string cleanBase = publicBaseUrl.TrimEnd('/');
            string redirectUri = $"{cleanBase}/auth/oauth/{normalized.ToLowerInvariant()}/callback";

            (string sub, string? email, string? name, string? pictureUrl, string? fallbackAccessToken) = normalized == "Google"
                ? await ExchangeGoogleCodeAsync(code, redirectUri, ct)
                : await ExchangeFacebookCodeAsync(code, redirectUri, ct);

            string? avatarBase64 = await TryDownloadAndCompressAvatarBase64Async(pictureUrl, normalized, sub, fallbackAccessToken, ct);

            if (!oauthStateStore.TryComplete(state, sub, email, name, pictureUrl, avatarBase64, out string oneTimeCode))
            {
                return (false, string.Empty, normalized, "OAuth sign-in session expired.");
            }

            return (true, oneTimeCode, normalized, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OAuth callback failed for {provider}", normalized);
            string msg = ex is WrongPasswordException ? ex.Message : $"{normalized} sign-in verification failed.";
            oauthStateStore.MarkError(state, msg);
            return (false, string.Empty, normalized, msg);
        }
    }

    public async Task<(User User, string OneTimeCode)> VerifyGoogleIdTokenAndCreateSessionAsync(
        string? idToken,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> allowedClientIds = GetAllowedGoogleClientIds();
        if (allowedClientIds.Count == 0)
        {
            throw new WrongPasswordException("Google Sign-In is not configured on this server yet — add GOOGLE_CLIENT_ID to .env");
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new WrongPasswordException("Missing Google ID token.");
        }

        (string sub, string? email, string? name, string? pictureUrl) = await VerifyGoogleTokenAsync(idToken.Trim(), allowedClientIds, cancellationToken);
        string? avatarBase64 = await TryDownloadAndCompressAvatarBase64Async(pictureUrl, "Google", sub, null, cancellationToken);

        User user = await userRepository.FindOrCreateSocialUser("Google", sub, email, name, pictureUrl, avatarBase64);

        OAuthPendingSession pending = oauthStateStore.CreatePending("Google");
        oauthStateStore.TryComplete(pending.State, sub, user.Email, user.Login, pictureUrl, avatarBase64 ?? user.AvatarBase64, out string oneTimeCode);
        return (user, oneTimeCode);
    }

    public OAuthPendingSession? PollOAuthSession(string state)
    {
        return oauthStateStore.GetByState(state);
    }

    public async Task<User> AuthenticateProfileAsync(SocialAuthDto dto, CancellationToken cancellationToken = default)
    {
        string provider = NormalizeProvider(dto.Provider);
        if (string.IsNullOrEmpty(provider))
        {
            throw new WrongPasswordException("Unsupported social sign-in provider. Please use Google or Facebook.");
        }

        // 1. Real OAuth browser/deep-link or native ID-token one-time code exchange
        if (!string.IsNullOrWhiteSpace(dto.OneTimeCode))
        {
            OAuthPendingSession? verified = oauthStateStore.RedeemOneTimeCode(dto.OneTimeCode);
            if (verified is null)
            {
                throw new WrongPasswordException("Your sign-in link has expired. Please sign in again.");
            }

            return await userRepository.FindOrCreateSocialUser(
                verified.Provider,
                verified.ProviderUserId ?? string.Empty,
                verified.Email,
                verified.DisplayName,
                verified.AvatarUrl,
                verified.AvatarBase64);
        }

        // 2. Direct ID/Access Token verification
        if (provider == "Google")
        {
            IReadOnlyList<string> allowedClientIds = GetAllowedGoogleClientIds();
            if (allowedClientIds.Count == 0)
            {
                throw new WrongPasswordException("Google Sign-In is not configured on this server yet — add GOOGLE_CLIENT_ID to .env");
            }

            if (string.IsNullOrWhiteSpace(dto.IdToken))
            {
                throw new WrongPasswordException("Please sign in by selecting your Google account.");
            }

            (string sub, string? email, string? name, string? pictureUrl) = await VerifyGoogleTokenAsync(dto.IdToken.Trim(), allowedClientIds, cancellationToken);
            string? avatarBase64 = dto.AvatarBase64 ?? await TryDownloadAndCompressAvatarBase64Async(pictureUrl, "Google", sub, null, cancellationToken);
            return await userRepository.FindOrCreateSocialUser("Google", sub, email, name, pictureUrl, avatarBase64);
        }

        // Facebook
        string? facebookAppId = Environment.GetEnvironmentVariable("FACEBOOK_APP_ID");
        if (string.IsNullOrWhiteSpace(facebookAppId))
        {
            throw new WrongPasswordException("Facebook Sign-In is not configured on this server yet — add FACEBOOK_APP_ID to .env");
        }

        if (string.IsNullOrWhiteSpace(dto.IdToken))
        {
            throw new WrongPasswordException("Please complete Facebook sign-in in your browser.");
        }

        (string fbId, string? fbEmail, string? fbName, string? fbPictureUrl) = await VerifyFacebookTokenAsync(dto.IdToken.Trim(), facebookAppId, cancellationToken);
        string? fbAvatarBase64 = dto.AvatarBase64 ?? await TryDownloadAndCompressAvatarBase64Async(fbPictureUrl, "Facebook", fbId, dto.IdToken.Trim(), cancellationToken);
        return await userRepository.FindOrCreateSocialUser("Facebook", fbId, fbEmail, fbName, fbPictureUrl, fbAvatarBase64);
    }

    public async Task<Tuple<User, Guid>> SignInClientAsync(SocialAuthDto dto, IClient client, CancellationToken cancellationToken = default)
    {
        if (client.IsLoggedIn)
        {
            throw new ClientAlreadyLoggedInException("You are already signed in.");
        }

        User user = await AuthenticateProfileAsync(dto, cancellationToken);

        foreach (IClient other in clientManager.GetAllClients())
        {
            if (other.Id != client.Id && other.IsLoggedIn && other.User is not null && other.User.Id == user.Id)
            {
                throw new ClientAlreadyLoggedInException("This account is already signed in on another device.");
            }
        }

        Guid token = tokenManager.GenerateTokenForClient(client);
        client.User = user;
        client.IsLoggedIn = true;
        chatManager.ClientJoinChat(client, chatManager.GlobalChatId);
        return new Tuple<User, Guid>(user, token);
    }

    private async Task<(string Sub, string? Email, string? Name, string? PictureUrl, string? AccessToken)> ExchangeGoogleCodeAsync(
        string code,
        string redirectUri,
        CancellationToken ct)
    {
        string clientId = GetGoogleClientId() ?? string.Empty;
        string clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? string.Empty;

        using FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        using HttpResponseMessage tokenResp = await httpClient.PostAsync("https://oauth2.googleapis.com/token", form, ct);
        if (!tokenResp.IsSuccessStatusCode)
        {
            throw new WrongPasswordException("Google authorization code exchange failed.");
        }

        using JsonDocument tokenDoc = await tokenResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            ?? throw new WrongPasswordException("Empty Google token response.");
        string accessToken = tokenDoc.RootElement.TryGetProperty("access_token", out JsonElement atEl) ? atEl.GetString() ?? string.Empty : string.Empty;
        string? idToken = tokenDoc.RootElement.TryGetProperty("id_token", out JsonElement idEl) ? idEl.GetString() : null;

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            using HttpRequestMessage userReq = new(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
            userReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage userResp = await httpClient.SendAsync(userReq, ct);
            if (userResp.IsSuccessStatusCode)
            {
                using JsonDocument userDoc = await userResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                    ?? throw new WrongPasswordException("Empty Google user profile response.");
                JsonElement root = userDoc.RootElement;

                string sub = root.TryGetProperty("sub", out JsonElement subEl) ? subEl.GetString() ?? string.Empty : string.Empty;
                string? email = root.TryGetProperty("email", out JsonElement emailEl) ? emailEl.GetString() : null;
                string? name = root.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
                string? picture = root.TryGetProperty("picture", out JsonElement picEl) ? picEl.GetString() : null;

                if (!string.IsNullOrWhiteSpace(sub) && !string.IsNullOrWhiteSpace(email))
                {
                    return (sub, email, name, picture, accessToken);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(idToken))
        {
            (string sub, string? email, string? name, string? picture) = await VerifyGoogleTokenAsync(idToken, GetAllowedGoogleClientIds(), ct);
            return (sub, email, name, picture, accessToken);
        }

        throw new WrongPasswordException("Google profile did not include an email address.");
    }

    private async Task<(string Id, string? Email, string? Name, string? PictureUrl, string? AccessToken)> ExchangeFacebookCodeAsync(
        string code,
        string redirectUri,
        CancellationToken ct)
    {
        string appId = Environment.GetEnvironmentVariable("FACEBOOK_APP_ID") ?? string.Empty;
        string appSecret = Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET") ?? string.Empty;

        string tokenUrl =
            "https://graph.facebook.com/v19.0/oauth/access_token" +
            $"?client_id={Uri.EscapeDataString(appId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&client_secret={Uri.EscapeDataString(appSecret)}" +
            $"&code={Uri.EscapeDataString(code)}";

        using HttpResponseMessage tokenResp = await httpClient.GetAsync(tokenUrl, ct);
        if (!tokenResp.IsSuccessStatusCode)
        {
            throw new WrongPasswordException("Facebook authorization code exchange failed.");
        }

        using JsonDocument tokenDoc = await tokenResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            ?? throw new WrongPasswordException("Empty Facebook token response.");
        string accessToken = tokenDoc.RootElement.TryGetProperty("access_token", out JsonElement atEl) ? atEl.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new WrongPasswordException("Facebook did not return an access token.");
        }

        (string id, string? email, string? name, string? picture) = await VerifyFacebookTokenAsync(accessToken, appId, ct);
        return (id, email, name, picture, accessToken);
    }

    private async Task<(string Sub, string? Email, string? Name, string? PictureUrl)> VerifyGoogleTokenAsync(
        string idToken,
        IReadOnlyList<string> allowedClientIds,
        CancellationToken ct)
    {
        try
        {
            string url = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}";
            using HttpResponseMessage resp = await httpClient.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                throw new WrongPasswordException("Google sign-in token could not be verified.");
            }

            using JsonDocument doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                ?? throw new WrongPasswordException("Empty response from Google token verification.");

            JsonElement root = doc.RootElement;
            string aud = root.TryGetProperty("aud", out JsonElement audEl) ? audEl.GetString() ?? string.Empty : string.Empty;
            if (!allowedClientIds.Any(id => string.Equals(aud, id, StringComparison.Ordinal)))
            {
                throw new WrongPasswordException("Google sign-in token audience mismatch.");
            }

            if (root.TryGetProperty("exp", out JsonElement expEl))
            {
                string? expStr = expEl.ValueKind == JsonValueKind.Number ? expEl.GetInt64().ToString() : expEl.GetString();
                if (long.TryParse(expStr, out long expUnix) && DateTimeOffset.FromUnixTimeSeconds(expUnix) < DateTimeOffset.UtcNow.AddMinutes(-1))
                {
                    throw new WrongPasswordException("Google sign-in token has expired. Please try again.");
                }
            }

            string sub = root.TryGetProperty("sub", out JsonElement subEl) ? subEl.GetString() ?? string.Empty : string.Empty;
            string? email = root.TryGetProperty("email", out JsonElement emailEl) ? emailEl.GetString() : null;
            string? name = root.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
            string? picture = root.TryGetProperty("picture", out JsonElement picEl) ? picEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(sub))
            {
                throw new WrongPasswordException("Google sign-in token is missing subject identifier.");
            }
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new WrongPasswordException("Google account did not include an email address.");
            }

            return (sub, email, name, picture);
        }
        catch (WrongPasswordException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new WrongPasswordException("Unable to reach Google sign-in verification service.");
        }
    }

    private async Task<(string Id, string? Email, string? Name, string? PictureUrl)> VerifyFacebookTokenAsync(
        string accessToken,
        string expectedAppId,
        CancellationToken ct)
    {
        try
        {
            string? appSecret = Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET");
            if (!string.IsNullOrWhiteSpace(appSecret))
            {
                string debugUrl = $"https://graph.facebook.com/v19.0/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={Uri.EscapeDataString($"{expectedAppId}|{appSecret}")}";
                using HttpResponseMessage debugResp = await httpClient.GetAsync(debugUrl, ct);
                if (!debugResp.IsSuccessStatusCode)
                {
                    throw new WrongPasswordException("Facebook sign-in token could not be verified.");
                }
                using JsonDocument debugDoc = await debugResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                    ?? throw new WrongPasswordException("Empty response from Facebook token verification.");
                if (!debugDoc.RootElement.TryGetProperty("data", out JsonElement dataEl) ||
                    !dataEl.TryGetProperty("is_valid", out JsonElement validEl) ||
                    !validEl.GetBoolean())
                {
                    throw new WrongPasswordException("Facebook sign-in token is not valid.");
                }
            }

            string meUrl = $"https://graph.facebook.com/v19.0/me?fields=id,name,email,picture.type(large)&access_token={Uri.EscapeDataString(accessToken)}";
            using HttpResponseMessage meResp = await httpClient.GetAsync(meUrl, ct);
            if (!meResp.IsSuccessStatusCode)
            {
                throw new WrongPasswordException("Facebook profile could not be loaded.");
            }

            using JsonDocument meDoc = await meResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                ?? throw new WrongPasswordException("Empty response from Facebook profile lookup.");
            JsonElement root = meDoc.RootElement;
            string id = root.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
            string? email = root.TryGetProperty("email", out JsonElement emailEl) ? emailEl.GetString() : null;
            string? name = root.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
            string? picture = null;
            if (root.TryGetProperty("picture", out JsonElement picObj) &&
                picObj.TryGetProperty("data", out JsonElement picData) &&
                picData.TryGetProperty("url", out JsonElement picUrl))
            {
                picture = picUrl.GetString();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new WrongPasswordException("Facebook profile is missing user ID.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                email = $"fb_{id}@facebook.roatandomino.local";
            }

            return (id, email, name, picture);
        }
        catch (WrongPasswordException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new WrongPasswordException("Unable to reach Facebook sign-in verification service.");
        }
    }

    private async Task<string?> TryDownloadAndCompressAvatarBase64Async(
        string? pictureUrl,
        string provider,
        string providerUserId,
        string? accessToken,
        CancellationToken ct)
    {
        List<string> candidateUrls = [];

        if (!string.IsNullOrWhiteSpace(pictureUrl))
        {
            if (provider == "Google")
            {
                candidateUrls.Add(NormalizeGoogleAvatarUrl(pictureUrl, 128));
                candidateUrls.Add(NormalizeGoogleAvatarUrl(pictureUrl, 96));
                if (!candidateUrls.Contains(pictureUrl, StringComparer.Ordinal))
                {
                    candidateUrls.Add(pictureUrl);
                }
            }
            else
            {
                candidateUrls.Add(pictureUrl);
                if (!string.IsNullOrWhiteSpace(providerUserId) && !string.IsNullOrWhiteSpace(accessToken))
                {
                    candidateUrls.Add($"https://graph.facebook.com/v19.0/{Uri.EscapeDataString(providerUserId)}/picture?width=128&height=128&redirect=true&access_token={Uri.EscapeDataString(accessToken)}");
                }
            }
        }

        foreach (string url in candidateUrls.Distinct(StringComparer.Ordinal))
        {
            try
            {
                using HttpResponseMessage resp = await httpClient.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) continue;
                byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length == 0) continue;

                if (bytes.Length > UserRepository.MaxAvatarBytes)
                {
                    bytes = StripJpegMetadata(bytes);
                }

                if (bytes.Length > 0 && bytes.Length <= UserRepository.MaxAvatarBytes)
                {
                    return Convert.ToBase64String(bytes);
                }
            }
            catch
            {
                // Try next smaller avatar candidate
            }
        }

        return null;
    }

    internal static string NormalizeGoogleAvatarUrl(string pictureUrl, int sizePx)
    {
        if (string.IsNullOrWhiteSpace(pictureUrl)) return string.Empty;
        if (!pictureUrl.Contains("googleusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            return pictureUrl;
        }

        if (GoogleSizeSuffixRegex.IsMatch(pictureUrl))
        {
            return GoogleSizeSuffixRegex.Replace(pictureUrl, $"=s{sizePx}-c");
        }

        return $"{pictureUrl}=s{sizePx}-c";
    }

    internal static byte[] StripJpegMetadata(byte[] raw)
    {
        // Only operate on valid JPEG SOI (0xFF 0xD8)
        if (raw.Length < 4 || raw[0] != 0xFF || raw[1] != 0xD8)
        {
            return raw;
        }

        using MemoryStream output = new(raw.Length);
        output.WriteByte(0xFF);
        output.WriteByte(0xD8);

        int idx = 2;
        while (idx + 1 < raw.Length)
        {
            if (raw[idx] != 0xFF)
            {
                return raw;
            }

            while (idx < raw.Length && raw[idx] == 0xFF)
            {
                idx++;
            }

            if (idx >= raw.Length) break;
            byte marker = raw[idx++];

            // Start of Scan (SOS) or End of Image (EOI): copy remaining image entropy stream untouched
            if (marker == 0xDA || marker == 0xD9)
            {
                output.WriteByte(0xFF);
                output.WriteByte(marker);
                output.Write(raw, idx, raw.Length - idx);
                return output.ToArray();
            }

            // Standalone markers without length
            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                output.WriteByte(0xFF);
                output.WriteByte(marker);
                continue;
            }

            if (idx + 1 >= raw.Length) break;
            int segLen = (raw[idx] << 8) | raw[idx + 1];
            if (segLen < 2 || idx + segLen > raw.Length)
            {
                return raw;
            }

            // Strip APP1..APP15 (EXIF/XMP/ICC profiles) and COM (0xFE) comments; keep APP0 (JFIF) and all DQT/DHT/SOF markers
            bool isRemovableMetadata = (marker >= 0xE1 && marker <= 0xEF) || marker == 0xFE;
            if (!isRemovableMetadata)
            {
                output.WriteByte(0xFF);
                output.WriteByte(marker);
                output.Write(raw, idx, segLen);
            }

            idx += segLen;
        }

        return output.ToArray();
    }
}
