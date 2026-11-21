using System.Text.Json;
using NodaTime;
using Nexgate.Modules.Tenancy.Contracts;
using Nexgate.Modules.Tenancy.Domain;

namespace Nexgate.Modules.Tenancy.Application;

public enum TenancyErrorKind
{
    Validation,
    NotFound,
    Conflict
}

public sealed record TenancyError(TenancyErrorKind Kind, string? Field = null, string? Message = null);

/// <summary>Resultado de un caso de uso: un valor o un error esperado (los errores de programación son excepciones).</summary>
public sealed record TenancyResult<T>(T? Value, TenancyError? Error)
{
    public static TenancyResult<T> Ok(T value) => new(value, null);

    public static TenancyResult<T> Fail(TenancyErrorKind kind, string? field = null, string? message = null) =>
        new(default, new TenancyError(kind, field, message));
}

public sealed record SiteInput(string Name, string Type, string TimeZone, string? Configuration);

public sealed record SiteUpdate(string Name, string TimeZone, string? Configuration);

public static class TimeZones
{
    /// <summary>Identificador IANA válido según la base tzdb incluida (no depende del sistema operativo).</summary>
    public static bool IsValid(string? id) =>
        !string.IsNullOrWhiteSpace(id) && DateTimeZoneProviders.Tzdb.GetZoneOrNull(id) is not null;
}

public static class SiteTypes
{
    public static bool TryParse(string? value, out SiteType type)
    {
        (var ok, type) = value switch
        {
            SiteTypeNames.Building => (true, SiteType.Building),
            SiteTypeNames.GatedCommunity => (true, SiteType.GatedCommunity),
            SiteTypeNames.Company => (true, SiteType.Company),
            SiteTypeNames.House => (true, SiteType.House),
            _ => (false, default)
        };
        return ok;
    }

    public static string ToName(SiteType type) => type switch
    {
        SiteType.Building => SiteTypeNames.Building,
        SiteType.GatedCommunity => SiteTypeNames.GatedCommunity,
        SiteType.Company => SiteTypeNames.Company,
        SiteType.House => SiteTypeNames.House,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

public sealed class OrganizationService(IOrganizationStore store, TimeProvider clock)
{
    public async Task<TenancyResult<OrganizationDto>> CreateAsync(string name, CancellationToken cancellationToken)
    {
        Organization organization;
        try
        {
            organization = Organization.Create(name, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return TenancyResult<OrganizationDto>.Fail(TenancyErrorKind.Validation, e.Field, e.Message);
        }

        await store.AddAsPlatformOperationAsync(organization, cancellationToken);
        return TenancyResult<OrganizationDto>.Ok(ToDto(organization));
    }

    public async Task<TenancyResult<OrganizationDto>> GetActiveAsync(CancellationToken cancellationToken) =>
        await store.FindActiveAsync(cancellationToken) is { } organization
            ? TenancyResult<OrganizationDto>.Ok(ToDto(organization))
            : TenancyResult<OrganizationDto>.Fail(TenancyErrorKind.NotFound);

    public static OrganizationDto ToDto(Organization organization) =>
        new(organization.Id, organization.Name, organization.Status.ToString().ToLowerInvariant());
}

public sealed class SiteService(ISiteStore store, TimeProvider clock)
{
    public async Task<IReadOnlyList<SiteDto>> ListAsync(IReadOnlyCollection<Guid>? onlySites, CancellationToken cancellationToken) =>
        (await store.ListAsync(onlySites, cancellationToken)).Select(ToDto).ToList();

    public async Task<TenancyResult<SiteDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await store.FindAsync(id, cancellationToken) is { } site
            ? TenancyResult<SiteDto>.Ok(ToDto(site))
            : TenancyResult<SiteDto>.Fail(TenancyErrorKind.NotFound);

    /// <summary>Crea el sitio en la organización activa, que recibe el llamador (nunca del contenido de la solicitud).</summary>
    public async Task<TenancyResult<SiteDto>> CreateAsync(Guid activeOrganizationId, SiteInput input, CancellationToken cancellationToken)
    {
        if (!SiteTypes.TryParse(input.Type, out var type))
        {
            return TenancyResult<SiteDto>.Fail(TenancyErrorKind.Validation, nameof(SiteInput.Type), "Tipo de sitio inválido.");
        }

        if (!TimeZones.IsValid(input.TimeZone))
        {
            return TenancyResult<SiteDto>.Fail(TenancyErrorKind.Validation, nameof(SiteInput.TimeZone), "Zona horaria IANA inválida.");
        }

        Site site;
        try
        {
            site = Site.Create(activeOrganizationId, input.Name, type, input.TimeZone, input.Configuration, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return TenancyResult<SiteDto>.Fail(TenancyErrorKind.Validation, e.Field, e.Message);
        }

        if (await store.NameInUseAsync(site.Name, null, cancellationToken))
        {
            return NameConflict();
        }

        store.Add(site);
        return await store.SaveChangesAsync(cancellationToken) ? TenancyResult<SiteDto>.Ok(ToDto(site)) : NameConflict();
    }

    public async Task<TenancyResult<SiteDto>> UpdateAsync(Guid id, SiteUpdate update, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } site)
        {
            return TenancyResult<SiteDto>.Fail(TenancyErrorKind.NotFound);
        }

        if (!TimeZones.IsValid(update.TimeZone))
        {
            return TenancyResult<SiteDto>.Fail(TenancyErrorKind.Validation, nameof(SiteUpdate.TimeZone), "Zona horaria IANA inválida.");
        }

        try
        {
            site.Update(update.Name, update.TimeZone, update.Configuration, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return TenancyResult<SiteDto>.Fail(TenancyErrorKind.Validation, e.Field, e.Message);
        }

        if (await store.NameInUseAsync(site.Name, site.Id, cancellationToken))
        {
            return NameConflict();
        }

        return await store.SaveChangesAsync(cancellationToken) ? TenancyResult<SiteDto>.Ok(ToDto(site)) : NameConflict();
    }

    public static SiteDto ToDto(Site site)
    {
        using var configuration = JsonDocument.Parse(site.Configuration);
        return new SiteDto(site.Id, site.OrganizationId, site.Name, SiteTypes.ToName(site.Type), site.TimeZone,
            configuration.RootElement.Clone(), site.CreatedAt, site.UpdatedAt);
    }

    private static TenancyResult<SiteDto> NameConflict() =>
        TenancyResult<SiteDto>.Fail(TenancyErrorKind.Conflict, nameof(SiteInput.Name), "Ya existe un sitio con ese nombre en la organización.");
}
