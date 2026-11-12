using Autorizador.Api.Mensageria;
using Lab.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Autorizador.Api.Autorizacoes;

public static class AutorizacoesEndpoints
{
    public static IEndpointRouteBuilder MapAutorizacoes(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/autorizacoes");
        grupo.MapPost("/", Criar);
        grupo.MapGet("/{id:guid}", Obter).WithName(nameof(Obter));
        grupo.MapPost("/{id:guid}/confirmacao", Confirmar);
        return app;
    }

    /// <summary>
    /// Grava como Pendente e publica AutorizacaoCriada em "terminais" com routing key "terminal.{id}".
    /// Mesma idempotencyKey → mesmo autorizacaoId. Só republica se a publicação anterior não chegou
    /// a ser confirmada pelo broker (PublicadaEm nulo).
    /// </summary>
    private static async Task<Results<Accepted<CriarAutorizacaoResponse>, ValidationProblem>> Criar(
        CriarAutorizacaoRequest request,
        AutorizacaoRepository repositorio,
        PublicadorRabbitMq publicador,
        TimeProvider relogio,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Autorizacoes");
        var erros = request.Validar();
        if (erros.Count > 0)
            return TypedResults.ValidationProblem(erros);

        var autorizacao = await repositorio.ObterPorIdempotencyKeyAsync(request.IdempotencyKey!, ct);
        if (autorizacao is null)
        {
            var nova = new Autorizacao
            {
                Id = Guid.CreateVersion7(),
                IdempotencyKey = request.IdempotencyKey!,
                TerminalId = request.TerminalId,
                Valor = request.Valor,
                Bandeira = request.Bandeira!.Trim(),
                CartaoToken = request.CartaoToken,
                Status = StatusAutorizacao.Pendente,
                CriadaEm = relogio.GetUtcNow(),
            };

            // Se outra requisição com a mesma chave inseriu entre o SELECT e o INSERT, usa a dela.
            autorizacao = await repositorio.InserirAsync(nova, ct)
                ? nova
                : await repositorio.ObterPorIdempotencyKeyAsync(nova.IdempotencyKey, ct)
                  ?? throw new InvalidOperationException("IdempotencyKey duplicada, mas registro não encontrado.");
        }

        if (autorizacao.PublicadaEm is null)
        {
            var mensagem = new AutorizacaoCriada(
                autorizacao.Id, autorizacao.TerminalId, autorizacao.Valor,
                autorizacao.Bandeira, autorizacao.CartaoToken, autorizacao.CriadaEm);

            await publicador.PublicarAsync(mensagem, Topologia.RoutingKeyTerminal(autorizacao.TerminalId), autorizacao.Id, ct);
            await repositorio.MarcarPublicadaAsync(autorizacao.Id, relogio.GetUtcNow(), ct);
            logger.LogInformation("Autorização {AutorizacaoId} publicada para terminal {TerminalId}",
                autorizacao.Id, autorizacao.TerminalId);
        }

        return TypedResults.Accepted($"/autorizacoes/{autorizacao.Id}", new CriarAutorizacaoResponse(autorizacao.Id));
    }

    private static async Task<Results<Ok<AutorizacaoResponse>, NotFound>> Obter(
        Guid id, AutorizacaoRepository repositorio, CancellationToken ct)
    {
        var autorizacao = await repositorio.ObterAsync(id, ct);
        return autorizacao is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(AutorizacaoResponse.De(autorizacao));
    }

    /// <summary>
    /// Pendente → Confirmada e publica AutorizacaoConfirmada com routing key "confirmacao.{terminalId}".
    /// Idempotente: confirmar de novo retorna 200 com o estado atual, sem republicar.
    /// </summary>
    private static async Task<Results<Ok<AutorizacaoResponse>, NotFound>> Confirmar(
        Guid id,
        AutorizacaoRepository repositorio,
        PublicadorRabbitMq publicador,
        TimeProvider relogio,
        CancellationToken ct)
    {
        var confirmada = await repositorio.ConfirmarAsync(id, relogio.GetUtcNow(), ct);
        if (confirmada is null)
        {
            var atual = await repositorio.ObterAsync(id, ct);
            return atual is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(AutorizacaoResponse.De(atual));
        }

        var mensagem = new AutorizacaoConfirmada(confirmada.Id, confirmada.TerminalId, confirmada.ConfirmadaEm!.Value);
        await publicador.PublicarAsync(mensagem, Topologia.RoutingKeyConfirmacao(confirmada.TerminalId), confirmada.Id, ct);

        return TypedResults.Ok(AutorizacaoResponse.De(confirmada));
    }
}
