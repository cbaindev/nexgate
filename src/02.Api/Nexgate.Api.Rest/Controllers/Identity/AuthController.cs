using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Extensions;
using Nexgate.Api.Rest.Models.Identity;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Identity;

[Tags("Auth")]
[Route(Prefix + "auth")]
[AnonymousAccess]
public sealed class AuthController(IAuthenticationService authentication, TimeProvider clock) : ApiControllerBase
{
    /// <summary>
    /// Cookie del token de renovación de la aplicación web (design de add-admin-panel-shell, decisión 3): httpOnly,
    /// Secure, SameSite=Strict y acotada a las operaciones de autenticación.
    /// </summary>
    public const string RefreshCookie = "nexgate_refresh";

    public const string RefreshCookiePath = "/api/v1/auth";

    [HttpPost("token")]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [EndpointName("signIn")]
    [EndpointSummary("Iniciar sesión")]
    [EndpointDescription("Valida correo y contraseña y emite el token de acceso y el de renovación. Con una sola membresía la " +
        "organización queda elegida; con varias, se elige con organizationId. Con refreshTokenInCookie el token de renovación " +
        "viaja solo en la cookie httpOnly nexgate_refresh y refreshToken es nulo. Credenciales inválidas, cuenta inexistente " +
        "y cuenta deshabilitada responden igual (401 invalid_credentials).")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProblemResponse(StatusCodes.Status401Unauthorized)]
    [ProblemResponse(StatusCodes.Status403Forbidden)]
    public async Task<IResult> SignIn(SignInRequest request, CancellationToken ct) =>
        IdentityResults.ToHttp(await authentication.SignInAsync(request.Email, request.Password, request.OrganizationId, ct),
            pair => Results.Ok(ToTokenResponse(pair, request.RefreshTokenInCookie == true)));

    [HttpPost("refresh")]
    [EndpointName("refreshToken")]
    [EndpointSummary("Renovar los tokens o cambiar de organización activa")]
    [EndpointDescription("Rota el token de renovación: el usado deja de valer y reutilizarlo revoca la sesión entera. Con " +
        "organizationId cambia la organización activa. Sin refreshToken en el cuerpo usa la cookie nexgate_refresh, la " +
        "reemplaza por la nueva y responde refreshToken nulo.")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProblemResponse(StatusCodes.Status401Unauthorized)]
    [ProblemResponse(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var fromCookie = string.IsNullOrEmpty(request.RefreshToken);
        var result = await authentication.RefreshAsync(fromCookie ? CookieToken() : request.RefreshToken, request.OrganizationId, ct);
        if (fromCookie && result.Error?.Kind == IdentityErrorKind.InvalidRefreshToken)
        {
            DeleteCookie();
        }

        return IdentityResults.ToHttp(result, pair => Results.Ok(ToTokenResponse(pair, fromCookie)));
    }

    [HttpPost("logout")]
    [EndpointName("signOut")]
    [EndpointSummary("Cerrar sesión")]
    [EndpointDescription("Revoca la sesión del token de renovación (del cuerpo o, sin él, de la cookie nexgate_refresh) y borra " +
        "la cookie. Responde igual aunque el token no exista o ya esté revocado.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public async Task<IResult> SignOut(SignOutRequest request, CancellationToken ct)
    {
        await authentication.SignOutAsync(string.IsNullOrEmpty(request.RefreshToken) ? CookieToken() : request.RefreshToken, ct);
        DeleteCookie();
        return Results.NoContent();
    }

    private string? CookieToken() => Request.Cookies.TryGetValue(RefreshCookie, out var value) ? value : null;

    private TokenResponse ToTokenResponse(TokenPair pair, bool inCookie)
    {
        if (inCookie)
        {
            Response.Cookies.Append(RefreshCookie, pair.RefreshToken, CookieOptions(pair.SessionExpiresAt));
        }

        return new TokenResponse(pair.AccessToken, "Bearer", (int)Math.Max(0, (pair.AccessTokenExpiresAt - clock.GetUtcNow()).TotalSeconds),
            inCookie ? null : pair.RefreshToken, pair.OrganizationId);
    }

    private void DeleteCookie() => Response.Cookies.Delete(RefreshCookie, CookieOptions(null));

    private static CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
        Expires = expires,
        IsEssential = true
    };
}
