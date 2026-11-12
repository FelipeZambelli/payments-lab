using Autorizador.Api.Infra;
using DbUp;

namespace Autorizador.Api.Banco;

/// <summary>Cria o banco (se necessário) e aplica os scripts em Migrations/ no startup da API.</summary>
public static class Migrador
{
    public static async Task ExecutarAsync(string connectionString, ILogger logger, CancellationToken ct)
    {
        await Retry.ExecutarAsync(_ =>
        {
            EnsureDatabase.For.SqlDatabase(connectionString);
            return Task.FromResult(true);
        }, logger, "criação do banco", ct);

        var resultado = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(Migrador).Assembly)
            .WithTransactionPerScript()
            .LogTo(logger)
            .Build()
            .PerformUpgrade();

        if (!resultado.Successful)
            throw new InvalidOperationException("Falha ao aplicar migrations.", resultado.Error);
    }
}
