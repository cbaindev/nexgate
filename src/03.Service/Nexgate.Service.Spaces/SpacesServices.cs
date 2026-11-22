using Nexgate.Data.Spaces;
using Nexgate.Domain.Spaces;
using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Service.Spaces;

internal static class SpacesMapping
{
    public static SpaceDto ToDto(Space space) =>
        new(space.Id, space.SiteId, space.ParentId, SpaceNames.ToName(space.Type), space.Name, space.IsCommon, space.Depth,
            space.PathIds.Take(space.Depth).ToList());

    public static AccessPointDto ToDto(AccessPoint point) =>
        new(point.Id, point.SiteId, point.Name, SpaceNames.ToName(point.Type), point.ToSpaceId, point.FromSpaceId,
            SpaceNames.ToName(point.FailMode));

    public static SpacesError Validation(string field, string message) => new(SpacesErrorKind.Validation, field, message);

    public static SpacesError Validation(DomainValidationException e) => new(SpacesErrorKind.Validation, e.Field, e.Message);

    public static SpacesError Conflict(string code, string message) => new(SpacesErrorKind.Conflict, null, message, code);

    public static readonly SpacesError NameConflict =
        Conflict(SpacesErrorCodes.SpaceNameConflict, "Ya existe un espacio con ese nombre bajo el mismo padre.");

    public static readonly SpacesError RootExists =
        Conflict(SpacesErrorCodes.SpaceRootExists, "El sitio ya tiene un espacio raíz.");

    public static readonly SpacesError HasDependents =
        Conflict(SpacesErrorCodes.SpaceHasDependents, "El espacio tiene espacios hijos o puntos de acceso relacionados.");

    public static readonly SpacesError AccessPointNameConflict =
        Conflict(SpacesErrorCodes.AccessPointNameConflict, "Ya existe un punto de acceso con ese nombre en el sitio.");
}

