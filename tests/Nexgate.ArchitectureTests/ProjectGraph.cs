using System.Xml.Linq;

namespace Nexgate.ArchitectureTests;

/// <summary>Un proyecto de la solución, tal como lo declara su .csproj.</summary>
public sealed record ProjectInfo(
    string Name,
    string? Module,
    string? Layer,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences,
    IReadOnlyList<string> FrameworkReferences)
{
    public bool IsModuleProject => Module is not null;
}

public static class ProjectGraph
{
    private static readonly string[] Layers = ["Contracts", "Domain", "Application", "Infrastructure"];

    public static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio (global.json).");
    }

    /// <summary>Lee todos los proyectos bajo src/. Los módulos viven en src/Modules/&lt;Modulo&gt;/&lt;Modulo&gt;.&lt;Capa&gt;.</summary>
    public static IReadOnlyList<ProjectInfo> LoadSolution()
    {
        var src = Path.Combine(FindRepositoryRoot(), "src");
        return Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Select(Load)
            .ToList();
    }

    public static ProjectInfo Load(string csprojPath)
    {
        var name = Path.GetFileNameWithoutExtension(csprojPath);
        var doc = XDocument.Load(csprojPath);

        string? module = null, layer = null;
        var parts = name.Split('.');
        var isUnderModules = csprojPath.Replace('\\', '/').Contains("/src/Modules/");
        if (isUnderModules && parts.Length == 2 && Layers.Contains(parts[1]))
        {
            module = parts[0];
            layer = parts[1];
        }

        static IReadOnlyList<string> Values(XDocument d, string element, string attribute) =>
            d.Descendants(element).Select(e => (string?)e.Attribute(attribute)).OfType<string>().ToList();

        var projectRefs = Values(doc, "ProjectReference", "Include")
            .Select(p => Path.GetFileNameWithoutExtension(p.Replace('\\', '/')))
            .ToList();

        return new ProjectInfo(
            name, module, layer, projectRefs,
            Values(doc, "PackageReference", "Include"),
            Values(doc, "FrameworkReference", "Include"));
    }
}
