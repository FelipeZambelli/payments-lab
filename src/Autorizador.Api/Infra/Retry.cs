namespace Autorizador.Api.Infra;

/// <summary>Retry simples para dependências que podem demorar a subir (SQL Server, RabbitMQ).</summary>
public static class Retry
{
    public static async Task<T> ExecutarAsync<T>(
        Func<CancellationToken, Task<T>> acao,
        ILogger logger,
        string descricao,
        CancellationToken ct,
        int tentativas = 10,
        TimeSpan? intervalo = null)
    {
        var espera = intervalo ?? TimeSpan.FromSeconds(3);

        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                return await acao(ct);
            }
            catch (Exception ex) when (tentativa < tentativas && !ct.IsCancellationRequested)
            {
                logger.LogWarning("Falha em {Descricao} (tentativa {Tentativa}/{Total}): {Erro}. Nova tentativa em {Espera}s.",
                    descricao, tentativa, tentativas, ex.Message, espera.TotalSeconds);
                await Task.Delay(espera, ct);
            }
        }
    }
}
