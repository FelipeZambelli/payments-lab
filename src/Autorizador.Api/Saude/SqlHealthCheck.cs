using Autorizador.Api.Banco;
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Autorizador.Api.Saude;

/// <summary>
/// SELECT 1 no SQL Server. Passa pela SqlConnectionFactory de propósito: com CHAOS_SQL_DELAY_MS
/// maior que o timeout do check, o /health/ready também fica lento e falha, como num banco lento de verdade.
/// </summary>
public sealed class SqlHealthCheck(SqlConnectionFactory conexoes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var conexao = await conexoes.CriarAsync(ct);
            await conexao.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: ct));
            return HealthCheckResult.Healthy("SELECT 1 ok.");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Falha ao consultar o SQL Server.", ex);
        }
    }
}
