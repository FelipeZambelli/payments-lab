using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Lab.Telemetria;

public static class TelemetriaExtensions
{
    /// <summary>
    /// Liga traces, métricas e logs do OpenTelemetry. Toda a configuração de destino vem das variáveis
    /// padrão OTEL_* (OTEL_SERVICE_NAME, OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_PROTOCOL,
    /// OTEL_RESOURCE_ATTRIBUTES), lidas pelo próprio SDK.
    ///
    /// O exporter OTLP só é registrado se OTEL_EXPORTER_OTLP_ENDPOINT estiver definido: sem a stack de
    /// observabilidade, o Lab sobe igual a antes, sem tentar exportar para lugar nenhum.
    /// </summary>
    /// <param name="tracing">Instrumentações específicas do serviço (ex.: ASP.NET Core e SqlClient na API).</param>
    /// <param name="metricas">Idem, para métricas.</param>
    public static IHostApplicationBuilder AddTelemetria(
        this IHostApplicationBuilder builder,
        Action<TracerProviderBuilder>? tracing = null,
        Action<MeterProviderBuilder>? metricas = null)
    {
        var otel = builder.Services.AddOpenTelemetry()
            .WithTracing(t =>
            {
                t.AddSource(Telemetria.NomeFonte)
                 .AddHttpClientInstrumentation();
                tracing?.Invoke(t);
            })
            .WithMetrics(m =>
            {
                // System.Runtime: meter nativo do .NET 9+ (GC, heap, threads, exceções).
                m.AddMeter("System.Runtime")
                 .AddHttpClientInstrumentation();
                metricas?.Invoke(m);
            })
            .WithLogging(configureBuilder: null, configureOptions: o =>
            {
                // Mantém o texto final e as propriedades do message template (AutorizacaoId, TerminalId...).
                o.IncludeFormattedMessage = true;
                o.IncludeScopes = true;
            });

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            otel.UseOtlpExporter();

        return builder;
    }
}
