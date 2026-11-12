using Autorizador.Api.Mensageria;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Autorizador.Api.Saude;

/// <summary>
/// Verifica a conexão e o canal que a API usa para publicar. Se o broker cair, o RabbitMQ.Client tenta
/// reconectar sozinho (automatic recovery); enquanto isso o ready fica Unhealthy.
/// </summary>
public sealed class RabbitMqHealthCheck(PublicadorRabbitMq publicador) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        Task.FromResult(publicador.Conectado
            ? HealthCheckResult.Healthy("Conexão e canal de publicação abertos.")
            : HealthCheckResult.Unhealthy("Sem conexão com o RabbitMQ."));
}
