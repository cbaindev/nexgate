namespace Nexgate.Workers;

/// <summary>
/// Proceso de segundo plano. Hoy solo informa que está vivo; acá se van a alojar el puente MQTT,
/// el compilador de políticas, el outbox y la retención de datos.
/// </summary>
public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Nexgate.Workers iniciado");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }
}
