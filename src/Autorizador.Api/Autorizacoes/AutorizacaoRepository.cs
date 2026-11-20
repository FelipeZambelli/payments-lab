using Autorizador.Api.Banco;
using Autorizador.Api.Infra;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Autorizador.Api.Autorizacoes;

public sealed class AutorizacaoRepository(SqlConnectionFactory conexoes, ChaosOptions chaos)
{
    /// <summary>
    /// CHAOS_SQL_DELAY_MS: "WAITFOR DELAY" antes de cada query de negócio. A espera acontece no
    /// servidor, então aparece na duração do span de SQL (e no db.query.text). O literal vem de um
    /// inteiro já validado em ChaosOptions, sem risco de injeção. O health check não passa por aqui.
    /// </summary>
    private readonly string _prefixoChaos = chaos.AtrasoSql > TimeSpan.Zero
        ? $"WAITFOR DELAY '{chaos.AtrasoSql:hh\\:mm\\:ss\\.fff}';\n"
        : "";

    private string Sql(string sql) => _prefixoChaos + sql;

    private const string Colunas = """
        Id, IdempotencyKey, TerminalId, Valor, Bandeira, CartaoToken,
        Status, CriadaEm, PublicadaEm, ConfirmadaEm
        """;

    // Violação de PRIMARY KEY/UNIQUE constraint (2627) ou de índice único (2601).
    private static bool EhChaveDuplicada(SqlException ex) => ex.Number is 2627 or 2601;

    public async Task<Autorizacao?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var conexao = await conexoes.CriarAsync(ct);
        return await conexao.QuerySingleOrDefaultAsync<Autorizacao>(new CommandDefinition(
            Sql($"SELECT {Colunas} FROM dbo.Autorizacoes WHERE Id = @id"),
            new { id }, cancellationToken: ct));
    }

    public async Task<Autorizacao?> ObterPorIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        await using var conexao = await conexoes.CriarAsync(ct);
        return await conexao.QuerySingleOrDefaultAsync<Autorizacao>(new CommandDefinition(
            Sql($"SELECT {Colunas} FROM dbo.Autorizacoes WHERE IdempotencyKey = @idempotencyKey"),
            new { idempotencyKey }, cancellationToken: ct));
    }

    /// <returns>false se já existe uma autorização com a mesma IdempotencyKey.</returns>
    public async Task<bool> InserirAsync(Autorizacao a, CancellationToken ct)
    {
        await using var conexao = await conexoes.CriarAsync(ct);
        try
        {
            await conexao.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO dbo.Autorizacoes
                    (Id, IdempotencyKey, TerminalId, Valor, Bandeira, CartaoToken, Status, CriadaEm)
                VALUES
                    (@Id, @IdempotencyKey, @TerminalId, @Valor, @Bandeira, @CartaoToken, @Status, @CriadaEm)
                """),
                new
                {
                    a.Id, a.IdempotencyKey, a.TerminalId, a.Valor, a.Bandeira, a.CartaoToken,
                    Status = a.Status.ToString(),
                    a.CriadaEm,
                },
                cancellationToken: ct));
            return true;
        }
        catch (SqlException ex) when (EhChaveDuplicada(ex))
        {
            return false;
        }
    }

    public async Task MarcarPublicadaAsync(Guid id, DateTimeOffset publicadaEm, CancellationToken ct)
    {
        await using var conexao = await conexoes.CriarAsync(ct);
        await conexao.ExecuteAsync(new CommandDefinition(
            Sql("UPDATE dbo.Autorizacoes SET PublicadaEm = @publicadaEm WHERE Id = @id AND PublicadaEm IS NULL"),
            new { id, publicadaEm }, cancellationToken: ct));
    }

    /// <summary>Pendente → Confirmada. Retorna a linha atualizada, ou null se não estava Pendente (ou não existe).</summary>
    public async Task<Autorizacao?> ConfirmarAsync(Guid id, DateTimeOffset confirmadaEm, CancellationToken ct)
    {
        await using var conexao = await conexoes.CriarAsync(ct);
        return await conexao.QuerySingleOrDefaultAsync<Autorizacao>(new CommandDefinition(Sql("""
            UPDATE dbo.Autorizacoes
               SET Status = @confirmada, ConfirmadaEm = @confirmadaEm
            OUTPUT inserted.Id, inserted.IdempotencyKey, inserted.TerminalId, inserted.Valor,
                   inserted.Bandeira, inserted.CartaoToken, inserted.Status, inserted.CriadaEm,
                   inserted.PublicadaEm, inserted.ConfirmadaEm
             WHERE Id = @id AND Status = @pendente
            """),
            new
            {
                id,
                confirmadaEm,
                confirmada = nameof(StatusAutorizacao.Confirmada),
                pendente = nameof(StatusAutorizacao.Pendente),
            },
            cancellationToken: ct));
    }
}
