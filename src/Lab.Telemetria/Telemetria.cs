using System.Diagnostics;

namespace Lab.Telemetria;

/// <summary>Nomes e fontes de telemetria próprios do Lab, compartilhados entre API e Worker.</summary>
public static class Telemetria
{
    /// <summary>
    /// Nome do ActivitySource dos spans manuais (publish/process do RabbitMQ, autorizacao.criar/confirmar).
    /// O serviço que gerou o span é identificado pelo service.name (OTEL_SERVICE_NAME), não pela fonte.
    /// </summary>
    public const string NomeFonte = "PaymentsLab";

    public static readonly ActivitySource Fonte = new(NomeFonte);

    /// <summary>Registra a exceção no span (evento "exception") e marca o status como Error.</summary>
    public static void RegistrarFalha(this Activity? span, Exception ex)
    {
        if (span is null)
            return;
        span.AddException(ex);
        span.SetStatus(ActivityStatusCode.Error, ex.Message);
    }
}
