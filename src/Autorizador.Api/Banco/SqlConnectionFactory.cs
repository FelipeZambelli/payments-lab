using Microsoft.Data.SqlClient;

namespace Autorizador.Api.Banco;

/// <summary>Ponto único de criação de conexões.</summary>
/// <remarks>
/// O chaos de latência (CHAOS_SQL_DELAY_MS) não fica aqui: ele vai dentro da query, como
/// WAITFOR DELAY, no AutorizacaoRepository. Assim a espera aparece no span de SQL, e o
/// SqlHealthCheck (que também usa esta factory) continua rápido.
/// </remarks>
public sealed class SqlConnectionFactory(string connectionString)
{
    /// <summary>Conexão nova e fechada: o Dapper abre e fecha.</summary>
    public Task<SqlConnection> CriarAsync(CancellationToken ct) =>
        Task.FromResult(new SqlConnection(connectionString));
}
