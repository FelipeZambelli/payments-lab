namespace Terminal.Worker.Configuracao;

/// <summary>
/// Configuração do Worker, lida de variáveis de ambiente "planas" (TERMINAIS, PREFETCH, API_URL,
/// CHAOS_WORKER_PAUSED). Montada uma vez no startup: valor inválido derruba o processo logo.
/// </summary>
public sealed class WorkerOptions
{
    public required IReadOnlyList<int> Terminais { get; init; }

    /// <summary>Máximo de mensagens entregues e ainda sem ack, por terminal (basic.qos por canal).</summary>
    public required ushort Prefetch { get; init; }

    /// <summary>URL base da Autorizador.Api (termina com "/").</summary>
    public required Uri ApiUrl { get; init; }

    /// <summary>CHAOS_WORKER_PAUSED: declara as filas mas não consome (as mensagens acumulam).</summary>
    public required bool Pausado { get; init; }

    public static WorkerOptions Ler(IConfiguration config)
    {
        var prefetch = config.GetValue<ushort?>("PREFETCH") ?? 10;
        if (prefetch == 0)
            throw new InvalidOperationException("PREFETCH deve ser maior que zero (0 significaria \"sem limite\").");

        var apiUrl = config["API_URL"] ?? "http://localhost:5080/";
        if (!apiUrl.EndsWith('/'))
            apiUrl += "/";

        return new WorkerOptions
        {
            Terminais = FaixaTerminais.Interpretar(config["TERMINAIS"] ?? "1-50"),
            Prefetch = prefetch,
            ApiUrl = new Uri(apiUrl),
            Pausado = config.GetValue<bool>("CHAOS_WORKER_PAUSED"),
        };
    }
}
