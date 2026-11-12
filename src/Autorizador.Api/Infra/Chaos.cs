namespace Autorizador.Api.Infra;

/// <summary>
/// Falhas artificiais para os experimentos do lab, lidas de variáveis de ambiente no startup:
/// CHAOS_SQL_DELAY_MS (atraso antes de cada query) e CHAOS_ERROR_RATE (fração 0..1 de respostas 500).
/// Valor inválido derruba a API no startup, em vez de ser ignorado em silêncio.
/// </summary>
public sealed record ChaosOptions(TimeSpan AtrasoSql, double TaxaErro)
{
    public bool Ativo => AtrasoSql > TimeSpan.Zero || TaxaErro > 0;

    public static ChaosOptions Ler(IConfiguration config)
    {
        var atrasoMs = config.GetValue<int?>("CHAOS_SQL_DELAY_MS") ?? 0;
        if (atrasoMs < 0)
            throw new InvalidOperationException($"CHAOS_SQL_DELAY_MS deve ser >= 0 (recebido: {atrasoMs}).");

        // O binder de configuração usa cultura invariante: o separador decimal é ponto (0.1).
        var taxa = config.GetValue<double?>("CHAOS_ERROR_RATE") ?? 0;
        if (double.IsNaN(taxa) || taxa is < 0 or > 1)
            throw new InvalidOperationException($"CHAOS_ERROR_RATE deve estar entre 0 e 1 (recebido: {taxa}).");

        return new ChaosOptions(TimeSpan.FromMilliseconds(atrasoMs), taxa);
    }
}

public static class ChaosMiddleware
{
    /// <summary>
    /// Com CHAOS_ERROR_RATE > 0, responde 500 a essa fração das requisições, antes de chegar ao endpoint
    /// (nada é gravado nem publicado). /health/* fica de fora: o objetivo é testar quem chama a API,
    /// não fazer o container parecer doente.
    /// </summary>
    public static WebApplication UseChaosDeErros(this WebApplication app, ChaosOptions chaos)
    {
        if (chaos.TaxaErro <= 0)
            return app;

        app.Use(async (contexto, proximo) =>
        {
            if (!contexto.Request.Path.StartsWithSegments("/health") && Random.Shared.NextDouble() < chaos.TaxaErro)
            {
                await TypedResults.Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Erro simulado",
                        detail: "Falha injetada por CHAOS_ERROR_RATE.")
                    .ExecuteAsync(contexto);
                return;
            }

            await proximo(contexto);
        });

        return app;
    }
}
