using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Nexgate.Test.Api.SpacesApiTests;

namespace Nexgate.Test.Api;

/// <summary>Token de renovación en cookie httpOnly para la aplicación web (add-admin-panel-shell, cambios en authentication).</summary>
public class AuthCookieTests
{
    private const string Cookie = "nexgate_refresh";

    private static string? SetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values.FirstOrDefault(v => v.StartsWith(Cookie + "=", StringComparison.Ordinal)) : null;

    private static string CookieValue(string setCookie) => setCookie.Split(';')[0][(Cookie.Length + 1)..];

    private static async Task<HttpResponseMessage> PostWithCookieAsync(NexgateFactory factory, string path, string? cookie, object body)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{Cookie}={cookie}");
        }

        return await client.SendAsync(request);
    }

    private static async Task<(string Email, string Cookie, JsonElement Body)> SignInWithCookieAsync(NexgateFactory factory)
    {
        var organization = await factory.CreateOrganizationAsync();
        var email = factory.Administrator(organization).Email;
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/token", new { email, password = NexgateFactory.Password, refreshTokenInCookie = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = SetCookie(response);
        Assert.NotNull(setCookie);
        return (email, CookieValue(setCookie), await Json(response));
    }

    [Fact]
    public async Task Sign_in_can_deliver_the_refresh_token_only_in_an_http_only_cookie()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/token",
            new { email = factory.Administrator(organization).Email, password = NexgateFactory.Password, refreshTokenInCookie = true });
        var body = await Json(response);
        var setCookie = SetCookie(response)!.ToLowerInvariant();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("refreshToken").ValueKind);
        Assert.Contains("httponly", setCookie);
        Assert.Contains("secure", setCookie);
        Assert.Contains("samesite=strict", setCookie);
        Assert.Contains("path=/api/v1/auth", setCookie);
        var expires = DateTimeOffset.Parse(setCookie.Split(';').Select(p => p.Trim()).Single(p => p.StartsWith("expires=")).Split('=')[1]);
        Assert.InRange(expires, DateTimeOffset.UtcNow.AddDays(29), DateTimeOffset.UtcNow.AddDays(31));
    }

    [Fact]
    public async Task Sign_in_without_the_option_keeps_the_token_in_the_body()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        var response = await factory.PostSignInAsync(factory.Administrator(organization).Email, NexgateFactory.Password);

        Assert.False(string.IsNullOrEmpty((await Json(response)).GetProperty("refreshToken").GetString()));
        Assert.Null(SetCookie(response));
    }

    [Fact]
    public async Task Refresh_with_the_cookie_rotates_it()
    {
        using var factory = new NexgateFactory();
        var (_, cookie, _) = await SignInWithCookieAsync(factory);

        var refreshed = await PostWithCookieAsync(factory, "/api/v1/auth/refresh", cookie, new { });
        var next = SetCookie(refreshed);
        var reused = await PostWithCookieAsync(factory, "/api/v1/auth/refresh", cookie, new { });

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await Json(refreshed)).GetProperty("refreshToken").ValueKind);
        Assert.NotNull(next);
        Assert.NotEqual(cookie, CookieValue(next));
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal("invalid_refresh_token", (await Json(reused)).GetProperty("code").GetString());
        // La reutilización revocó la sesión: el token nuevo tampoco vale.
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWithCookieAsync(factory, "/api/v1/auth/refresh", CookieValue(next), new { })).StatusCode);
    }

    [Fact]
    public async Task Refresh_without_token_nor_cookie_is_rejected()
    {
        using var factory = new NexgateFactory();

        var response = await PostWithCookieAsync(factory, "/api/v1/auth/refresh", null, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_refresh_token", (await Json(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Sign_out_with_the_cookie_revokes_the_session_and_removes_the_cookie()
    {
        using var factory = new NexgateFactory();
        var (_, cookie, _) = await SignInWithCookieAsync(factory);

        var signOut = await PostWithCookieAsync(factory, "/api/v1/auth/logout", cookie, new { });
        var removal = SetCookie(signOut)!.ToLowerInvariant();
        var refresh = await PostWithCookieAsync(factory, "/api/v1/auth/refresh", cookie, new { });

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Contains("expires=thu, 01 jan 1970", removal);
        Assert.Contains("path=/api/v1/auth", removal);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task The_current_user_says_whether_it_is_a_platform_operator()
    {
        using var factory = new NexgateFactory();
        var organization = await factory.CreateOrganizationAsync();
        using var admin = factory.CreateClientFor(organization);
        using var operatorClient = await factory.CreateOperatorClientAsync();

        Assert.False((await Json(await admin.GetAsync("/api/v1/me"))).GetProperty("platformOperator").GetBoolean());
        Assert.True((await Json(await operatorClient.GetAsync("/api/v1/me"))).GetProperty("platformOperator").GetBoolean());
    }
}
