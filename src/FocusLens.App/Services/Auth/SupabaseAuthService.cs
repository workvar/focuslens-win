using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusLens.Core.Ai;

namespace FocusLens.App.Services.Auth;

/// <summary>
/// Google sign-in through Supabase using the PKCE flow with a loopback redirect. Only identity and
/// the profile row are handled here; activity data never goes through Supabase.
/// Register http://127.0.0.1/callback (any port) as an allowed redirect URL in the Supabase project.
/// </summary>
public sealed partial class SupabaseAuthService : ObservableObject, IDisposable
{
    private const string AccessTokenKey = "supabase.accessToken";
    private const string RefreshTokenKey = "supabase.refreshToken";

    private readonly AppSettings _settings;
    private readonly ISecretStore _secrets;
    private readonly HttpClient _http = new();

    [ObservableProperty] private UserProfile? _currentUser;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _authError;

    public bool IsConfigured => _settings.IsAuthConfigured;
    public bool IsSignedIn => CurrentUser is not null;

    public SupabaseAuthService(AppSettings settings, ISecretStore secrets)
    {
        _settings = settings;
        _secrets = secrets;
    }

    private string BaseUrl => _settings.ResolvedSupabaseUrl!.TrimEnd('/');
    private string AnonKey => _settings.ResolvedSupabaseAnonKey!;

    /// <summary>Restores a saved session (refreshing it if needed) on app start.</summary>
    public async Task RestoreAsync()
    {
        if (!IsConfigured) return;
        var refresh = _secrets.Get(RefreshTokenKey);
        if (string.IsNullOrEmpty(refresh)) return;
        try
        {
            var session = await RequestTokenAsync("refresh_token", new { refresh_token = refresh });
            await CompleteSignInAsync(session);
        }
        catch
        {
            _secrets.Set(AccessTokenKey, null);
            _secrets.Set(RefreshTokenKey, null);
        }
    }

    public async Task SignInWithGoogleAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            AuthError = "Sign-in is not configured. Set FOCUSLENS_SUPABASE_URL and FOCUSLENS_SUPABASE_ANON_KEY.";
            return;
        }

        IsLoading = true;
        AuthError = null;
        try
        {
            var (verifier, challenge) = PkceFlow.CreatePair();
            var (listener, redirect) = PkceFlow.StartListener();

            var authorize = $"{BaseUrl}/auth/v1/authorize?provider=google" +
                            $"&redirect_to={Uri.EscapeDataString(redirect)}" +
                            $"&code_challenge={challenge}&code_challenge_method=s256";
            Process.Start(new ProcessStartInfo(authorize) { UseShellExecute = true });

            var query = await PkceFlow.WaitForCallbackAsync(listener, TimeSpan.FromMinutes(3), ct);
            if (!query.TryGetValue("code", out var code))
                throw new InvalidOperationException(query.GetValueOrDefault("error_description") ?? "No authorization code returned.");

            var session = await RequestTokenAsync("pkce", new { auth_code = code, code_verifier = verifier });
            await CompleteSignInAsync(session);
        }
        catch (Exception ex)
        {
            AuthError = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SignOut()
    {
        _secrets.Set(AccessTokenKey, null);
        _secrets.Set(RefreshTokenKey, null);
        CurrentUser = null;
        OnPropertyChanged(nameof(IsSignedIn));
    }

    public void Dispose() => _http.Dispose();

    private sealed record Session(string AccessToken, string RefreshToken, string UserId, string Email, string? FullName);

    private async Task<Session> RequestTokenAsync(string grantType, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/auth/v1/token?grant_type={grantType}")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("apikey", AnonKey);
        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;
        var user = root.GetProperty("user");
        string? fullName = null;
        if (user.TryGetProperty("user_metadata", out var meta) && meta.TryGetProperty("full_name", out var name))
            fullName = name.GetString();

        return new Session(
            root.GetProperty("access_token").GetString()!,
            root.GetProperty("refresh_token").GetString()!,
            user.GetProperty("id").GetString()!,
            user.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "",
            fullName);
    }

    private async Task CompleteSignInAsync(Session session)
    {
        _secrets.Set(AccessTokenKey, session.AccessToken);
        _secrets.Set(RefreshTokenKey, session.RefreshToken);
        CurrentUser = new UserProfile(session.UserId, session.Email, session.FullName);
        OnPropertyChanged(nameof(IsSignedIn));
        await EnsureProfileRowAsync(session);
    }

    /// <summary>Creates the user_profiles row on first sign-in; failures are non-fatal.</summary>
    private async Task EnsureProfileRowAsync(Session session)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/rest/v1/user_profiles")
            {
                Content = JsonContent.Create(new
                {
                    id = session.UserId,
                    email = session.Email,
                    display_name = session.FullName,
                }),
            };
            request.Headers.Add("apikey", AnonKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            request.Headers.Add("Prefer", "resolution=ignore-duplicates,return=minimal");
            await _http.SendAsync(request);
        }
        catch
        {
            // Profile sync is best effort.
        }
    }
}
