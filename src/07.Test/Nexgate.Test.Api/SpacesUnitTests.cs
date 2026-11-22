using Nexgate.Domain.Spaces;
using Nexgate.Service.Spaces.Contracts;

namespace Nexgate.Test.Api;

/// <summary>Reglas del árbol de espacios y de los puntos de acceso, sin base de datos.</summary>
public class SpacesUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SiteId = Guid.NewGuid();

    private static Space Root(Guid? site = null) => Space.Create(site ?? SiteId, null, SpaceType.Site, "Edificio", false, Now);

    [Fact]
    public void The_root_has_depth_zero_and_its_own_label_as_path()
    {
        var root = Root();

        Assert.True(root.IsRoot);
        Assert.Equal(0, root.Depth);
        Assert.Equal(Space.Label(root.Id), root.Path);
        Assert.Equal([root.Id], root.PathIds);
    }

    [Fact]
    public void A_child_extends_the_path_of_its_parent()
    {
        var root = Root();
        var tower = Space.Create(SiteId, root, SpaceType.Building, "Torre A", false, Now);
        var floor = Space.Create(SiteId, tower, SpaceType.Floor, "Piso 5", false, Now);

        Assert.Equal(2, floor.Depth);
        Assert.Equal(tower.Id, floor.ParentId);
        Assert.Equal([root.Id, tower.Id, floor.Id], floor.PathIds);
    }

    [Theory]
    [InlineData(SpaceType.Building, true)]
    [InlineData(SpaceType.Site, false)]
    public void Only_the_root_has_the_site_type(SpaceType type, bool isRoot)
    {
        var root = Root();

        var error = Assert.Throws<DomainValidationException>(() =>
            Space.Create(SiteId, isRoot ? null : root, type, "X", false, Now));

        Assert.Equal("Type", error.Field);
    }

    [Fact]
    public void A_parent_of_another_site_is_rejected()
    {
        var error = Assert.Throws<DomainValidationException>(() =>
            Space.Create(SiteId, Root(Guid.NewGuid()), SpaceType.Building, "Torre", false, Now));

        Assert.Equal("ParentId", error.Field);
    }

    [Fact]
    public void The_tree_cannot_be_deeper_than_the_limit()
    {
        var current = Root();
        for (var depth = 1; depth <= SpaceLimits.MaxDepth; depth++)
        {
            current = Space.Create(SiteId, current, SpaceType.Zone, $"Nivel {depth}", false, Now);
        }

        Assert.Equal(SpaceLimits.MaxDepth, current.Depth);
        var error = Assert.Throws<DomainValidationException>(() => Space.Create(SiteId, current, SpaceType.Zone, "Demasiado", false, Now));
        Assert.Equal("ParentId", error.Field);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_name_is_required(string? name)
    {
        var error = Assert.Throws<DomainValidationException>(() => Space.Create(SiteId, null, SpaceType.Site, name!, false, Now));
        Assert.Equal("Name", error.Field);
    }

    [Fact]
    public void Updating_keeps_the_type_rule_of_the_root()
    {
        var root = Root();
        var tower = Space.Create(SiteId, root, SpaceType.Building, "Torre", false, Now);

        Assert.Equal("Type", Assert.Throws<DomainValidationException>(() => root.Update("Edificio", SpaceType.Building, false, Now)).Field);
        Assert.Equal("Type", Assert.Throws<DomainValidationException>(() => tower.Update("Torre", SpaceType.Site, false, Now)).Field);

        tower.Update(" Torre Norte ", SpaceType.Zone, true, Now);
        Assert.Equal(("Torre Norte", SpaceType.Zone, true), (tower.Name, tower.Type, tower.IsCommon));
    }

    [Fact]
    public void Tree_order_puts_each_space_after_its_parent_and_siblings_by_name()
    {
        var root = Root();
        var towerB = Space.Create(SiteId, root, SpaceType.Building, "Torre B", false, Now);
        var towerA = Space.Create(SiteId, root, SpaceType.Building, "torre a", false, Now);
        var floor2 = Space.Create(SiteId, towerA, SpaceType.Floor, "Piso 2", false, Now);
        var floor1 = Space.Create(SiteId, towerA, SpaceType.Floor, "Piso 1", false, Now);
        var pool = Space.Create(SiteId, root, SpaceType.CommonArea, "Pileta", true, Now);

        var ordered = Space.InTreeOrder([floor2, pool, towerB, floor1, root, towerA]);

        Assert.Equal([root.Id, pool.Id, towerA.Id, floor1.Id, floor2.Id, towerB.Id], ordered.Select(s => s.Id));
    }

    [Fact]
    public void An_access_point_cannot_enter_from_its_own_target()
    {
        var root = Root();

        var error = Assert.Throws<DomainValidationException>(() =>
            AccessPoint.Create(SiteId, "Portón", AccessPointType.Gate, root, root, FailMode.FailSecure, Now));

        Assert.Equal("FromSpaceId", error.Field);
    }

    [Fact]
    public void An_access_point_needs_spaces_of_its_site()
    {
        var foreign = Root(Guid.NewGuid());
        var root = Root();

        Assert.Equal("ToSpaceId", Assert.Throws<DomainValidationException>(() =>
            AccessPoint.Create(SiteId, "Portón", AccessPointType.Gate, foreign, null, FailMode.FailSafe, Now)).Field);
        Assert.Equal("FromSpaceId", Assert.Throws<DomainValidationException>(() =>
            AccessPoint.Create(SiteId, "Portón", AccessPointType.Gate, root, foreign, FailMode.FailSafe, Now)).Field);
    }

    [Fact]
    public void Public_names_round_trip()
    {
        foreach (var name in SpaceTypeNames.All)
        {
            Assert.True(SpaceNames.TryParseSpaceType(name, out var type));
            Assert.Equal(name, SpaceNames.ToName(type));
        }

        foreach (var name in AccessPointTypeNames.All)
        {
            Assert.True(SpaceNames.TryParseAccessPointType(name, out var type));
            Assert.Equal(name, SpaceNames.ToName(type));
        }

        foreach (var name in FailModeNames.All)
        {
            Assert.True(SpaceNames.TryParseFailMode(name, out var mode));
            Assert.Equal(name, SpaceNames.ToName(mode));
        }

        Assert.False(SpaceNames.TryParseFailMode(null, out _));
    }
}
