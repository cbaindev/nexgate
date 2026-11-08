using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PasswordHasherCompatibilityMode = Microsoft.AspNetCore.Identity.PasswordHasherCompatibilityMode;
using PasswordHasherOptions = Microsoft.AspNetCore.Identity.PasswordHasherOptions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nexgate.BuildingBlocks.Access;
using Nexgate.BuildingBlocks.OpenApi;
using Nexgate.BuildingBlocks.Persistence;
using Nexgate.BuildingBlocks.Validation;
using Nexgate.Modules.Identity.Application;
using Nexgate.Modules.Identity.Contracts;
using Nexgate.Modules.Identity.Domain;

namespace Nexgate.Modules.Identity.Infrastructure;

public sealed record SignInRequest(string? Email, string? Password, Guid? OrganizationId);

public sealed record RefreshRequest(string? RefreshToken, Guid? OrganizationId);

public sealed record SignOutRequest(string? RefreshToken);

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken, Guid? OrganizationId);

public sealed record InviteRequest(
    [property: Required(ErrorMessage = "El correo es obligatorio")]
    [property: StringLength(EmailRules.MaxLength, ErrorMessage = "El correo no puede superar 320 caracteres")]
    string? Email,
    [property: Required(ErrorMessage = "El rol es obligatorio")]
    string? Role,
    [property: Required(ErrorMessage = "El tipo de alcance es obligatorio")]
    string? ScopeType,
    Guid? SiteId);

public sealed record AcceptInvitationRequest(string? Token, string? Password);

public sealed record AcceptInvitationResponse(Guid UserId, Guid OrganizationId);

public sealed record AssignRoleRequest(
    [property: Required(ErrorMessage = "El rol es obligatorio")]
    string? Role,
    [property: Required(ErrorMessage = "El tipo de alcance es obligatorio")]
    string? ScopeType,
    Guid? SiteId,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil);

public sealed record AdministratorInvitationRequest(
    [property: Required(ErrorMessage = "El correo es obligatorio")]
    [property: StringLength(EmailRules.MaxLength, ErrorMessage = "El correo no puede superar 320 caracteres")]
    string? Email);

public sealed record RoleAssignmentResponse(Guid Id, string Role, string ScopeType, Guid? SiteId, DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil);

public sealed record MemberResponse(Guid UserId, string Email, IReadOnlyList<RoleAssignmentResponse> RoleAssignments);

public sealed record PermissionResponse(string Name, bool Organization, IReadOnlyList<Guid> SiteIds);

public sealed record PermissionsResponse(Guid? OrganizationId, IReadOnlyList<PermissionResponse> Permissions);

/// <summary>Módulo Identity: cuentas, inicio de sesión, membresías, invitaciones y roles por alcance (D-03, D-04, D-05).</summary>
public static class IdentityModule
{
    public const string AuthRateLimitPolicy = "identity-auth";

    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<IdentityDbContext>(IdentityDbContext.Schema);
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<IdentityModuleOptions>().BindConfiguration(IdentityModuleOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<IdentityModuleOptions>, IdentityModuleOptionsValidator>();
        services.AddOptions<PasswordHasherOptions>().Configure<IOptions<IdentityModuleOptions>>((hasher, identity) =>
        {
            hasher.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
            if (identity.Value.Passwords.IterationCount is > 0 and var iterations)
            {
                hasher.IterationCount = iterations;
            }
        });
        services.AddSingleton(sp =>
        {
            var tokens = sp.GetRequiredService<IOptions<IdentityModuleOptions>>().Value.Tokens;
            return new IdentityLifetimes(TimeSpan.FromDays(tokens.RefreshSessionDays), TimeSpan.FromDays(tokens.InvitationDays));
        });

        services.AddSingleton<TokenSigningKey>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IPasswordHashing, PasswordHashing>();

        services.AddScoped<IIdentityLookups, IdentityLookups>();
        services.AddScoped<IAccountStore, AccountStore>();
        services.AddScoped<ISessionStore, SessionStore>();
        services.AddScoped<IInvitationStore, InvitationStore>();
        services.AddScoped<IMemberStore, MemberStore>();
        services.AddScoped<IOrganizationNames, OrganizationNames>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<InvitationService>();
        services.AddScoped<IAdministratorInvitations>(sp => sp.GetRequiredService<InvitationService>());
        services.AddScoped<MemberService>();
        services.AddScoped<CurrentUserService>();
        services.AddScoped<PlatformOperatorService>();
        services.AddScoped<IRequestAccessResolver, IdentityAccessResolver>();
        services.AddScoped<IPlatformAccess, IdentityPlatformAccess>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<TokenSigningKey>((jwt, key) =>
        {
            jwt.MapInboundClaims = false;
            jwt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = TokenSigningKey.Issuer,
                ValidAudience = TokenSigningKey.Audience,
                IssuerSigningKey = key.Key,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = AccessClaims.Subject
            };
        });

