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

public class SocialAuthService(
    IUserRepository userRepository,
    IClientManager clientManager,
    ITokenManager tokenManager,
    IChatManager chatManager,
    OAuthStateStore oauthStateStore,
    ILogger<SocialAuthService> logger)
{
    private static readonly Regex UsernamePattern = new(@"^[A-Za-z0-9_]{3,16}$", RegexOptions.Compiled);
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static string NormalizeProvider(string? provider)
    {
        if (string.Equals(provider, "Facebook", StringComparison.OrdinalIgnoreCase)) return "Facebook";
        if (string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase)) return "Google";
        return string.Empty;
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

    private static string? GetGoogleClientId()
    {
        return Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID");
    }

    public (bool Configured, string Provider, string State, string? AuthorizeUrl, string? Message) StartOAuthFlow(
        string provider,
        string publicBaseUrl,
        string? requestedState = null)
    {
        string normalized = NormalizeProvider(provider);
        if (string.IsNullOrEmpty(normalized))
        {
            return (false, provider, string.Empty, null, "Unsupported OAuth provider. Use Google or Facebook.");
        }

        if (!IsProviderConfigured(normalized))
        {
            return (false, normalized, string.Empty, null,
                $"{normalized} OAuth credentials are not configured on this server. Complete your profile directly in the app.");
        }

        OAuthPendingSession pending = oauthStateStore.CreatePending(normalized, requestedState);
        string cleanBase = publicBaseUrl.TrimEnd('/');
        string redirectUri = $"{cleanBase}/auth/oauth/{normalized.ToLowerInvariant()}/callback";

        string authorizeUrl;
        if (normalized == "Google")
        {
            string clientId = GetGoogleClientId()!;
            authorizeUrl =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&response_type=code" +
                "&scope=" + Uri.EscapeDataString("openid email profile") +
                $"&state={Uri.EscapeDataString(pending.State)}" +
                "&prompt=select_account";
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
                "&response_type=code";
        }

        return (true, normalized, pending.State, authorizeUrl, null);
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

            (string sub, string? email, string? name, string? pictureUrl) = normalized == "Google"
                ? await ExchangeGoogleCodeAsync(code, redirectUri, ct)
                : await ExchangeFacebookCodeAsync(code, redirectUri, ct);

            string? avatarBase64 = await TryDownloadAvatarThumbnailBase64Async(pictureUrl, ct);

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

        string? providerUserId = dto.ProviderUserId;
        string? email = dto.Email;
        string? displayName = dto.DisplayName;
        string? avatarUrl = dto.AvatarUrl;
        string? avatarBase64 = dto.AvatarBase64;

        // 1. Real OAuth browser/deep-link one-time code exchange
        if (!string.IsNullOrWhiteSpace(dto.OneTimeCode))
        {
            OAuthPendingSession? verified = oauthStateStore.RedeemOneTimeCode(dto.OneTimeCode);
            if (verified is null)
            {
                throw new WrongPasswordException("Your sign-in link has expired. Please sign in again.");
            }

            provider = verified.Provider;
            providerUserId = verified.ProviderUserId;
            email = verified.Email;
            displayName = verified.DisplayName;
            avatarUrl = verified.AvatarUrl;
            avatarBase64 = verified.AvatarBase64;
            return await userRepository.FindOrCreateSocialUser(provider, providerUserId ?? string.Empty, email, displayName, avatarUrl, avatarBase64);
        }

        // 2. Direct ID/Access Token verification when OAuth client ID is configured
        string? googleClientId = GetGoogleClientId();
        string? facebookAppId = Environment.GetEnvironmentVariable("FACEBOOK_APP_ID");

        if (provider == "Google" && !string.IsNullOrWhiteSpace(googleClientId))
        {
            if (string.IsNullOrWhiteSpace(dto.IdToken))
            {
                throw new WrongPasswordException("Please complete Google sign-in in your browser.");
            }

            (providerUserId, email, displayName, avatarUrl) = await VerifyGoogleTokenAsync(dto.IdToken, googleClientId, cancellationToken);
            avatarBase64 ??= await TryDownloadAvatarThumbnailBase64Async(avatarUrl, cancellationToken);
            return await userRepository.FindOrCreateSocialUser(provider, providerUserId, email, displayName, avatarUrl, avatarBase64);
        }

        if (provider == "Facebook" && !string.IsNullOrWhiteSpace(facebookAppId))
        {
            if (string.IsNullOrWhiteSpace(dto.IdToken))
            {
                throw new WrongPasswordException("Please complete Facebook sign-in in your browser.");
            }

            (providerUserId, email, displayName, avatarUrl) = await VerifyFacebookTokenAsync(dto.IdToken, facebookAppId, cancellationToken);
            avatarBase64 ??= await TryDownloadAvatarThumbnailBase64Async(avatarUrl, cancellationToken);
            return await userRepository.FindOrCreateSocialUser(provider, providerUserId, email, displayName, avatarUrl, avatarBase64);
        }

        // 3. Local/tunnel profile-completion mode (when OAuth client IDs are not configured on the server):
        // Never silently create generic "GooglePlayer" / "FacebookPlayer" accounts — require a real email & chosen username!
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidEmailFormatException("Please enter your email address to complete sign-in.");
        }

        User? existingByEmail = await userRepository.GetByEmail(email.Trim());
        if (existingByEmail is null)
        {
            if (string.IsNullOrWhiteSpace(displayName) || !UsernamePattern.IsMatch(displayName.Trim()))
            {
                throw new LoginTooLongException("Username must be 3–16 letters or numbers.");
            }
        }

        logger.LogInformation("Social auth ({provider}) completing user-supplied profile for {email} (OAuth env vars not configured).", provider, email);
        return await userRepository.FindOrCreateSocialUser(provider, string.Empty, email, displayName, avatarUrl, avatarBase64);
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

    private static async Task<(string Sub, string? Email, string? Name, string? PictureUrl)> ExchangeGoogleCodeAsync(string code, string redirectUri, CancellationToken ct)
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

        using HttpResponseMessage tokenResp = await SharedHttpClient.PostAsync("https://oauth2.googleapis.com/token", form, ct);
        if (!tokenResp.IsSuccessStatusCode)
        {
            throw new WrongPasswordException("Google authorization code exchange failed.");
        }

        using JsonDocument tokenDoc = await tokenResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            ?? throw new WrongPasswordException("Empty Google token response.");
        string accessToken = tokenDoc.RootElement.TryGetProperty("access_token", out JsonElement atEl) ? atEl.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new WrongPasswordException("Google did not return an access token.");
        }

        using HttpRequestMessage userReq = new(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
        userReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage userResp = await SharedHttpClient.SendAsync(userReq, ct);
        if (!userResp.IsSuccessStatusCode)
        {
            throw new WrongPasswordException("Failed to fetch Google user profile.");
        }

        using JsonDocument userDoc = await userResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            ?? throw new WrongPasswordException("Empty Google user profile response.");
        JsonElement root = userDoc.RootElement;

        string sub = root.TryGetProperty("sub", out JsonElement subEl) ? subEl.GetString() ?? string.Empty : string.Empty;
        string? email = root.TryGetProperty("email", out JsonElement emailEl) ? emailEl.GetString() : null;
        string? name = root.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
        string? picture = root.TryGetProperty("picture", out JsonElement picEl) ? picEl.GetString() : null;

        if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email))
        {
            throw new WrongPasswordException("Google profile did not include an email address.");
        }

        return (sub, email, name, picture);
    }

    private static async Task<(string Id, string? Email, string? Name, string? PictureUrl)> ExchangeFacebookCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        string appId = Environment.GetEnvironmentVariable("FACEBOOK_APP_ID") ?? string.Empty;
        string appSecret = Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET") ?? string.Empty;

        string tokenUrl =
            "https://graph.facebook.com/v19.0/oauth/access_token" +
            $"?client_id={Uri.EscapeDataString(appId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&client_secret={Uri.EscapeDataString(appSecret)}" +
            $"&code={Uri.EscapeDataString(code)}";

        using HttpResponseMessage tokenResp = await SharedHttpClient.GetAsync(tokenUrl, ct);
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

        return await VerifyFacebookTokenAsync(accessToken, appId, ct);
    }

    private static async Task<(string Sub, string? Email, string? Name, string? PictureUrl)> VerifyGoogleTokenAsync(string idToken, string expectedClientId, CancellationToken ct)
    {
        try
        {
            string url = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}";
            using HttpResponseMessage resp = await SharedHttpClient.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                throw new WrongPasswordException("Google sign-in token could not be verified.");
            }

            using JsonDocument doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                ?? throw new WrongPasswordException("Empty response from Google token verification.");

            JsonElement root = doc.RootElement;
            string aud = root.TryGetProperty("aud", out JsonElement audEl) ? audEl.GetString() ?? string.Empty : string.Empty;
            if (!string.Equals(aud, expectedClientId, StringComparison.Ordinal))
            {
                throw new WrongPasswordException("Google sign-in token audience mismatch.");
            }

            string sub = root.TryGetProperty("sub", out JsonElement subEl) ? subEl.GetString() ?? string.Empty : string.Empty;
            string? email = root.TryGetProperty("email", out JsonElement emailEl) ? emailEl.GetString() : null;
            string? name = root.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
            string? picture = root.TryGetProperty("picture", out JsonElement picEl) ? picEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(sub))
            {
                throw new WrongPasswordException("Google sign-in token is missing subject identifier.");
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

    private static async Task<(string Id, string? Email, string? Name, string? PictureUrl)> VerifyFacebookTokenAsync(string accessToken, string expectedAppId, CancellationToken ct)
    {
        try
        {
            string? appSecret = Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET");
            if (!string.IsNullOrWhiteSpace(appSecret))
            {
                string debugUrl = $"https://graph.facebook.com/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={Uri.EscapeDataString($"{expectedAppId}|{appSecret}")}";
                using HttpResponseMessage debugResp = await SharedHttpClient.GetAsync(debugUrl, ct);
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

            string meUrl = $"https://graph.facebook.com/me?fields=id,name,email,picture.type(normal)&access_token={Uri.EscapeDataString(accessToken)}";
            using HttpResponseMessage meResp = await SharedHttpClient.GetAsync(meUrl, ct);
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

    private static async Task<string?> TryDownloadAvatarThumbnailBase64Async(string? pictureUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pictureUrl)) return null;
        try
        {
            using HttpResponseMessage resp = await SharedHttpClient.GetAsync(pictureUrl, ct);
            if (!resp.IsSuccessStatusCode) return null;
            byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0 || bytes.Length > UserRepository.MaxAvatarBytes) return null;
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return null;
        }
    }
}
