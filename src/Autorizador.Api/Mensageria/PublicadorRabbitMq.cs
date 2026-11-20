using System.Diagnostics;
using System.Text.Json;
using Autorizador.Api.Infra;
using Lab.Contracts;
using Lab.Telemetria;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Autorizador.Api.Mensageria;

/// <summary>
/// Mantém uma conexão e um canal com publisher confirms: PublicarAsync só retorna
/// depois que o broker confirmou a mensagem (lança exceção se ele recusar).
/// </summary>
public sealed class PublicadorRabbitMq(IOptions<RabbitMqOptions> options, ILogger<PublicadorRabbitMq> logger)
    : IAsyncDisposable
{
    // Serializa publicações no canal compartilhado; simples e suficiente para o lab.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _conexao;
    private IChannel? _canal;

    /// <summary>Usado pelo /health/ready. Fica false enquanto o automatic recovery tenta reconectar.</summary>
    public bool Conectado => _conexao?.IsOpen == true && _canal?.IsOpen == true;

    public async Task InicializarAsync(CancellationToken ct)
    {
        var o = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = o.HostName,
            Port = o.Port,
            VirtualHost = o.VirtualHost,
            UserName = o.UserName,
            Password = o.Password,
            ClientProvidedName = "autorizador-api",
        };

        _conexao = await Retry.ExecutarAsync(t => factory.CreateConnectionAsync(t), logger, "conexão com RabbitMQ", ct);
        _canal = await _conexao.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);

        await DeclararTopologiaAsync(_canal, ct);
        logger.LogInformation("Conectado ao RabbitMQ em {Host}:{Port}", o.HostName, o.Port);
    }

    /// <summary>
    /// A API declara só os exchanges e a DLQ. As filas "terminal.{id}" são responsabilidade do Worker.
    /// </summary>
    private static async Task DeclararTopologiaAsync(IChannel canal, CancellationToken ct)
    {
        await canal.ExchangeDeclareAsync(Topologia.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(Topologia.DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: ct);
        await canal.QueueDeclareAsync(Topologia.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await canal.QueueBindAsync(Topologia.DeadLetterQueue, Topologia.DeadLetterExchange, routingKey: "", cancellationToken: ct);
    }

    /// <summary>
    /// Abre o span "publish terminais" (Producer) e injeta o contexto dele nos headers da mensagem,
    /// para o span de process do Worker ser filho deste. O span inclui a espera pelo publisher confirm.
    /// </summary>
    public async Task PublicarAsync<T>(T mensagem, string routingKey, Guid messageId, CancellationToken ct)
    {
        var canal = _canal ?? throw new InvalidOperationException("Publicador não inicializado.");

        // Nome com cardinalidade baixa ("{operação} {destino}"); o routing key real vai só em atributo.
        using var span = Telemetria.Fonte.StartActivity($"publish {Topologia.Exchange}", ActivityKind.Producer);
        span?.SetTag("messaging.system", "rabbitmq");
        span?.SetTag("messaging.operation.type", "send");
        span?.SetTag("messaging.operation.name", "publish");
        span?.SetTag("messaging.destination.name", Topologia.Exchange);
        span?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        span?.SetTag("messaging.message.id", messageId.ToString());

        var agora = DateTimeOffset.UtcNow;
        var headers = new Dictionary<string, object?>
        {
            // Unix ms (long): o process calcula lab.tempo_na_fila_ms a partir dele.
            [Topologia.HeaderPublicadoEm] = agora.ToUnixTimeMilliseconds(),
        };
        PropagacaoRabbitMq.Injetar(span?.Context ?? Activity.Current?.Context ?? default, headers);

        var propriedades = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = messageId.ToString(),
            Type = typeof(T).Name,
            Timestamp = new AmqpTimestamp(agora.ToUnixTimeSeconds()),
            Headers = headers,
        };
        var corpo = JsonSerializer.SerializeToUtf8Bytes(mensagem, JsonSerializerOptions.Web);

        await _lock.WaitAsync(ct);
        try
        {
            // Com publisherConfirmationTrackingEnabled, lança PublishException se o broker recusar (nack).
            await canal.BasicPublishAsync(Topologia.Exchange, routingKey, mandatory: false, propriedades, corpo, ct);
        }
        catch (Exception ex)
        {
            span.RegistrarFalha(ex);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_canal is not null) await _canal.CloseAsync();
        if (_conexao is not null) await _conexao.CloseAsync();
        _lock.Dispose();
    }
}
