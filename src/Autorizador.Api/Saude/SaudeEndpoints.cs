using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Autorizador.Api.Saude;

public static class SaudeEndpoints
{
    private const string TagReady = "ready";

    public static IServiceCollection AddSaude(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<SqlHealthCheck>("sqlserver", tags: [TagReady], timeout: TimeSpan.FromSeconds(3))
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [TagReady]);
        return services;
    }

    /// <summary>
    /// /health/live: o processo está de pé e respondendo HTTP (não olha dependências; se falhar, reinicie).
    /// /health/ready: SQL e RabbitMQ ok, pode receber tráfego. 200 se Healthy, 503 se não.
    /// </summary>
    public static IEndpointRouteBuilder MapSaude(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = EscreverAsync,
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(TagReady),
            ResponseWriter = EscreverAsync,
        });
        return app;
    }

    private static Task EscreverAsync(HttpContext contexto, HealthReport relatorio) =>
        contexto.Response.WriteAsJsonAsync(new
        {
            status = relatorio.Status.ToString(),
            duracaoMs = Math.Round(relatorio.TotalDuration.TotalMilliseconds, 1),
            dependencias = relatorio.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    descricao = e.Value.Description,
                    erro = e.Value.Exception?.Message,
                    duracaoMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                }),
        });
}
