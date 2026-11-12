using Autorizador.Api.Infra;
using Microsoft.Data.SqlClient;

namespace Autorizador.Api.Banco;

/// <summary>Ponto único de criação de conexões, e por isso o lugar do chaos de latência SQL.</summary>
public sealed class SqlConnectionFactory(string connectionString, ChaosOptions chaos)
{
    /// <summary>
    /// Conexão nova (fechada; o Dapper abre e fecha). Cada uso faz uma única query, então o atraso
    /// de CHAOS_SQL_DELAY_MS aqui equivale a "um atraso antes de cada query".
    /// </summary>
    public async Task<SqlConnection> CriarAsync(CancellationToken ct)
    {
        if (chaos.AtrasoSql > TimeSpan.Zero)
            await Task.Delay(chaos.AtrasoSql, ct);

        return new SqlConnection(connectionString);
    }
}
