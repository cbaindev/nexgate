using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexgate.BuildingBlocks.Tenancy;

namespace Nexgate.BuildingBlocks.Persistence;

/// <summary>
/// Base de los DbContext de los módulos. Es la segunda barrera de aislamiento (la primera es RLS):
/// filtra por la organización activa las entidades marcadas con <see cref="TenantModelExtensions.IsTenantOwned{T}"/>,
/// les asigna la organización al crearlas y rechaza cualquier cambio de organización.
/// </summary>
public abstract class NexgateDbContext(DbContextOptions options, ICurrentOrganization currentOrganization) : DbContext(options)
{
    internal const string TenantOwnedAnnotation = "Nexgate:TenantOwned";
    public const string OrganizationIdProperty = "OrganizationId";

    /// <summary>Organización activa. La usan los filtros globales (se evalúa en cada consulta).</summary>
    public Guid? CurrentOrganizationId => currentOrganization.Id;

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureModel(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => t.FindAnnotation(TenantOwnedAnnotation) is not null)
                     .ToList())
        {
            var entity = Expression.Parameter(entityType.ClrType, "e");
            var organizationId = Expression.Convert(Expression.Property(entity, OrganizationIdProperty), typeof(Guid?));
            var current = Expression.Property(Expression.Constant(this), nameof(CurrentOrganizationId));
            var filter = Expression.Lambda(Expression.Equal(organizationId, current), entity);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    /// <summary>Configuración del modelo del módulo (tablas, columnas, índices).</summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTenantRules()
    {
        foreach (var entry in ChangeTracker.Entries()
                     .Where(e => e.Metadata.FindAnnotation(TenantOwnedAnnotation) is not null)
                     .ToList())
        {
            var property = entry.Property(OrganizationIdProperty);
            switch (entry.State)
            {
                case EntityState.Added:
                    var organization = CurrentOrganizationId
                        ?? throw new TenantIsolationException("No se puede crear un dato de negocio sin organización activa.");
                    var assigned = (Guid)property.CurrentValue!;
                    if (assigned != Guid.Empty && assigned != organization)
                    {
                        throw new TenantIsolationException("El dato pertenece a una organización distinta de la activa.");
                    }

                    property.CurrentValue = organization;
                    break;

                case EntityState.Modified or EntityState.Deleted:
                    if (property.IsModified || (Guid?)property.OriginalValue != CurrentOrganizationId)
                    {
                        throw new TenantIsolationException("No se puede cambiar de organización ni modificar datos de otra organización.");
                    }

                    break;
            }
        }
    }
}

public static class TenantModelExtensions
{
    /// <summary>
    /// Marca la entidad como dato de negocio de una organización: requiere una propiedad <c>Guid OrganizationId</c>,
    /// que se mapea a la columna <c>organization_id</c>. La migración debe llamar a <c>EnableTenantIsolation</c>.
    /// </summary>
    public static EntityTypeBuilder<T> IsTenantOwned<T>(this EntityTypeBuilder<T> builder) where T : class
    {
        builder.Property<Guid>(NexgateDbContext.OrganizationIdProperty).HasColumnName("organization_id").IsRequired();
        builder.HasAnnotation(NexgateDbContext.TenantOwnedAnnotation, true);
        return builder;
    }
}

/// <summary>Intento de leer o escribir fuera de la organización activa. Es un error de programación, no del usuario.</summary>
public sealed class TenantIsolationException(string message) : InvalidOperationException(message);
