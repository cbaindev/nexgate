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
    [HttpPost("token")]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [EndpointName("signIn")]
    [EndpointSummary("Iniciar sesión")]
    [EndpointDescription("Valida correo y contraseña y emite el token de acceso y el de renovación. Con una sola membresía la " +
        "organización queda elegida; con varias, se elige con organizationId. Credenciales inválidas, cuenta inexistente " +
        "y cuenta deshabilitada responden igual (401 invalid_credentials).")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProblemResponse(StatusCodes.Status401Unauthorized)]
    [ProblemResponse(StatusCodes.Status403Forbidden)]
    public async Task<IResult> SignIn(SignInRequest request, CancellationToken ct) =>
        IdentityResults.ToHttp(await authentication.SignInAsync(request.Email, request.Password, request.OrganizationId, ct),
            pair => Results.Ok(ToTokenResponse(pair)));

    [HttpPost("refresh")]
    [EndpointName("refreshToken")]
    [EndpointSummary("Renovar los tokens o cambiar de organización activa")]
    [EndpointDescription("Rota el token de renovación: el usado deja de valer y reutilizarlo revoca la sesión entera. Con " +
        "organizationId cambia la organización activa.")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    [ProblemResponse(StatusCodes.Status401Unauthorized)]
    [ProblemResponse(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Refresh(RefreshRequest request, CancellationToken ct) =>
        IdentityResults.ToHttp(await authentication.RefreshAsync(request.RefreshToken, request.OrganizationId, ct),
            pair => Results.Ok(ToTokenResponse(pair)));

    [HttpPost("logout")]
    [EndpointName("signOut")]
    [EndpointSummary("Cerrar sesión")]
    [EndpointDescription("Revoca la sesión del token de renovación. Responde igual aunque el token no exista o ya esté revocado.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status400BadRequest)]
    public async Task<IResult> SignOut(SignOutRequest request, CancellationToken ct)
    {
        await authentication.SignOutAsync(request.RefreshToken, ct);
        return Results.NoContent();
    }

    private TokenResponse ToTokenResponse(TokenPair pair) =>
        new(pair.AccessToken, "Bearer", (int)Math.Max(0, (pair.AccessTokenExpiresAt - clock.GetUtcNow()).TotalSeconds),
            pair.RefreshToken, pair.OrganizationId);
}
