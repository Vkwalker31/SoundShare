// Typed access to the SoundShare REST API: bearer token, one silent refresh on 401, ProblemDetails to ApiException.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace SoundShare.Client.Services;

public sealed class ApiException : Exception
{
    public ApiException()
    {
    }

    public ApiException(string message)
        : base(message)
    {
    }

    public ApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ApiException(HttpStatusCode status, string message)
        : base(message)
    {
        Status = status;
    }

    public HttpStatusCode Status { get; }
}

public sealed class ApiClient(HttpClient http, AuthSession session, IJSRuntime js)
{
    private const string SessionFlag = "soundshare.session";

    public async Task LoginAsync(string username, string password)
    {
        using var response = await http.PostAsJsonAsync("api/auth/login", new { username, password });
        await EnsureSuccessAsync(response);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        session.SignIn(token!.AccessToken);
        await SetSessionFlagAsync(true);
    }

    public async Task RegisterAsync(string username, string email, string password)
    {
        using var response = await http.PostAsJsonAsync("api/auth/register", new { username, email, password });
        await EnsureSuccessAsync(response);
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        if (!await HasSessionFlagAsync())
        {
            return false;
        }

        try
        {
            using var response = await http.PostAsync("api/auth/refresh", null);
            if (!response.IsSuccessStatusCode)
            {
                await SetSessionFlagAsync(false);
                return false;
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
            session.SignIn(token!.AccessToken);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            using var response = await http.PostAsync("api/auth/logout", null);
        }
        finally
        {
            session.SignOut();
            await SetSessionFlagAsync(false);
        }
    }

    private async Task<bool> HasSessionFlagAsync()
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", SessionFlag) == "1";
        }
        catch (JSException)
        {
            return true;
        }
    }

    private async Task SetSessionFlagAsync(bool value)
    {
        try
        {
            if (value)
            {
                await js.InvokeVoidAsync("localStorage.setItem", SessionFlag, "1");
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.removeItem", SessionFlag);
            }
        }
        catch (JSException)
        {
        }
    }

    public Task<T> GetAsync<T>(string url) => SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Get, url));

    public Task<T> PostAsync<T>(string url, object? body = null) => SendAsync<T>(() => Json(HttpMethod.Post, url, body));

    public Task<T> PutAsync<T>(string url, object? body = null) => SendAsync<T>(() => Json(HttpMethod.Put, url, body));

    public Task SendAsync(HttpMethod method, string url, object? body = null) => SendAsync<object?>(() => Json(method, url, body), expectBody: false);

    private static HttpRequestMessage Json(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task<T> SendAsync<T>(Func<HttpRequestMessage> createRequest, bool expectBody = true)
    {
        var response = await SendWithTokenAsync(createRequest);
        if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRestoreSessionAsync())
        {
            response.Dispose();
            response = await SendWithTokenAsync(createRequest);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                session.SignOut();
            }

            await EnsureSuccessAsync(response);
            if (!expectBody || response.StatusCode == HttpStatusCode.NoContent)
            {
                return default!;
            }

            return (await response.Content.ReadFromJsonAsync<T>())!;
        }
    }

    private async Task<HttpResponseMessage> SendWithTokenAsync(Func<HttpRequestMessage> createRequest)
    {
        using var request = createRequest();
        if (session.AccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        return await http.SendAsync(request);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string message;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
            message = problem?.Errors is { Count: > 0 } errors
                ? string.Join(" ", errors.SelectMany(e => e.Value))
                : problem?.Detail ?? problem?.Title ?? response.ReasonPhrase ?? "Ошибка запроса";
        }
        catch (System.Text.Json.JsonException)
        {
            message = response.StatusCode == HttpStatusCode.TooManyRequests
                ? "Слишком много попыток, подождите минуту."
                : response.ReasonPhrase ?? "Ошибка запроса";
        }

        throw new ApiException(response.StatusCode, message);
    }
}
