using Nexgate.Modules.Identity.Application;

namespace Nexgate.Host;

/// <summary>Comandos administrativos del host. No exponen endpoints ni escriben datos personales en la salida.</summary>
public static class AdminCommands
{
    public const string CreatePlatformOperator = "create-platform-operator";
    public const string PasswordVariable = "NEXGATE_OPERATOR_PASSWORD";

    public static async Task<int> CreatePlatformOperatorAsync(IServiceProvider services, string[] args, TextReader input, TextWriter output)
    {
        var emailIndex = Array.IndexOf(args, "--email");
        var email = emailIndex >= 0 && emailIndex + 1 < args.Length ? args[emailIndex + 1] : null;
        if (string.IsNullOrWhiteSpace(email))
        {
            await output.WriteLineAsync($"Uso: {CreatePlatformOperator} --email <correo> (contraseña en {PasswordVariable} o por la entrada estándar)");
            return 2;
        }

        var password = Environment.GetEnvironmentVariable(PasswordVariable);
        if (string.IsNullOrEmpty(password))
        {
            await output.WriteLineAsync("Contraseña:");
            password = await input.ReadLineAsync();
        }

        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<PlatformOperatorService>().EnsureAsync(email, password, CancellationToken.None);
        if (result.Error is { } error)
        {
            await output.WriteLineAsync($"No se pudo crear el operador: {error.Message}");
            return 1;
        }

        await output.WriteLineAsync(result.Value == PlatformOperatorOutcome.Created
            ? "Operador de plataforma creado."
            : "La cuenta existente ahora es operador de plataforma (la contraseña no cambió).");
        return 0;
    }
}
