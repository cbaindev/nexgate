using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Filters;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Devices.Contracts;

namespace Nexgate.Api.Rest.Authorization;

/// <summary>Autenticación de dispositivos por certificado de cliente (design de add-device-sync-protocol, decisión 3).</summary>
public static class DeviceAuthentication
{
    /// <summary>Esquema de certificado de cliente. No es el esquema por defecto: solo lo usan los endpoints de dispositivo.</summary>
    public const string Scheme = "DeviceCertificate";

    public const string DeviceIdClaim = "device_id";
    public const string OrganizationClaim = "org";
    public const string SiteIdClaim = "site_id";

    private const string ItemKey = "nexgate:device";

    public static ClaimsPrincipal Principal(DeviceIdentity device) => new(new ClaimsIdentity(
    [
        new Claim(DeviceIdClaim, device.DeviceId.ToString("D")),
        new Claim(OrganizationClaim, device.OrganizationId.ToString("D")),
        new Claim(SiteIdClaim, device.SiteId.ToString("D"))
    ], Scheme));

    /// <summary>Dispositivo autenticado de la solicitud; lo fija <see cref="RequireDeviceAttribute"/>.</summary>
    public static DeviceIdentity Device(this HttpContext http) =>
        http.Items[ItemKey] as DeviceIdentity ?? throw new InvalidOperationException("La solicitud no tiene un dispositivo autenticado.");

    internal static void SetDevice(HttpContext http, DeviceIdentity device) => http.Items[ItemKey] = device;

    internal static DeviceIdentity? FromPrincipal(ClaimsPrincipal? principal) =>
        Guid.TryParse(principal?.FindFirst(DeviceIdClaim)?.Value, out var device)
        && Guid.TryParse(principal.FindFirst(OrganizationClaim)?.Value, out var organization)
        && Guid.TryParse(principal.FindFirst(SiteIdClaim)?.Value, out var site)
            ? new DeviceIdentity(device, organization, site)
            : null;
}

/// <summary>
/// Exige el certificado vigente de un dispositivo activo (401 <c>device_authentication_required</c>). La organización
/// activa de la solicitud pasa a ser la del dispositivo, tomada del certificado verificado. El token de usuario no
/// cuenta en estas operaciones.
/// </summary>
public sealed class RequireDeviceAttribute : AccessRequirementAttribute
{
    public override bool AcceptsToken => false;

    public override bool RequiresToken => false;

    public override bool AcceptsDeviceCertificate => true;

    public override IReadOnlyList<int> ProblemStatusCodes => [StatusCodes.Status401Unauthorized];

    public override async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var result = await http.AuthenticateAsync(DeviceAuthentication.Scheme);
        if (!result.Succeeded || DeviceAuthentication.FromPrincipal(result.Principal) is not { } device)
        {
            context.Result = new ResultActionResult(Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Se requiere el certificado de un dispositivo activo",
                extensions: new Dictionary<string, object?> { ["code"] = DevicesErrorCodes.DeviceAuthenticationRequired }));
            return;
        }

        Context(context).SetOrganization(device.OrganizationId);
        DeviceAuthentication.SetDevice(http, device);
    }
}
