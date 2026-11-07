namespace Nexgate.ArchitectureTests;

public class ArchitectureTests
{
    [Fact]
    public void The_solution_discovers_modules_and_core_projects()
    {
        // Evita que las reglas pasen "en vacío" si la estructura cambia sin que nos demos cuenta.
        var projects = ProjectGraph.LoadSolution();

        Assert.Contains(projects, p => p.Name == "Nexgate.Host");
        Assert.Contains(projects, p => p.Name == "Nexgate.Workers");
        Assert.Contains(projects, p => p.Name == "Nexgate.BuildingBlocks");
        Assert.Contains(projects, p => p is { Module: "Tenancy", Layer: "Contracts" });
        Assert.Contains(projects, p => p is { Module: "Tenancy", Layer: "Domain" });
        Assert.Contains(projects, p => p is { Module: "Tenancy", Layer: "Application" });
        Assert.Contains(projects, p => p is { Module: "Tenancy", Layer: "Infrastructure" });
    }

    [Fact]
    public void The_solution_respects_the_dependency_rules()
    {
        var violations = ArchitectureRules.Violations(ProjectGraph.LoadSolution());

        Assert.True(violations.Count == 0, "Violaciones de arquitectura:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void A_module_referencing_another_modules_internals_is_rejected()
    {
        var projects = new[]
        {
            Module("Billing", "Infrastructure", refs: ["Access.Domain"]),
            Module("Access", "Domain"),
        };

        var violations = ArchitectureRules.Violations(projects);

        Assert.Contains(violations, v => v.Contains("Billing.Infrastructure") && v.Contains("Access.Contracts"));
    }

    [Fact]
    public void A_module_referencing_another_modules_contracts_is_accepted()
    {
        var projects = new[]
        {
            Module("Billing", "Application", refs: ["Access.Contracts"]),
            Module("Access", "Contracts"),
        };

        Assert.Empty(ArchitectureRules.Violations(projects));
    }

    [Theory]
    [InlineData("Sample.Domain", "Sample.Infrastructure")]
    [InlineData("Sample.Domain", "Sample.Application")]
    [InlineData("Sample.Application", "Sample.Infrastructure")]
    [InlineData("Sample.Contracts", "Sample.Domain")]
    public void Inward_layers_cannot_depend_on_outer_layers(string from, string to)
    {
        var f = from.Split('.');
        var t = to.Split('.');
        var projects = new[]
        {
            Module(f[0], f[1], refs: [to]),
            Module(t[0], t[1]),
        };

        Assert.NotEmpty(ArchitectureRules.Violations(projects));
    }

    [Fact]
    public void BuildingBlocks_cannot_depend_on_modules_or_processes()
    {
        var projects = new[]
        {
            new ProjectInfo("Nexgate.BuildingBlocks", null, null, ["Sample.Contracts", "Nexgate.Host"], [], []),
            Module("Sample", "Contracts"),
            new ProjectInfo("Nexgate.Host", null, null, [], [], []),
        };

        Assert.Equal(2, ArchitectureRules.Violations(projects).Count);
    }

    [Fact]
    public void Domain_and_Contracts_cannot_use_external_packages_or_frameworks()
    {
        var projects = new[]
        {
            new ProjectInfo("Sample.Domain", "Sample", "Domain", [], ["Npgsql"], []),
            new ProjectInfo("Sample.Contracts", "Sample", "Contracts", [], [], ["Microsoft.AspNetCore.App"]),
        };

        Assert.Equal(2, ArchitectureRules.Violations(projects).Count);
    }

    private static ProjectInfo Module(string module, string layer, string[]? refs = null) =>
        new($"{module}.{layer}", module, layer, refs ?? [], [], []);
}
