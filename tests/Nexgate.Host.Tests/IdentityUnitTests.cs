using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Nexgate.BuildingBlocks.Access;
using Nexgate.Modules.Identity.Application;
using Nexgate.Modules.Identity.Contracts;
using Nexgate.Modules.Identity.Domain;
using Nexgate.Modules.Identity.Infrastructure;

namespace Nexgate.Host.Tests;

/// <summary>Reglas del dominio, catálogo de permisos y tokens, sin base de datos.</summary>
public class IdentityUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("  Ana.Perez@Example.COM ", "ana.perez@example.com")]
    [InlineData("a@b", "a@b")]
    public void Valid_emails_are_normalized(string input, string expected)
    {
        Assert.True(EmailRules.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin-arroba")]
    [InlineData("@example.com")]
    [InlineData("ana@")]
    [InlineData("ana@@example.com")]
    [InlineData("ana perez@example.com")]
    public void Invalid_emails_are_rejected(string? input)
    {
        Assert.False(EmailRules.TryNormalize(input, out _));
        Assert.Throws<DomainValidationException>(() => EmailAddress.Normalize(input));
    }

    [Fact]
    public void Email_longer_than_320_characters_is_rejected() =>
        Assert.False(EmailRules.TryNormalize(new string('a', 315) + "@x.com", out _));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("once-chars!")]
    public void Passwords_shorter_than_12_characters_are_rejected(string? password)
    {
        var error = Assert.Throws<DomainValidationException>(() => PasswordPolicy.Validate(password));
        Assert.Equal("Password", error.Field);
    }

    [Fact]
    public void Password_of_12_characters_is_accepted() => PasswordPolicy.Validate("doce-caracts");

    [Fact]
    public void Site_scope_requires_a_site_and_organization_scope_ignores_it()
    {
        Assert.Throws<DomainValidationException>(() => RoleScope.Create(ScopeType.Site, null));
        Assert.Throws<DomainValidationException>(() => RoleScope.Create(ScopeType.Site, Guid.Empty));
        Assert.Equal(RoleScope.Organization, RoleScope.Create(ScopeType.Organization, Guid.NewGuid()));
    }

    [Fact]
    public void Validity_must_end_after_it_starts()
    {
        var error = Assert.Throws<DomainValidationException>(() =>
            RoleAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), Role.Guard, RoleScope.Organization, Now, Now, Now));
        Assert.Equal("ValidUntil", error.Field);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(-1, null, true)]
    [InlineData(1, null, false)]
    [InlineData(null, 1, true)]
    [InlineData(null, 0, false)]
    [InlineData(-2, -1, false)]
    public void Assignment_is_valid_only_inside_its_period(int? fromHours, int? untilHours, bool valid)
    {
        var assignment = RoleAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), Role.Guard, RoleScope.Organization,
            fromHours is { } f ? Now.AddHours(f) : null, untilHours is { } u ? Now.AddHours(u) : null, Now.AddDays(-1));

        Assert.Equal(valid, assignment.IsValidAt(Now));
    }

    [Fact]
    public void Catalog_grants_permissions_with_the_scope_of_each_valid_assignment()
    {
        var organization = Guid.NewGuid();
        var user = Guid.NewGuid();
        var site = Guid.NewGuid();
        var assignments = new[]
        {
            RoleAssignment.Create(organization, user, Role.Guard, RoleScope.Organization, null, null, Now),
            RoleAssignment.Create(organization, user, Role.Administrator, RoleScope.Site(site), null, null, Now),
            RoleAssignment.Create(organization, user, Role.Administrator, RoleScope.Organization, Now.AddDays(1), null, Now)
        };

        var grants = PermissionCatalog.Grants(assignments, Now);

        Assert.Contains((Permissions.SitesRead, (Guid?)null), grants);
        Assert.Contains((Permissions.SitesManage, (Guid?)site), grants);
        Assert.DoesNotContain(grants, g => g.Permission == Permissions.SitesManage && g.SiteId is null);
        Assert.Equal(grants.Count, grants.Distinct().Count());
    }

    [Fact]
    public void Site_access_is_all_sites_with_organization_scope_and_a_list_otherwise()
    {
        var site = Guid.NewGuid();
        var context = new RequestContext();
        context.SetOrganization(Guid.NewGuid(),
        [
            new PermissionGrant(Permissions.SitesRead, null),
            new PermissionGrant(Permissions.SitesManage, site)
        ]);
        var permissions = new CurrentPermissions(context);

        Assert.True(permissions.SitesFor(Permissions.SitesRead).AllSites);
        Assert.Equal([site], permissions.SitesFor(Permissions.SitesManage).SiteIds);
        Assert.False(permissions.SitesFor(Permissions.SitesManage).Allows(Guid.NewGuid()));
        Assert.False(permissions.HasAtOrganization(Permissions.SitesManage));
        Assert.True(permissions.Has(Permissions.SitesManage));
        Assert.False(permissions.SitesFor(Permissions.MembersRead).Any);
    }

    [Fact]
    public void Clearing_the_organization_clears_the_grants()
    {
        var context = new RequestContext();
        context.SetOrganization(Guid.NewGuid(), [new PermissionGrant(Permissions.SitesRead, null)]);

        context.SetOrganization(null, [new PermissionGrant(Permissions.SitesRead, null)]);

        Assert.Empty(context.Grants);
    }

    [Fact]
    public void Opaque_tokens_are_random_url_safe_and_hashed()
    {
        var (first, firstHash) = OpaqueTokens.Create();
        var (second, _) = OpaqueTokens.Create();

        Assert.NotEqual(first, second);
        Assert.Equal(43, first.Length);
        Assert.DoesNotContain(first, c => c is '+' or '/' or '=');
        Assert.Equal(firstHash, OpaqueTokens.Hash(first));
        Assert.Equal(32, firstHash.Length);
    }

    [Fact]
    public async Task Access_token_is_signed_with_es256_and_carries_only_identifiers()
    {
        using var factory = new NexgateFactory();
        var issuer = factory.Services.GetRequiredService<IAccessTokenIssuer>();
        var key = factory.Services.GetRequiredService<TokenSigningKey>();
        var user = Guid.NewGuid();
        var organization = Guid.NewGuid();
        var session = Guid.NewGuid();

        var token = issuer.Issue(user, organization, session);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = TokenSigningKey.Issuer,
            ValidAudience = TokenSigningKey.Audience,
            IssuerSigningKey = key.Key
        });
        var jwt = (JsonWebToken)result.SecurityToken;

        Assert.True(result.IsValid);
        Assert.Equal(SecurityAlgorithms.EcdsaSha256, jwt.Alg);
        Assert.Equal(key.Key.KeyId, jwt.Kid);
        Assert.Equal(user.ToString(), jwt.Subject);
        Assert.Equal(organization.ToString(), jwt.GetClaim(AccessClaims.Organization).Value);
        Assert.Equal(session.ToString(), jwt.GetClaim(AccessClaims.Session).Value);
        Assert.InRange((jwt.ValidTo - jwt.ValidFrom).TotalMinutes, 14.9, 15.1);
        Assert.Equal(new[] { "aud", "exp", "iat", "iss", "nbf", "org_id", "sid", "sub" }, jwt.Claims.Select(c => c.Type).Distinct().Order());
    }

    [Fact]
    public void Access_token_without_organization_has_no_organization_claim()
    {
        using var factory = new NexgateFactory();
        var issuer = factory.Services.GetRequiredService<IAccessTokenIssuer>();

        var jwt = new JsonWebToken(issuer.Issue(Guid.NewGuid(), null, Guid.NewGuid()).Value);

        Assert.False(jwt.TryGetClaim(AccessClaims.Organization, out _));
    }
}
