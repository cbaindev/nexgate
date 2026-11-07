namespace Nexgate.ArchitectureTests;

/// <summary>
/// Reglas de dependencia entre proyectos (ver docs/05-arquitectura-tecnica.md).
/// Devuelve una lista de violaciones legibles; vacía si todo está bien.
/// </summary>
public static class ArchitectureRules
{
    public static IReadOnlyList<string> Violations(IReadOnlyList<ProjectInfo> projects)
    {
        var byName = projects.ToDictionary(p => p.Name);
        var violations = new List<string>();

        foreach (var project in projects)
        {
            foreach (var referenceName in project.ProjectReferences)
            {
                byName.TryGetValue(referenceName, out var reference);

                // 1. Un módulo solo depende de otro módulo a través de sus Contracts.
                if (project.IsModuleProject && reference is { IsModuleProject: true }
                    && reference.Module != project.Module && reference.Layer != "Contracts")
                {
                    violations.Add($"{project.Name} referencia a {reference.Name}: un módulo solo puede depender de {reference.Module}.Contracts.");
                }

                // 2. BuildingBlocks no conoce a los módulos ni a los procesos.
                if (project.Name == "Nexgate.BuildingBlocks"
                    && (reference?.IsModuleProject == true || referenceName is "Nexgate.Host" or "Nexgate.Workers"))
                {
                    violations.Add($"{project.Name} no puede depender de {referenceName}.");
                }

                // 3. Los módulos no conocen a Host ni a Workers.
                if (project.IsModuleProject && referenceName is "Nexgate.Host" or "Nexgate.Workers")
                {
                    violations.Add($"{project.Name} no puede depender de {referenceName}.");
                }

                // 4. Contracts es la capa pública: no depende de capas internas.
                if (project.Layer == "Contracts" && reference is { IsModuleProject: true, Layer: not "Contracts" })
                {
                    violations.Add($"{project.Name} (Contracts) no puede depender de {reference.Name}.");
                }

                // 5. Domain no depende de Application ni de Infrastructure.
                if (project.Layer == "Domain" && reference is { IsModuleProject: true, Layer: "Application" or "Infrastructure" })
                {
                    violations.Add($"{project.Name} (Domain) no puede depender de {reference.Name}.");
                }

                // 6. Application no depende de Infrastructure.
                if (project.Layer == "Application" && reference is { IsModuleProject: true, Layer: "Infrastructure" })
                {
                    violations.Add($"{project.Name} (Application) no puede depender de {reference.Name}.");
                }
            }

            // 7. Domain es puro: sin framework web ni paquetes de infraestructura.
            if (project.Layer == "Domain" && (project.FrameworkReferences.Count > 0 || project.PackageReferences.Count > 0))
            {
                violations.Add($"{project.Name} (Domain) no puede referenciar frameworks ni paquetes externos.");
            }

            // 8. Contracts es liviano: sin frameworks ni paquetes externos.
            if (project.Layer == "Contracts" && (project.FrameworkReferences.Count > 0 || project.PackageReferences.Count > 0))
            {
                violations.Add($"{project.Name} (Contracts) no puede referenciar frameworks ni paquetes externos.");
            }
        }

        return violations;
    }
}
