using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexgate.Data.Occupancy;
using Nexgate.Domain.Occupancy;
using Nexgate.Service.Occupancy.Contracts;
using Nexgate.Service.Tenancy.Contracts;

namespace Nexgate.Service.Occupancy;

internal sealed class SiteReferences(ISiteDirectory sites) : ISiteReferences
{
    public Task<bool> SiteExistsAsync(Guid siteId, CancellationToken cancellationToken) => sites.ExistsAsync(siteId, cancellationToken);
}

internal sealed class SubjectService(ISubjectStore store, ISiteReferences sites, IEnumerable<ISubjectUsage> usages, TimeProvider clock)
    : ISubjectService
{
    public async Task<OccupancyResult<IReadOnlyList<SubjectDto>>> ListAsync(Guid siteId, CancellationToken cancellationToken)
    {
        if (!await sites.SiteExistsAsync(siteId, cancellationToken))
        {
            return OccupancyResult<IReadOnlyList<SubjectDto>>.NotFound();
        }

        return OccupancyResult<IReadOnlyList<SubjectDto>>.Ok((await store.ListBySiteAsync(siteId, cancellationToken)).Select(ToDto).ToList());
    }

    public async Task<OccupancyResult<SubjectDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await store.FindAsync(id, cancellationToken) is { } subject
            ? OccupancyResult<SubjectDto>.Ok(ToDto(subject))
            : OccupancyResult<SubjectDto>.NotFound();

    public async Task<OccupancyResult<SubjectDto>> CreateAsync(Guid siteId, SubjectInput input, Guid createdBy, CancellationToken cancellationToken)
    {
        if (!await sites.SiteExistsAsync(siteId, cancellationToken))
        {
            return OccupancyResult<SubjectDto>.NotFound();
        }

        if (!SubjectKinds.TryParse(input.Kind, out var kind))
        {
            return Invalid(nameof(SubjectInput.Kind), "Tipo de sujeto inválido: person o anonymous.");
        }

        Subject subject;
        try
        {
            subject = Subject.Create(siteId, kind, input.Alias, createdBy, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return Invalid(e.Field, e.Message);
        }

        store.Add(subject);
        await store.SaveChangesAsync(cancellationToken);
        return OccupancyResult<SubjectDto>.Ok(ToDto(subject));
    }

    public async Task<OccupancyResult<SubjectDto>> UpdateAsync(Guid id, SubjectInput input, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } subject)
        {
            return OccupancyResult<SubjectDto>.NotFound();
        }

        if (!SubjectKinds.TryParse(input.Kind, out var kind))
        {
            return Invalid(nameof(SubjectInput.Kind), "Tipo de sujeto inválido: person o anonymous.");
        }

        try
        {
            subject.Update(kind, input.Alias, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return Invalid(e.Field, e.Message);
        }

        await store.SaveChangesAsync(cancellationToken);
        return OccupancyResult<SubjectDto>.Ok(ToDto(subject));
    }

    public async Task<OccupancyResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } subject)
        {
            return OccupancyResult<Guid>.NotFound();
        }

        foreach (var usage in usages)
        {
            if (await usage.IsUsedAsync(id, cancellationToken))
            {
                return OccupancyResult<Guid>.Fail(new OccupancyError(OccupancyErrorKind.Conflict, null,
                    "El sujeto tiene credenciales u otorgamientos.", OccupancyErrorCodes.SubjectInUse));
            }
        }

        store.Remove(subject);
        await store.SaveChangesAsync(cancellationToken);
        return OccupancyResult<Guid>.Ok(id);
    }

    public static SubjectDto ToDto(Subject subject) => new(subject.Id, subject.SiteId, SubjectKinds.ToName(subject.Kind), subject.Alias);

    private static OccupancyResult<SubjectDto> Invalid(string field, string message) =>
        OccupancyResult<SubjectDto>.Fail(new OccupancyError(OccupancyErrorKind.Validation, field, message));
}

internal sealed class SubjectDirectory(ISubjectStore store) : ISubjectDirectory
{
    public async Task<SubjectDto?> GetSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
        await store.FindAsync(subjectId, cancellationToken) is { } subject ? SubjectService.ToDto(subject) : null;
}

/// <summary>Módulo Occupancy: por ahora, el registro mínimo de sujetos (add-access-evaluator). Requiere Tenancy.</summary>
public static class OccupancyServiceCollectionExtensions
{
    public static IServiceCollection AddOccupancyService(this IServiceCollection services)
    {
        services.AddOccupancyData();
        services.AddScoped<ISiteReferences, SiteReferences>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<ISubjectDirectory, SubjectDirectory>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
