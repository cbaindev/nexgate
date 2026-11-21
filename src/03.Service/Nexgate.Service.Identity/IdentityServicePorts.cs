namespace Nexgate.Service.Identity;

internal enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded
}

internal interface IPasswordHashing
{
    string Hash(string password);

    PasswordCheck Verify(string passwordHash, string password);

    /// <summary>Verifica contra un hash ficticio, para que una cuenta inexistente tarde lo mismo que una existente.</summary>
    void VerifyAgainstDummy(string password);
}

internal sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

internal interface IAccessTokenIssuer
{
    AccessToken Issue(Guid userId, Guid? organizationId, Guid sessionId);
}

/// <summary>Duraciones configurables (sección <c>Identity:Tokens</c>).</summary>
internal sealed record IdentityLifetimes(TimeSpan RefreshSession, TimeSpan Invitation);

/// <summary>Nombre de una organización donde el usuario es miembro (verificado por quien llama).</summary>
internal interface IOrganizationNames
{
    Task<string?> GetNameAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);
}
