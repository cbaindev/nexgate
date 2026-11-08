using Microsoft.EntityFrameworkCore;
using Nexgate.BuildingBlocks.Persistence;
using Nexgate.BuildingBlocks.Tenancy;
using Nexgate.Modules.Identity.Domain;

namespace Nexgate.Modules.Identity.Infrastructure;

/// <summary>
/// Schema <c>identity</c>. Las políticas RLS y las funciones restringidas están en la migración inicial
/// (design, decisiones 5 y 6).
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ICurrentOrganization currentOrganization)
    : NexgateDbContext(options, currentOrganization)
{
    public const string Schema = "identity";
    public const string NormalizedEmailIndex = "ux_user_account_normalized_email";

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<UserAccount>(account =>
        {
            account.ToTable("user_account");
            account.HasKey(a => a.Id);
            account.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            account.Property(a => a.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            account.Property(a => a.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
            account.Property(a => a.PasswordHash).HasColumnName("password_hash").IsRequired();
            account.Property(a => a.Status).HasColumnName("status").HasConversion(LowerEnum<AccountStatus>()).HasMaxLength(20).IsRequired();
            account.Property(a => a.PlatformOperator).HasColumnName("platform_operator");
            account.Property(a => a.CreatedAt).HasColumnName("created_at");
            account.HasIndex(a => a.NormalizedEmail).IsUnique().HasDatabaseName(NormalizedEmailIndex);
        });

        modelBuilder.Entity<Membership>(membership =>
        {
            membership.ToTable("membership");
            membership.HasKey(m => m.Id);
            membership.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
            membership.IsTenantOwned();
            membership.Property(m => m.UserId).HasColumnName("user_id");
            membership.Property(m => m.CreatedAt).HasColumnName("created_at");
            membership.HasAlternateKey(m => new { m.OrganizationId, m.UserId }).HasName("ux_membership_organization_user");
            membership.HasIndex(m => m.UserId).HasDatabaseName("ix_membership_user_id");
            membership.HasOne<UserAccount>().WithMany().HasForeignKey(m => m.UserId)
                .HasConstraintName("fk_membership_user").OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoleAssignment>(assignment =>
        {
            assignment.ToTable("role_assignment");
            assignment.HasKey(r => r.Id);
            assignment.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
            assignment.IsTenantOwned();
            assignment.Property(r => r.UserId).HasColumnName("user_id");
            assignment.Property(r => r.Role).HasColumnName("role").HasConversion(LowerEnum<Role>()).HasMaxLength(30).IsRequired();
            assignment.Property(r => r.ScopeType).HasColumnName("scope_type").HasConversion(LowerEnum<ScopeType>()).HasMaxLength(20).IsRequired();
            assignment.Property(r => r.ScopeId).HasColumnName("scope_id");
            assignment.Property(r => r.ValidFrom).HasColumnName("valid_from");
            assignment.Property(r => r.ValidUntil).HasColumnName("valid_until");
            assignment.Property(r => r.CreatedAt).HasColumnName("created_at");
            assignment.Ignore(r => r.Scope);
            assignment.HasIndex(r => new { r.OrganizationId, r.UserId }).HasDatabaseName("ix_role_assignment_organization_user");
            // Quitar la membresía quita sus asignaciones.
            assignment.HasOne<Membership>().WithMany()
                .HasForeignKey(r => new { r.OrganizationId, r.UserId })
                .HasPrincipalKey(m => new { m.OrganizationId, m.UserId })
                .HasConstraintName("fk_role_assignment_membership").OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Invitation>(invitation =>
        {
            invitation.ToTable("invitation");
            invitation.HasKey(i => i.Id);
            invitation.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
            invitation.IsTenantOwned();
            invitation.Property(i => i.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
            invitation.Property(i => i.Role).HasColumnName("role").HasConversion(LowerEnum<Role>()).HasMaxLength(30).IsRequired();
            invitation.Property(i => i.ScopeType).HasColumnName("scope_type").HasConversion(LowerEnum<ScopeType>()).HasMaxLength(20).IsRequired();
            invitation.Property(i => i.ScopeId).HasColumnName("scope_id");
            invitation.Property(i => i.TokenHash).HasColumnName("token_hash").IsRequired();
            invitation.Property(i => i.ExpiresAt).HasColumnName("expires_at");
            invitation.Property(i => i.AcceptedAt).HasColumnName("accepted_at");
            invitation.Property(i => i.AcceptedBy).HasColumnName("accepted_by");
            invitation.Property(i => i.CreatedBy).HasColumnName("created_by");
            invitation.Property(i => i.CreatedAt).HasColumnName("created_at");
            invitation.Ignore(i => i.Scope);
            invitation.HasIndex(i => i.TokenHash).IsUnique().HasDatabaseName("ux_invitation_token_hash");
            invitation.HasIndex(i => i.OrganizationId).HasDatabaseName("ix_invitation_organization_id");
        });

        modelBuilder.Entity<RefreshSession>(session =>
        {
            session.ToTable("refresh_session");
            session.HasKey(s => s.Id);
            session.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            session.Property(s => s.UserId).HasColumnName("user_id");
            session.Property(s => s.ActiveOrganizationId).HasColumnName("active_organization_id");
            session.Property(s => s.CreatedAt).HasColumnName("created_at");
            session.Property(s => s.ExpiresAt).HasColumnName("expires_at");
            session.Property(s => s.RevokedAt).HasColumnName("revoked_at");
            session.HasIndex(s => s.UserId).HasDatabaseName("ix_refresh_session_user_id");
            session.HasOne<UserAccount>().WithMany().HasForeignKey(s => s.UserId)
                .HasConstraintName("fk_refresh_session_user").OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(token =>
        {
            token.ToTable("refresh_token");
            token.HasKey(t => t.Id);
            token.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
            token.Property(t => t.SessionId).HasColumnName("session_id");
            token.Property(t => t.UserId).HasColumnName("user_id");
            token.Property(t => t.TokenHash).HasColumnName("token_hash").IsRequired();
            token.Property(t => t.CreatedAt).HasColumnName("created_at");
            token.Property(t => t.UsedAt).HasColumnName("used_at");
            token.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ux_refresh_token_token_hash");
            token.HasIndex(t => t.SessionId).HasDatabaseName("ix_refresh_token_session_id");
            token.HasOne<RefreshSession>().WithMany().HasForeignKey(t => t.SessionId)
                .HasConstraintName("fk_refresh_token_session").OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, string> LowerEnum<T>() where T : struct, Enum =>
        new(value => ToSnake(value.ToString()), value => Enum.Parse<T>(value.Replace("_", string.Empty), true));

    private static string ToSnake(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
