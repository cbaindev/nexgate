namespace Nexgate.Test.Architecture;

/// <summary>
/// Reglas de dependencia entre proyectos (ver docs/05-arquitectura-tecnica.md y la decisión D-32).
/// Sentido permitido: Api → Service → Data → Domain, con Common disponible para todos. Entre módulos, solo
/// Nexgate.Service.&lt;Modulo&gt;.Contracts. Devuelve una lista de violaciones legibles; vacía si todo está bien.
/// </summary>
public static class ArchitectureRules
{
    private const string AspNetCoreFramework = "Microsoft.AspNetCore.App";

    public static IReadOnlyList<string> Violations(IReadOnlyList<ProjectInfo> projects)
    {
        var byName = projects.ToDictionary(p => p.Name);
        var violations = new List<string>();

        foreach (var project in projects)
        {
            foreach (var referenceName in project.ProjectReferences)
            {
                var reference = byName.GetValueOrDefault(referenceName);
                if (Check(project, referenceName, reference) is { } violation)
                {
                    violations.Add(violation);
                }
            }

            // Domain y Contracts son puros: sin frameworks ni paquetes externos.
            if (project.Layer is "Domain" or "Contracts"
                && (project.FrameworkReferences.Count > 0 || project.PackageReferences.Count > 0))
            {
                violations.Add($"{project.Name} ({project.Layer}) no puede referenciar frameworks ni paquetes externos.");
            }

            // Common no depende del framework web: lo usan también los workers y los proyectos de módulo.
            if (IsCommon(project.Name) && project.FrameworkReferences.Contains(AspNetCoreFramework))
            {
                violations.Add($"{project.Name} (Common) no puede referenciar {AspNetCoreFramework}.");
            }
        }

        return violations;
    }

    private static string? Check(ProjectInfo project, string referenceName, ProjectInfo? reference)
    {
        if (IsCommon(project.Name))
        {
            return IsCommon(referenceName)
                ? null
                : $"{project.Name} no puede depender de {referenceName}: Common solo puede depender de otros proyectos Common.";
        }

        if (!project.IsModuleProject)
        {
            // Los procesos de 02.Api son la raíz de composición y pueden referenciar cualquier capa.
            return null;
        }

        if (IsCommon(referenceName))
        {
            // Domain y Contracts no referencian ningún proyecto, ni siquiera Common.
            return project.Layer is "Domain" or "Contracts"
                ? $"{project.Name} ({project.Layer}) no puede depender de {referenceName}."
                : null;
        }

        if (reference is not { IsModuleProject: true })
        {
            return $"{project.Name} no puede depender de {referenceName}: un módulo no conoce a los procesos de 02.Api.";
        }

        if (reference.Module != project.Module)
        {
            // Entre módulos, solo el contrato público y solo desde la capa de servicio.
            if (reference.Layer != "Contracts")
            {
                return $"{project.Name} referencia a {reference.Name}: un módulo solo puede depender de Nexgate.Service.{reference.Module}.Contracts.";
            }

            return project.Layer == "Service"
                ? null
                : $"{project.Name} ({project.Layer}) no puede depender de {reference.Name}: solo la capa de servicio usa contratos de otros módulos.";
        }

        return AllowedSameModule(project.Layer!).Contains(reference.Layer!)
            ? null
            : $"{project.Name} ({project.Layer}) no puede depender de {reference.Name} ({reference.Layer}).";
    }

    /// <summary>Capas del mismo módulo que cada capa puede referenciar.</summary>
    private static string[] AllowedSameModule(string layer) => layer switch
    {
        "Service" => ["Contracts", "Domain", "Data"],
        "Data" => ["Domain"],
        _ => [] // Domain y Contracts no dependen de nada.
    };

    private static bool IsCommon(string projectName) => projectName.StartsWith("Nexgate.Common.", StringComparison.Ordinal);
}
