using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nexgate.Api.Rest.Extensions;

namespace Nexgate.Test.Architecture;

public class ArchitectureTests
{
    [Fact]
    public void The_solution_discovers_modules_and_core_projects()
    {
        // Evita que las reglas pasen "en vacío" si la estructura cambia sin que nos demos cuenta.
        var projects = ProjectGraph.LoadSolution();

        Assert.Contains(projects, p => p.Name == "Nexgate.Api.Rest");
        Assert.Contains(projects, p => p.Name == "Nexgate.Api.Workers");
        Assert.Contains(projects, p => p.Name == "Nexgate.Common.BuildingBlocks");
        Assert.Contains(projects, p => p is { Module: "Sample", Layer: "Contracts" });
        Assert.Contains(projects, p => p is { Module: "Sample", Layer: "Service" });
        Assert.Contains(projects, p => p is { Module: "Sample", Layer: "Domain" });
        Assert.DoesNotContain(projects, p => p.Name.StartsWith("Nexgate.Test.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("src/03.Service/Nexgate.Service.Access/Nexgate.Service.Access.csproj", "Access", "Service")]
    [InlineData("src/03.Service/Nexgate.Service.Access.Contracts/Nexgate.Service.Access.Contracts.csproj", "Access", "Contracts")]
    [InlineData("src/04.Data/Nexgate.Data.Access/Nexgate.Data.Access.csproj", "Access", "Data")]
    [InlineData("src/05.Domain/Nexgate.Domain.Access/Nexgate.Domain.Access.csproj", "Access", "Domain")]
    [InlineData("src/02.Api/Nexgate.Api.Rest/Nexgate.Api.Rest.csproj", null, null)]
    [InlineData("src/06.Common/Nexgate.Common.BuildingBlocks/Nexgate.Common.BuildingBlocks.csproj", null, null)]
    [InlineData("src/05.Domain/Nexgate.Service.Access/Nexgate.Service.Access.csproj", null, null)]
    public void Projects_are_classified_by_folder_and_name(string path, string? module, string? layer)
    {
        Assert.Equal((module, layer), ProjectGraph.Classify("C:/repo/" + path));
    }

    [Fact]
    public void The_solution_respects_the_dependency_rules()
    {
        var violations = ArchitectureRules.Violations(ProjectGraph.LoadSolution());

        Assert.True(violations.Count == 0, "Violaciones de arquitectura:\n" + string.Join("\n", violations));
    }

    // ---- Entre módulos ----

    [Fact]
    public void A_module_referencing_another_modules_internals_is_rejected()
    {
        var projects = new[]
        {
            Module("Billing", "Service", refs: [Name("Access", "Domain")]),
            Module("Access", "Domain"),
        };

        var violations = ArchitectureRules.Violations(projects);

        Assert.Contains(violations, v => v.Contains("Nexgate.Service.Billing") && v.Contains("Nexgate.Service.Access.Contracts"));
    }

    [Fact]
    public void The_service_layer_can_use_another_modules_contracts()
    {
        var projects = new[]
        {
            Module("Billing", "Service", refs: [Name("Access", "Contracts")]),
            Module("Access", "Contracts"),
        };

        Assert.Empty(ArchitectureRules.Violations(projects));
    }

    [Theory]
    [InlineData("Data")]
    [InlineData("Domain")]
    [InlineData("Contracts")]
    public void Only_the_service_layer_can_use_another_modules_contracts(string layer)
    {
        var projects = new[]
        {
            Module("Billing", layer, refs: [Name("Access", "Contracts")]),
            Module("Access", "Contracts"),
        };

        Assert.NotEmpty(ArchitectureRules.Violations(projects));
    }

    // ---- Dentro de un módulo: Api → Service → Data → Domain ----

    [Theory]
    [InlineData("Service", "Contracts")]
    [InlineData("Service", "Domain")]
    [InlineData("Service", "Data")]
    [InlineData("Data", "Domain")]
    public void Allowed_layer_dependencies_are_accepted(string from, string to)
    {
        Assert.Empty(ArchitectureRules.Violations(SameModule(from, to)));
    }

    [Theory]
    [InlineData("Domain", "Data")]
    [InlineData("Domain", "Service")]
    [InlineData("Domain", "Contracts")]
    [InlineData("Data", "Service")]
    [InlineData("Data", "Contracts")]
    [InlineData("Contracts", "Domain")]
    [InlineData("Contracts", "Service")]
    public void Layer_dependencies_against_the_allowed_direction_are_rejected(string from, string to)
    {
        Assert.NotEmpty(ArchitectureRules.Violations(SameModule(from, to)));
    }

    [Theory]
    [InlineData("Domain", false)]
    [InlineData("Contracts", false)]
    [InlineData("Data", true)]
    [InlineData("Service", true)]
    public void Only_service_and_data_can_use_Common(string layer, bool allowed)
    {
        var projects = new[]
        {
            Module("Sample", layer, refs: ["Nexgate.Common.BuildingBlocks"]),
            new ProjectInfo("Nexgate.Common.BuildingBlocks", null, null, [], [], []),
        };

        Assert.Equal(allowed, ArchitectureRules.Violations(projects).Count == 0);
    }

    [Theory]
    [InlineData("Service", "Nexgate.Api.Rest")]
    [InlineData("Data", "Nexgate.Api.Workers")]
    public void A_module_cannot_depend_on_the_processes(string layer, string process)
    {
        var projects = new[]
        {
            Module("Sample", layer, refs: [process]),
            new ProjectInfo(process, null, null, [], [], []),
        };

        Assert.NotEmpty(ArchitectureRules.Violations(projects));
    }

    [Fact]
    public void The_processes_can_reference_any_layer_to_compose_the_modules()
    {
        var projects = new[]
        {
            new ProjectInfo("Nexgate.Api.Rest", null, null,
                [Name("Sample", "Service"), Name("Sample", "Data"), Name("Sample", "Contracts"), "Nexgate.Common.BuildingBlocks"], [], []),
            Module("Sample", "Service"),
            Module("Sample", "Data"),
            Module("Sample", "Contracts"),
            new ProjectInfo("Nexgate.Common.BuildingBlocks", null, null, [], [], []),
        };

        Assert.Empty(ArchitectureRules.Violations(projects));
    }

    // ---- Common ----

    [Fact]
    public void Common_cannot_depend_on_modules_or_processes()
    {
        var projects = new[]
        {
            new ProjectInfo("Nexgate.Common.BuildingBlocks", null, null, [Name("Sample", "Contracts"), "Nexgate.Api.Rest"], [], []),
            Module("Sample", "Contracts"),
            new ProjectInfo("Nexgate.Api.Rest", null, null, [], [], []),
        };

        Assert.Equal(2, ArchitectureRules.Violations(projects).Count);
    }

    [Fact]
    public void Common_cannot_reference_the_web_framework()
    {
        var projects = new[]
        {
            new ProjectInfo("Nexgate.Common.BuildingBlocks", null, null, [], [], ["Microsoft.AspNetCore.App"]),
        };

        Assert.Contains(ArchitectureRules.Violations(projects), v => v.Contains("Microsoft.AspNetCore.App"));
    }

    [Fact]
    public void Domain_and_Contracts_cannot_use_external_packages_or_frameworks()
    {
        var projects = new[]
        {
            new ProjectInfo(Name("Sample", "Domain"), "Sample", "Domain", [], ["Npgsql"], []),
            new ProjectInfo(Name("Sample", "Contracts"), "Sample", "Contracts", [], [], ["Microsoft.AspNetCore.App"]),
        };

        Assert.Equal(2, ArchitectureRules.Violations(projects).Count);
    }

    // ---- Controllers solo con contratos (D3) ----

    [Fact]
    public void The_api_controllers_only_use_module_contracts()
    {
        var controllers = ControllerRules.ControllersIn(typeof(ModuleExtensions).Assembly).ToList();

        Assert.NotEmpty(controllers);
        var violations = ControllerRules.Violations(controllers);
        Assert.True(violations.Count == 0, "Violaciones de arquitectura:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void A_controller_using_a_module_internal_type_is_rejected()
    {
        var violations = ControllerRules.Violations([typeof(ControllerUsingInternals)]);

        // Constructor, parámetro de la acción y tipo genérico del retorno.
        Assert.Equal(3, violations.Count);
        Assert.All(violations, v => Assert.Contains(nameof(InternalModuleType), v));
    }

    [Fact]
    public void A_controller_using_only_framework_types_is_accepted()
    {
        Assert.Empty(ControllerRules.Violations([typeof(ControllerUsingFrameworkTypes)]));
    }

    /// <summary>Nombre de proyecto según la convención: Nexgate.Service.X, Nexgate.Service.X.Contracts, Nexgate.Data.X, Nexgate.Domain.X.</summary>
    private static string Name(string module, string layer) => layer switch
    {
        "Contracts" => $"Nexgate.Service.{module}.Contracts",
        _ => $"Nexgate.{layer}.{module}"
    };

    private static ProjectInfo Module(string module, string layer, string[]? refs = null) =>
        new(Name(module, layer), module, layer, refs ?? [], [], []);

    private static ProjectInfo[] SameModule(string from, string to) =>
    [
        Module("Sample", from, refs: [Name("Sample", to)]),
        Module("Sample", to),
    ];
}

/// <summary>
/// Representa un tipo interno de un módulo: vive en un ensamblado Nexgate.* que no es Contracts ni Common
/// (este mismo ensamblado de pruebas).
/// </summary>
public sealed class InternalModuleType;

[ApiController]
public sealed class ControllerUsingInternals(InternalModuleType dependency) : ControllerBase
{
    public Task<ActionResult<InternalModuleType>> Get(InternalModuleType input) =>
        Task.FromResult<ActionResult<InternalModuleType>>(input ?? dependency);
}

[ApiController]
public sealed class ControllerUsingFrameworkTypes(ILogger<ControllerUsingFrameworkTypes> logger) : ControllerBase
{
    public ActionResult<string> Get(int id)
    {
        logger.LogInformation("Get {Id}", id);
        return id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