/// <summary>Camino a un destino: los puntos que dan acceso al destino o a sus ancestros, desde la raíz (D-13).</summary>
internal sealed class AccessPathQuery(ISpaceStore spaces, IAccessPointStore points)
{
    public async Task<IReadOnlyList<AccessPathEntryDto>?> GetAsync(Guid spaceId, CancellationToken cancellationToken)
    {
        if (await spaces.FindAsync(spaceId, cancellationToken) is not { } destination)
        {
            return null;
        }

        // El path del destino ya tiene sus ancestros: no hace falta recorrer el árbol.
        var depthById = destination.PathIds.Select((id, depth) => (id, depth)).ToDictionary(x => x.id, x => x.depth);
        var onPath = await points.ListByTargetsAsync(depthById.Keys, cancellationToken);
        return onPath
            .Select(p => (Point: p, Depth: depthById[p.ToSpaceId]))
            .OrderBy(x => x.Depth)
            .ThenBy(x => x.Point.Name, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(x => x.Point.Id)
            .Select(x => new AccessPathEntryDto(x.Point.Id, x.Point.SiteId, x.Point.Name, SpaceNames.ToName(x.Point.Type),
                x.Point.ToSpaceId, x.Point.FromSpaceId, SpaceNames.ToName(x.Point.FailMode), x.Depth))
            .ToList();
    }
}

internal sealed class SpaceService(
    ISpaceStore store,
    ISiteReferences sites,
    AccessPathQuery paths,
    IEnumerable<ISpaceUsage> usages,
    TimeProvider clock) : ISpaceService
{
    public async Task<SpacesResult<IReadOnlyList<SpaceDto>>> ListAsync(Guid siteId, CancellationToken cancellationToken)
    {
        if (!await sites.SiteExistsAsync(siteId, cancellationToken))
        {
            return SpacesResult<IReadOnlyList<SpaceDto>>.NotFound();
        }

        var spaces = await store.ListBySiteAsync(siteId, cancellationToken);
        return SpacesResult<IReadOnlyList<SpaceDto>>.Ok(Space.InTreeOrder(spaces).Select(SpacesMapping.ToDto).ToList());
    }

    public async Task<SpacesResult<SpaceDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await store.FindAsync(id, cancellationToken) is { } space
            ? SpacesResult<SpaceDto>.Ok(SpacesMapping.ToDto(space))
            : SpacesResult<SpaceDto>.NotFound();

    public async Task<SpacesResult<SpaceDto>> CreateAsync(Guid siteId, SpaceInput input, CancellationToken cancellationToken)
    {
        if (!await sites.SiteExistsAsync(siteId, cancellationToken))
        {
            return SpacesResult<SpaceDto>.NotFound();
        }

        if (!SpaceNames.TryParseSpaceType(input.Type, out var type))
        {
            return SpacesResult<SpaceDto>.Fail(SpacesMapping.Validation(nameof(SpaceInput.Type), "Tipo de espacio inválido."));
        }

        Space? parent = null;
        if (input.ParentId is { } parentId)
        {
            parent = await store.FindAsync(parentId, cancellationToken);
            if (parent is null || parent.SiteId != siteId)
            {
                return SpacesResult<SpaceDto>.Fail(SpacesMapping.Validation(nameof(SpaceInput.ParentId), "El padre no existe en el sitio."));
            }
        }

        Space space;
        try
        {
            space = Space.Create(siteId, parent, type, input.Name, input.IsCommon, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return SpacesResult<SpaceDto>.Fail(SpacesMapping.Validation(e));
        }

        if (parent is null && await store.RootExistsAsync(siteId, cancellationToken))
        {
            return SpacesResult<SpaceDto>.Fail(SpacesMapping.RootExists);
        }

        if (parent is not null && await store.SiblingNameInUseAsync(parent.Id, space.Name, null, cancellationToken))
        {
            return SpacesResult<SpaceDto>.Fail(SpacesMapping.NameConflict);
        }

        store.Add(space);
        return await SaveAsync(space, cancellationToken);
    }

    public async Task<SpacesResult<SpaceDto>> UpdateAsync(Guid id, SpaceUpdate update, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } space)
        {
            return SpacesResult<SpaceDto>.NotFound();
        }

        if (!SpaceNames.TryParseSpaceType(update.Type, out var type))
        {
            return SpacesResult<SpaceDto>.Fail(SpacesMapping.Validation(nameof(SpaceUpdate.Type), "Tipo de espacio inválido."));
        }

        try
        {
            space.Update(update.Name, type, update.IsCommon, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return SpacesResult<SpaceDto>.Fail(SpacesMapping.Validation(e));
        }

        if (space.ParentId is { } parentId && await store.SiblingNameInUseAsync(parentId, space.Name, space.Id, cancellationToken))
        {
            return SpacesResult<SpaceDto>.Fail(SpacesMapping.NameConflict);
        }

        return await SaveAsync(space, cancellationToken);
    }

    public async Task<SpacesResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } space)
        {
            return SpacesResult<Guid>.NotFound();
        }

        if (await store.HasDependentsAsync(id, cancellationToken) || await UsedByOtherModulesAsync(id, cancellationToken))
        {
            return SpacesResult<Guid>.Fail(SpacesMapping.HasDependents);
        }

        store.Remove(space);
        return await store.SaveChangesAsync(cancellationToken) switch
        {
            SpaceSaveOutcome.Saved => SpacesResult<Guid>.Ok(id),
            _ => SpacesResult<Guid>.Fail(SpacesMapping.HasDependents)
        };
    }

    public async Task<SpacesResult<IReadOnlyList<AccessPathEntryDto>>> GetAccessPathAsync(Guid spaceId, CancellationToken cancellationToken) =>
        await paths.GetAsync(spaceId, cancellationToken) is { } path
            ? SpacesResult<IReadOnlyList<AccessPathEntryDto>>.Ok(path)
            : SpacesResult<IReadOnlyList<AccessPathEntryDto>>.NotFound();

    private async Task<bool> UsedByOtherModulesAsync(Guid spaceId, CancellationToken cancellationToken)
    {
        foreach (var usage in usages)
        {
            if (await usage.IsUsedAsync(spaceId, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<SpacesResult<SpaceDto>> SaveAsync(Space space, CancellationToken cancellationToken) =>
        await store.SaveChangesAsync(cancellationToken) switch
        {
            SpaceSaveOutcome.Saved => SpacesResult<SpaceDto>.Ok(SpacesMapping.ToDto(space)),
            SpaceSaveOutcome.RootExists => SpacesResult<SpaceDto>.Fail(SpacesMapping.RootExists),
            SpaceSaveOutcome.NameConflict => SpacesResult<SpaceDto>.Fail(SpacesMapping.NameConflict),
            // El padre se eliminó en paralelo: igual que un padre inexistente.
            _ => SpacesResult<SpaceDto>.Fail(SpacesMapping.Validation(nameof(SpaceInput.ParentId), "El padre no existe en el sitio."))
        };
}

internal sealed class AccessPointService(IAccessPointStore store, ISpaceStore spaces, ISiteReferences sites, TimeProvider clock)
    : IAccessPointService
{
    public async Task<SpacesResult<IReadOnlyList<AccessPointDto>>> ListAsync(Guid siteId, CancellationToken cancellationToken)
    {
        if (!await sites.SiteExistsAsync(siteId, cancellationToken))
        {
            return SpacesResult<IReadOnlyList<AccessPointDto>>.NotFound();
        }

        var points = await store.ListBySiteAsync(siteId, cancellationToken);
        return SpacesResult<IReadOnlyList<AccessPointDto>>.Ok(points.Select(SpacesMapping.ToDto).ToList());
    }

    public async Task<SpacesResult<AccessPointDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await store.FindAsync(id, cancellationToken) is { } point
            ? SpacesResult<AccessPointDto>.Ok(SpacesMapping.ToDto(point))
            : SpacesResult<AccessPointDto>.NotFound();

    public async Task<SpacesResult<AccessPointDto>> CreateAsync(Guid siteId, AccessPointInput input, CancellationToken cancellationToken)
    {
        if (!await sites.SiteExistsAsync(siteId, cancellationToken))
        {
            return SpacesResult<AccessPointDto>.NotFound();
        }

        var parsed = await ParseAsync(siteId, input, cancellationToken);
        if (parsed.Error is { } error)
        {
            return SpacesResult<AccessPointDto>.Fail(error);
        }

        AccessPoint point;
        try
        {
            point = AccessPoint.Create(siteId, input.Name, parsed.Type, parsed.To!, parsed.From, parsed.FailMode, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return SpacesResult<AccessPointDto>.Fail(SpacesMapping.Validation(e));
        }

        if (await store.NameInUseAsync(siteId, point.Name, null, cancellationToken))
        {
            return SpacesResult<AccessPointDto>.Fail(SpacesMapping.AccessPointNameConflict);
        }

        store.Add(point);
        return await SaveAsync(point, cancellationToken);
    }

    public async Task<SpacesResult<AccessPointDto>> UpdateAsync(Guid id, AccessPointInput input, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } point)
        {
            return SpacesResult<AccessPointDto>.NotFound();
        }

        var parsed = await ParseAsync(point.SiteId, input, cancellationToken);
        if (parsed.Error is { } error)
        {
            return SpacesResult<AccessPointDto>.Fail(error);
        }

        try
        {
            point.Update(input.Name, parsed.Type, parsed.To!, parsed.From, parsed.FailMode, clock.GetUtcNow());
        }
        catch (DomainValidationException e)
        {
            return SpacesResult<AccessPointDto>.Fail(SpacesMapping.Validation(e));
        }

        if (await store.NameInUseAsync(point.SiteId, point.Name, point.Id, cancellationToken))
        {
            return SpacesResult<AccessPointDto>.Fail(SpacesMapping.AccessPointNameConflict);
        }

        return await SaveAsync(point, cancellationToken);
    }

    public async Task<SpacesResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(id, cancellationToken) is not { } point)
        {
            return SpacesResult<Guid>.NotFound();
        }

        store.Remove(point);
        await store.SaveChangesAsync(cancellationToken);
        return SpacesResult<Guid>.Ok(id);
    }

    private sealed record Parsed(AccessPointType Type, FailMode FailMode, Space? To, Space? From, SpacesError? Error);

    private async Task<Parsed> ParseAsync(Guid siteId, AccessPointInput input, CancellationToken cancellationToken)
    {
        if (!SpaceNames.TryParseAccessPointType(input.Type, out var type))
        {
            return Failed(nameof(AccessPointInput.Type), "Tipo de punto de acceso inválido.");
        }

        if (!SpaceNames.TryParseFailMode(input.FailMode, out var failMode))
        {
            return Failed(nameof(AccessPointInput.FailMode), "Modo de fallo inválido: fail_safe o fail_secure.");
        }

        var ids = new[] { input.ToSpaceId }.Concat(input.FromSpaceId is { } f ? [f] : Array.Empty<Guid>()).Distinct().ToList();
        var found = (await spaces.FindManyAsync(ids, cancellationToken)).Where(s => s.SiteId == siteId).ToDictionary(s => s.Id);
        if (!found.TryGetValue(input.ToSpaceId, out var to))
        {
            return Failed(nameof(AccessPointInput.ToSpaceId), "El espacio no existe en el sitio.");
        }

        Space? from = null;
        if (input.FromSpaceId is { } fromId && !found.TryGetValue(fromId, out from))
        {
            return Failed(nameof(AccessPointInput.FromSpaceId), "El espacio no existe en el sitio.");
        }

        return new Parsed(type, failMode, to, from, null);

        static Parsed Failed(string field, string message) => new(default, default, null, null, SpacesMapping.Validation(field, message));
    }

    private async Task<SpacesResult<AccessPointDto>> SaveAsync(AccessPoint point, CancellationToken cancellationToken) =>
        await store.SaveChangesAsync(cancellationToken) == AccessPointSaveOutcome.Saved
            ? SpacesResult<AccessPointDto>.Ok(SpacesMapping.ToDto(point))
            : SpacesResult<AccessPointDto>.Fail(SpacesMapping.AccessPointNameConflict);
}
