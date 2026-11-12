using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Nexgate.Test.Architecture;

/// <summary>
/// Los controllers solo usan los contratos públicos de los módulos (D3 de adopt-smt-layout-and-controllers):
/// los tipos de sus constructores, parámetros de acciones y retornos declarados deben venir de un ensamblado
/// *.Contracts, de Nexgate.Common.* o del framework. El .csproj de la API referencia las capas internas solo
/// para registrarlas en DI, así que esta regla se verifica por reflexión.
/// </summary>
public static class ControllerRules
{
    public static IReadOnlyList<string> Violations(IEnumerable<Type> controllers)
    {
        var violations = new List<string>();

        foreach (var controller in controllers)
        {
            var usages = controller.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => (Where: $"constructor ({p.Name})", p.ParameterType))
                .Concat(Actions(controller).SelectMany(action => action.GetParameters()
                    .Select(p => (Where: $"{action.Name} ({p.Name})", p.ParameterType))
                    .Append((Where: $"{action.Name} (retorno)", action.ReturnType))));

            foreach (var (where, declared) in usages)
            {
                // El propio controller puede aparecer como argumento genérico (por ejemplo, ILogger<SampleController>).
                foreach (var type in Expand(declared).Where(t => t != controller && IsForbidden(t)).Distinct())
                {
                    violations.Add($"{controller.Name}.{where} usa {type.FullName} de {type.Assembly.GetName().Name}: un controller solo puede usar contratos públicos.");
                }
            }
        }

        return violations;
    }

    public static IEnumerable<Type> ControllersIn(Assembly assembly) =>
        assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t));

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);

    /// <summary>El tipo y todos los que contiene (elementos de arrays, argumentos genéricos como Task&lt;T&gt;).</summary>
    private static IEnumerable<Type> Expand(Type type)
    {
        if (type.HasElementType)
        {
            return Expand(type.GetElementType()!);
        }

        return type.IsGenericType
            ? type.GetGenericArguments().SelectMany(Expand).Prepend(type)
            : [type];
    }

    private static bool IsForbidden(Type type)
    {
        var assembly = type.Assembly.GetName().Name ?? string.Empty;
        return assembly.StartsWith("Nexgate.", StringComparison.Ordinal)
            && !assembly.EndsWith(".Contracts", StringComparison.Ordinal)
            && !assembly.StartsWith("Nexgate.Common.", StringComparison.Ordinal);
    }
}
