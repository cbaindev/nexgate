using System.Xml.Linq;

namespace Nexgate.Test.Architecture;

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
    /// <summary>Carpeta de cada capa de módulo y el segmento de nombre que la identifica (Nexgate.&lt;Segmento&gt;.&lt;Modulo&gt;).</summary>
    private static readonly (string Folder, string Segment)[] ModuleFolders =
    [
        ("03.Service", "Service"),
        ("04.Data", "Data"),
        ("05.Domain", "Domain"),
    ];

    private const string TestFolder = "07.Test";

    public static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio (global.json).");
    }

    /// <summary>
    /// Lee todos los proyectos bajo src/, salvo las pruebas (src/07.Test). Los proyectos de módulo viven en
    /// src/03.Service (Nexgate.Service.&lt;Modulo&gt; y Nexgate.Service.&lt;Modulo&gt;.Contracts), src/04.Data
    /// (Nexgate.Data.&lt;Modulo&gt;) y src/05.Domain (Nexgate.Domain.&lt;Modulo&gt;).
    /// </summary>
    public static IReadOnlyList<ProjectInfo> LoadSolution()
    {
        var src = Path.Combine(FindRepositoryRoot(), "src");
        return Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Replace('\\', '/').Contains($"/src/{TestFolder}/"))
            .Select(Load)
            .ToList();
    }

    /// <summary>Deduce módulo y capa de la carpeta y del nombre; (null, null) si no es un proyecto de módulo.</summary>
    public static (string? Module, string? Layer) Classify(string csprojPath)
    {
        var path = csprojPath.Replace('\\', '/');
        var parts = Path.GetFileNameWithoutExtension(csprojPath).Split('.');

        foreach (var (folder, segment) in ModuleFolders)
        {
            if (!path.Contains($"/src/{folder}/") || parts.Length < 3 || parts[0] != "Nexgate" || parts[1] != segment)
            {
                continue;
            }

            return (parts.Length, segment) switch
            {
                (3, _) => (parts[2], segment),
                (4, "Service") when parts[3] == "Contracts" => (parts[2], "Contracts"),
                _ => (null, null)
            };
        }

        return (null, null);
    }

    public static ProjectInfo Load(string csprojPath)
    {
        var name = Path.GetFileNameWithoutExtension(csprojPath);
        var doc = XDocument.Load(csprojPath);
        var (module, layer) = Classify(csprojPath);

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
