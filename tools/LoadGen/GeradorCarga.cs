using System.Diagnostics;
using System.Net.Http.Json;

namespace LoadGen;

/// <summary>
/// Dispara POST /autorizacoes no ritmo pedido. O ritmo é calculado pelo tempo decorrido
/// (devidas = segundos × tps), então pequenos atrasos do timer não se acumulam.
/// Se a API ficar lenta e as vagas de concorrência acabarem, o envio espera: a taxa real cai
/// e aparece nas estatísticas.
/// </summary>
public sealed class GeradorCarga(Opcoes opcoes, HttpClient http)
{
    // Bandeiras fictícias.
    private static readonly string[] Bandeiras = ["Orbita", "Cometa", "Nebula", "Aurora"];

    private readonly Estatisticas _estatisticas = new();
    private readonly SemaphoreSlim _vagas = new(opcoes.Concorrencia, opcoes.Concorrencia);

    /// <summary>Corpo do POST: exatamente os campos que a API aceita (ela rejeita campos extras).</summary>
    private sealed record NovaAutorizacao(int TerminalId, decimal Valor, string Bandeira, Guid CartaoToken, string IdempotencyKey);

    public async Task ExecutarAsync(CancellationToken ct)
    {
        var duracao = opcoes.Duracao is { } d ? $"por {d.TotalSeconds:0}s" : "até Ctrl+C";
        Console.WriteLine($"Disparando {opcoes.Tps} transações/s para os terminais 1..{opcoes.Terminais} em {opcoes.Url} ({duracao}).");

        var relogio = Stopwatch.StartNew();
        var proximoRelatorio = TimeSpan.FromSeconds(1);
        long disparadas = 0;

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var devidas = (long)(relogio.Elapsed.TotalSeconds * opcoes.Tps);
                for (; disparadas < devidas; disparadas++)
                {
                    await _vagas.WaitAsync(ct);
                    _ = EnviarAsync();
                }

                if (relogio.Elapsed >= proximoRelatorio)
                {
                    _estatisticas.ImprimirIntervalo(relogio.Elapsed);
                    proximoRelatorio += TimeSpan.FromSeconds(1);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        // Ocupar todas as vagas = esperar todas as requisições em andamento terminarem
        // (cada uma é limitada pelo timeout do HttpClient).
        Console.WriteLine("Parando: aguardando requisições em andamento...");
        for (var i = 0; i < opcoes.Concorrencia; i++)
            await _vagas.WaitAsync(CancellationToken.None);

        _estatisticas.ImprimirResumo(relogio.Elapsed);
    }

    private async Task EnviarAsync()
    {
        var inicio = Stopwatch.GetTimestamp();
        try
        {
            // Sem CancellationToken de propósito: ao parar, deixa as requisições já disparadas terminarem.
            using var resposta = await http.PostAsJsonAsync("autorizacoes", NovaTransacao());
            _estatisticas.Registrar((int)resposta.StatusCode, Stopwatch.GetElapsedTime(inicio));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _estatisticas.Registrar(status: null, Stopwatch.GetElapsedTime(inicio));
        }
        finally
        {
            _vagas.Release();
        }
    }

    private NovaAutorizacao NovaTransacao() => new(
        TerminalId: Random.Shared.Next(1, opcoes.Terminais + 1),
        Valor: Random.Shared.Next(100, 50_001) / 100m, // R$ 1,00 a R$ 500,00
        Bandeira: Bandeiras[Random.Shared.Next(Bandeiras.Length)],
        CartaoToken: Guid.NewGuid(),
        IdempotencyKey: Guid.NewGuid().ToString());
}
