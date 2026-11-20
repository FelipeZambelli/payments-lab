using Autorizador.Api.Mensageria;
using Lab.Contracts;
using Lab.Telemetria;
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
    /// Span "autorizacao.criar": só ids, terminal e bandeira; nunca valor nem cartaoToken.
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

        using var span = Telemetria.Fonte.StartActivity("autorizacao.criar");
        span?.SetTag("terminal.id", request.TerminalId);
        span?.SetTag("autorizacao.bandeira", request.Bandeira!.Trim());

        var autorizacao = await repositorio.ObterPorIdempotencyKeyAsync(request.IdempotencyKey!, ct);
        var idempotente = autorizacao is not null;
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
            if (await repositorio.InserirAsync(nova, ct))
                autorizacao = nova;
            else
            {
                idempotente = true;
                autorizacao = await repositorio.ObterPorIdempotencyKeyAsync(nova.IdempotencyKey, ct)
                    ?? throw new InvalidOperationException("IdempotencyKey duplicada, mas registro não encontrado.");
            }
        }

        span?.SetTag("autorizacao.id", autorizacao.Id.ToString());
        span?.SetTag("autorizacao.idempotente", idempotente);

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
    /// AutorizacaoConfirmada não tem consumidor por enquanto: o broker descarta em silêncio, por design.
    /// No trace, esse publish sem process é indistinguível de mensagem perdida (detecção: Semana 6).
    /// </summary>
    private static async Task<Results<Ok<AutorizacaoResponse>, NotFound>> Confirmar(
        Guid id,
        AutorizacaoRepository repositorio,
        PublicadorRabbitMq publicador,
        TimeProvider relogio,
        CancellationToken ct)
    {
        using var span = Telemetria.Fonte.StartActivity("autorizacao.confirmar");
        span?.SetTag("autorizacao.id", id.ToString());

        var confirmada = await repositorio.ConfirmarAsync(id, relogio.GetUtcNow(), ct);
        if (confirmada is null)
        {
            var atual = await repositorio.ObterAsync(id, ct);
            if (atual is null)
            {
                span?.SetTag("autorizacao.encontrada", false);
                return TypedResults.NotFound();
            }

            span?.SetTag("terminal.id", atual.TerminalId);
            span?.SetTag("autorizacao.ja_confirmada", true);
            return TypedResults.Ok(AutorizacaoResponse.De(atual));
        }

        span?.SetTag("terminal.id", confirmada.TerminalId);
        span?.SetTag("autorizacao.ja_confirmada", false);

        var mensagem = new AutorizacaoConfirmada(confirmada.Id, confirmada.TerminalId, confirmada.ConfirmadaEm!.Value);
        await publicador.PublicarAsync(mensagem, Topologia.RoutingKeyConfirmacao(confirmada.TerminalId), confirmada.Id, ct);

        return TypedResults.Ok(AutorizacaoResponse.De(confirmada));
    }
}
