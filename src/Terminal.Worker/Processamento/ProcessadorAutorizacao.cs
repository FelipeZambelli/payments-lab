using System.Net;
using System.Text.Json;
using Lab.Contracts;

namespace Terminal.Worker.Processamento;

/// <summary>O que o consumidor deve fazer com a mensagem depois de processá-la.</summary>
public enum Desfecho
{
    /// <summary>API confirmou (ou já estava confirmada): ack.</summary>
    Confirmada,

    /// <summary>Mensagem que nunca vai dar certo (JSON inválido, autorização inexistente): nack sem requeue → DLX.</summary>
    Descartar,

    /// <summary>Falha transitória (API fora, 5xx, timeout): nack com requeue, até o limite de tentativas do consumidor.</summary>
    TentarDeNovo,
}

/// <summary>Desfecho e, nas falhas, a exceção que explica o porquê (vai para o span de process).</summary>
public readonly record struct Resultado(Desfecho Desfecho, Exception? Erro = null);

/// <summary>"Terminal" simulado: recebe AutorizacaoCriada e confirma na API.</summary>
public sealed class ProcessadorAutorizacao(IHttpClientFactory httpClients, ILogger<ProcessadorAutorizacao> logger)
{
    public const string ClienteApi = "autorizador-api";

    public async Task<Resultado> ProcessarAsync(ReadOnlyMemory<byte> corpo, int terminalId, CancellationToken ct)
    {
        AutorizacaoCriada? mensagem;
        try
        {
            mensagem = JsonSerializer.Deserialize<AutorizacaoCriada>(corpo.Span, JsonSerializerOptions.Web);
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Terminal {TerminalId}: mensagem com JSON inválido ({Erro}); descartando.", terminalId, ex.Message);
            return new(Desfecho.Descartar, ex);
        }

        if (mensagem is null || mensagem.AutorizacaoId == Guid.Empty)
        {
            logger.LogWarning("Terminal {TerminalId}: mensagem sem autorizacaoId; descartando.", terminalId);
            return new(Desfecho.Descartar, new InvalidDataException("Mensagem sem autorizacaoId."));
        }

        var id = mensagem.AutorizacaoId;
        var http = httpClients.CreateClient(ClienteApi);
        try
        {
            using var resposta = await http.PostAsync($"autorizacoes/{id}/confirmacao", content: null, ct);

            if (resposta.IsSuccessStatusCode)
            {
                logger.LogInformation("Terminal {TerminalId}: autorização {AutorizacaoId} confirmada.", terminalId, id);
                return new(Desfecho.Confirmada);
            }

            // 4xx (exceto 408/429) não muda com nova tentativa: vai para a DLQ para análise.
            var codigo = (int)resposta.StatusCode;
            // Resposta HTTP de erro não lança exceção: cria uma descritiva para registrar no span.
            var erro = new HttpRequestException($"API respondeu {codigo} na confirmação.", null, resposta.StatusCode);
            if (codigo is >= 400 and < 500 && resposta.StatusCode is not (HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests))
            {
                logger.LogWarning("Terminal {TerminalId}: API respondeu {Status} para {AutorizacaoId}; descartando.", terminalId, codigo, id);
                return new(Desfecho.Descartar, erro);
            }

            logger.LogWarning("Terminal {TerminalId}: API respondeu {Status} para {AutorizacaoId}; vai tentar de novo.", terminalId, codigo, id);
            return new(Desfecho.TentarDeNovo, erro);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Terminal {TerminalId}: falha ao chamar a API para {AutorizacaoId} ({Erro}); vai tentar de novo.", terminalId, id, ex.Message);
            return new(Desfecho.TentarDeNovo, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Terminal {TerminalId}: timeout chamando a API para {AutorizacaoId}; vai tentar de novo.", terminalId, id);
            return new(Desfecho.TentarDeNovo, ex);
        }
    }
}