        services.AddOptions<RateLimiterOptions>().Configure<IOptions<IdentityModuleOptions>>((limiter, identity) =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // La clave es la IP: nunca el correo.
            limiter.AddPolicy(AuthRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = identity.Value.RateLimit.PermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Demasiados intentos. Probá de nuevo en un minuto.",
                        Extensions = { ["code"] = IdentityErrorCodes.TooManyRequests }
                    }
                });
            };
        });

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/token", async (SignInRequest request, AuthenticationService service, CancellationToken ct) =>
                ToHttp(await service.SignInAsync(request.Email, request.Password, request.OrganizationId, ct), pair => Results.Ok(ToTokenResponse(pair))))
            .WithName("signIn")
            .WithSummary("Iniciar sesión")
            .WithDescription("Valida correo y contraseña y emite el token de acceso y el de renovación. Con una sola membresía la " +
                "organización queda elegida; con varias, se elige con organizationId. Credenciales inválidas, cuenta inexistente " +
                "y cuenta deshabilitada responden igual (401 invalid_credentials).")
            .Produces<TokenResponse>()
            .Problems(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden)
            .RequireAuthRateLimiting();

        auth.MapPost("/refresh", async (RefreshRequest request, AuthenticationService service, CancellationToken ct) =>
                ToHttp(await service.RefreshAsync(request.RefreshToken, request.OrganizationId, ct), pair => Results.Ok(ToTokenResponse(pair))))
            .WithName("refreshToken")
            .WithSummary("Renovar los tokens o cambiar de organización activa")
            .WithDescription("Rota el token de renovación: el usado deja de valer y reutilizarlo revoca la sesión entera. Con " +
                "organizationId cambia la organización activa.")
            .Produces<TokenResponse>()
            .Problems(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden);

        auth.MapPost("/logout", async (SignOutRequest request, AuthenticationService service, CancellationToken ct) =>
            {
                await service.SignOutAsync(request.RefreshToken, ct);
                return Results.NoContent();
            })
            .WithName("signOut")
            .WithSummary("Cerrar sesión")
            .WithDescription("Revoca la sesión del token de renovación. Responde igual aunque el token no exista o ya esté revocado.")
            .Produces(StatusCodes.Status204NoContent)
            .Problems(StatusCodes.Status400BadRequest);

        api.MapGet("/me", async (RequestContext context, CurrentUserService service, CancellationToken ct) =>
                await service.GetAsync(context.UserId!.Value, context.OrganizationId, ct) is { } me
                    ? Results.Ok(me)
                    : AccessProblems.AuthenticationRequired())
            .RequireAuthenticatedUser()
            .WithTags("Me")
            .WithName("getCurrentUser")
            .WithSummary("Usuario actual y sus organizaciones")
            .WithDescription("Devuelve la cuenta, la organización activa y las organizaciones donde el usuario es miembro, para el selector.")
            .Produces<CurrentUserInfo>();

        api.MapGet("/me/permissions", (RequestContext context) => Results.Ok(new PermissionsResponse(
                context.OrganizationId,
                context.Grants
                    .GroupBy(g => g.Permission)
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => new PermissionResponse(
                        g.Key,
                        g.Any(x => x.SiteId is null),
                        g.Any(x => x.SiteId is null) ? [] : g.Select(x => x.SiteId!.Value).Distinct().Order().ToList()))
                    .ToList())))
            .RequireAuthenticatedUser()
            .WithTags("Me")
            .WithName("getMyPermissions")
            .WithSummary("Permisos efectivos en la organización activa")
            .WithDescription("Permisos administrativos del usuario según sus roles vigentes, con alcance de organización o la lista " +
                "de sitios. La interfaz muestra solo lo permitido; el servidor igual los verifica en cada operación.")
            .Produces<PermissionsResponse>();

        api.MapPost("/invitations", async (InviteRequest request, InvitationService service, RequestContext context, CancellationToken ct) =>
                ToHttp(await service.InviteAsync(context.OrganizationId!.Value, context.UserId!.Value,
                        new InvitationInput(request.Email, request.Role, request.ScopeType, request.SiteId), ct),
                    issued => Results.Created($"/api/v1/invitations/{issued.Id}", issued)))
            .RequirePermission(Permissions.MembersManage, atOrganization: true)
            .WithValidation<InviteRequest>()
            .WithTags("Invitations")
            .WithName("createInvitation")
            .WithSummary("Invitar a una persona a la organización activa")
            .WithDescription("Crea una invitación con rol y alcance. La respuesta es la misma tenga o no cuenta la persona invitada. " +
                "Mientras no haya envío de correos, el token de la invitación se devuelve a quien invita.")
            .Produces<IssuedInvitation>(StatusCodes.Status201Created)
            .Problems(StatusCodes.Status409Conflict);

        api.MapPost("/invitations/accept", async (AcceptInvitationRequest request, InvitationService service, RequestContext context,
                    CancellationToken ct) =>
                ToHttp(await service.AcceptAsync(request.Token, request.Password, context.UserId, ct),
                    accepted => Results.Ok(new AcceptInvitationResponse(accepted.UserId, accepted.OrganizationId))))
            .WithTags("Invitations")
            .WithName("acceptInvitation")
            .WithSummary("Aceptar una invitación")
            .WithDescription("Sin cuenta, la crea con la contraseña indicada. Con cuenta existente, exige el token de acceso de esa " +
                "cuenta (401 si no viene). Después hay que iniciar sesión para obtener tokens con la organización.")
            .AcceptsAccessToken(required: false)
            .Produces<AcceptInvitationResponse>()
            .Problems(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status409Conflict)
            .RequireAuthRateLimiting();

        var members = api.MapGroup("/members").WithTags("Members");

        members.MapGet("/", async (MemberService service, CancellationToken ct) =>
                Results.Ok((await service.ListAsync(ct))
                    .Select(m => new MemberResponse(m.UserId, m.Email, m.RoleAssignments.Select(ToResponse).ToList()))
                    .ToList()))
            .RequirePermission(Permissions.MembersRead, atOrganization: true)
            .WithName("listMembers")
            .WithSummary("Listar los miembros de la organización activa")
            .Produces<List<MemberResponse>>();

        members.MapDelete("/{userId:guid}", async (Guid userId, MemberService service, CancellationToken ct) =>
                ToHttp(await service.RemoveMemberAsync(userId, ct)))
            .RequirePermission(Permissions.MembersManage, atOrganization: true)
            .WithName("removeMember")
            .WithSummary("Dar de baja a un miembro")
            .WithDescription("Termina la membresía y sus roles con efecto en la solicitud siguiente. No se puede dar de baja al " +
                "último administrador (409 last_administrator).")
            .Produces(StatusCodes.Status204NoContent)
            .Problems(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        members.MapPost("/{userId:guid}/role-assignments", async (Guid userId, AssignRoleRequest request, MemberService service,
                    RequestContext context, CancellationToken ct) =>
                ToHttp(await service.AssignRoleAsync(context.OrganizationId!.Value, userId,
                        new RoleAssignmentInput(request.Role, request.ScopeType, request.SiteId, request.ValidFrom, request.ValidUntil), ct),
                    assignment => Results.Created($"/api/v1/members/{userId}/role-assignments/{assignment.Id}", ToResponse(assignment))))
            .RequirePermission(Permissions.MembersManage, atOrganization: true)
            .WithValidation<AssignRoleRequest>()
            .WithName("assignRole")
            .WithSummary("Asignar un rol a un miembro")
            .WithDescription("Rol con alcance de organización o de un sitio, con vigencia opcional.")
            .Produces<RoleAssignmentResponse>(StatusCodes.Status201Created)
            .Problems(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        members.MapDelete("/{userId:guid}/role-assignments/{assignmentId:guid}", async (Guid userId, Guid assignmentId,
                    MemberService service, CancellationToken ct) =>
                ToHttp(await service.RevokeRoleAsync(userId, assignmentId, ct)))
            .RequirePermission(Permissions.MembersManage, atOrganization: true)
            .WithName("revokeRole")
            .WithSummary("Revocar un rol de un miembro")
            .WithDescription("No se puede revocar el último rol de administrador vigente de la organización (409 last_administrator).")
            .Produces(StatusCodes.Status204NoContent)
            .Problems(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        api.MapPost("/platform/organizations/{organizationId:guid}/administrator-invitations", async (Guid organizationId,
                    AdministratorInvitationRequest request, IAdministratorInvitations invitations, RequestContext context, CancellationToken ct) =>
                await invitations.CreateAsync(organizationId, request.Email!, context.UserId!.Value, ct) switch
                {
                    { Status: AdministratorInvitationStatus.Created, Invitation: { } issued } =>
                        Results.Created($"/api/v1/invitations/{issued.Id}", issued),
                    { Status: AdministratorInvitationStatus.InvalidEmail } => Validation("Email", "Correo electrónico inválido."),
                    _ => NotFound()
                })
            .RequirePlatformOperator()
            .WithValidation<AdministratorInvitationRequest>()
            .WithTags("Platform")
            .WithName("reissueAdministratorInvitation")
            .WithSummary("Emitir una nueva invitación de administrador")
            .WithDescription("Para cuando la invitación del primer administrador se perdió o venció. Solo para el operador de plataforma.")
            .Produces<IssuedInvitation>(StatusCodes.Status201Created)
            .Problems(StatusCodes.Status404NotFound);

        return api;
    }

    /// <summary>Límite de intentos por IP (429 <c>too_many_requests</c>).</summary>
    private static RouteHandlerBuilder RequireAuthRateLimiting(this RouteHandlerBuilder builder) =>
        builder.RequireRateLimiting(AuthRateLimitPolicy).Problems(StatusCodes.Status429TooManyRequests);

    private static TokenResponse ToTokenResponse(TokenPair pair) =>
        new(pair.AccessToken, "Bearer", (int)Math.Max(0, (pair.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds),
            pair.RefreshToken, pair.OrganizationId);

    private static RoleAssignmentResponse ToResponse(RoleAssignment assignment) =>
        new(assignment.Id, IdentityNames.ToName(assignment.Role), IdentityNames.ToName(assignment.ScopeType), assignment.ScopeId,
            assignment.ValidFrom, assignment.ValidUntil);

    private static IResult ToHttp<T>(IdentityResult<T> result, Func<T, IResult> ok) => result switch
    {
        { Error: null, Value: { } value } => ok(value),
        { Error: { } error } => ToHttp(error),
        _ => throw new InvalidOperationException("Resultado sin valor ni error.")
    };

    private static IResult ToHttp(IdentityError error) => error.Kind switch
    {
        IdentityErrorKind.Validation => Validation(error.Field ?? string.Empty, error.Message ?? "Valor inválido"),
        IdentityErrorKind.InvalidCredentials => Problem(StatusCodes.Status401Unauthorized, "Credenciales inválidas", IdentityErrorCodes.InvalidCredentials),
        IdentityErrorKind.InvalidRefreshToken => Problem(StatusCodes.Status401Unauthorized, "Token de renovación inválido", IdentityErrorCodes.InvalidRefreshToken),
        IdentityErrorKind.OrganizationNotAvailable => Problem(StatusCodes.Status403Forbidden, "La organización no está disponible para este usuario",
            IdentityErrorCodes.OrganizationNotAvailable),
        IdentityErrorKind.InvalidInvitation => Problem(StatusCodes.Status400BadRequest, "Invitación inválida, vencida o ya usada",
            IdentityErrorCodes.InvalidInvitation),
        IdentityErrorKind.AuthenticationRequired => AccessProblems.AuthenticationRequired(),
        IdentityErrorKind.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: error.Message),
        _ => NotFound()
    };

    private static IResult ToHttp(RemovalOutcome outcome) => outcome switch
    {
        RemovalOutcome.Removed => Results.NoContent(),
        RemovalOutcome.LastAdministrator => Problem(StatusCodes.Status409Conflict,
            "La organización debe conservar al menos un administrador", IdentityErrorCodes.LastAdministrator),
        _ => NotFound()
    };

    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, title: "Error de validación");

    // Lo inexistente y lo de otra organización responden igual (no revelar existencia).
    private static IResult NotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado");

    private static IResult Problem(int status, string title, string code) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
}
