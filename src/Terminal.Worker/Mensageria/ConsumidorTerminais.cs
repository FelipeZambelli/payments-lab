using Lab.Contracts;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Terminal.Worker.Configuracao;
using Terminal.Worker.Infra;
using Terminal.Worker.Processamento;

namespace Terminal.Worker.Mensageria;

/// <summary>
/// Declara e consome as filas "terminal.{id}". Uma conexão, um canal por terminal: o RabbitMQ.Client
/// entrega as mensagens de um canal em série, então cada terminal processa uma transação por vez e
/// os terminais rodam em paralelo entre si.
///
/// Shutdown gracioso (SIGTERM → StopAsync): para de consumir, espera as mensagens em voo terminarem
/// (ack/nack) e só então fecha canais e conexão. O que foi entregue mas não processado volta para a fila.
/// </summary>
public sealed class ConsumidorTerminais(
    WorkerOptions opcoes,
    IOptions<RabbitMqOptions> rabbitMq,
    ProcessadorAutorizacao processador,
    ILogger<ConsumidorTerminais> logger) : BackgroundService
{
    /// <summary>
    /// Tentativas para falhas transitórias; esgotadas, a mensagem vai para a DLX.
    /// Controlado aqui, e não pelo x-delivery-limit: no RabbitMQ 4.x um nack com requeue (AMQP 0.9.1)
    /// não conta para o delivery-limit, só incrementa o header x-acquired-count.
    /// </summary>
    private const int MaxTentativas = 5;

    /// <summary>
    /// Rede de segurança para entregas que voltam à fila sem nack (ex.: o Worker cai no meio):
    /// após esse limite o próprio broker manda para a DLX.
    /// </summary>
    private const int LimiteEntregas = 20;

    /// <summary>Pausa antes do requeue, para não martelar a API quando ela está fora.</summary>
    private static readonly TimeSpan EsperaAntesDoRequeue = TimeSpan.FromSeconds(1);

    // Quorum queue: suporta x-delivery-limit e informa x-acquired-count nas reentregas.
    private static readonly Dictionary<string, object?> ArgumentosFila = new()
    {
        ["x-queue-type"] = "quorum",
        ["x-dead-letter-exchange"] = Topologia.DeadLetterExchange,
        ["x-delivery-limit"] = LimiteEntregas,
    };

    private readonly List<IChannel> _canais = [];
    private readonly List<(IChannel Canal, string ConsumerTag)> _consumidores = [];
    private readonly CancellationTokenSource _abortar = new();
    private IConnection? _conexao;
    private volatile bool _parando;
    private int _emVoo;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = rabbitMq.Value;
        var factory = new ConnectionFactory
        {
            HostName = o.HostName,
            Port = o.Port,
            VirtualHost = o.VirtualHost,
            UserName = o.UserName,
            Password = o.Password,
            ClientProvidedName = "terminal-worker",
        };

        _conexao = await Retry.ExecutarAsync(t => factory.CreateConnectionAsync(t), logger, "conexão com RabbitMQ", stoppingToken);

        await using (var canal = await _conexao.CreateChannelAsync(cancellationToken: stoppingToken))
            await DeclararExchangesAsync(canal, stoppingToken);

        foreach (var terminalId in opcoes.Terminais)
            await PrepararTerminalAsync(terminalId, stoppingToken);

        if (opcoes.Pausado)
            logger.LogWarning("CHAOS_WORKER_PAUSED=true: {Quantidade} filas declaradas, mas nenhuma será consumida.", opcoes.Terminais.Count);
        else
            logger.LogInformation("Consumindo {Quantidade} filas (terminais {Primeiro}..{Ultimo}), prefetch {Prefetch}, API {ApiUrl}.",
                opcoes.Terminais.Count, opcoes.Terminais[0], opcoes.Terminais[^1], opcoes.Prefetch, opcoes.ApiUrl);

        // O trabalho acontece nos callbacks dos consumidores; aqui só esperamos o pedido de parada.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Idempotente e igual ao que a API declara: assim tanto faz quem sobe primeiro.
    /// </summary>
    private static async Task DeclararExchangesAsync(IChannel canal, CancellationToken ct)
    {
        await canal.ExchangeDeclareAsync(Topologia.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(Topologia.DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: ct);
        await canal.QueueDeclareAsync(Topologia.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await canal.QueueBindAsync(Topologia.DeadLetterQueue, Topologia.DeadLetterExchange, routingKey: "", cancellationToken: ct);
    }

    private async Task PrepararTerminalAsync(int terminalId, CancellationToken ct)
    {
        var canal = await _conexao!.CreateChannelAsync(cancellationToken: ct);
        _canais.Add(canal);

        await canal.BasicQosAsync(prefetchSize: 0, prefetchCount: opcoes.Prefetch, global: false, ct);

        var fila = Topologia.FilaTerminal(terminalId);
        await canal.QueueDeclareAsync(fila, durable: true, exclusive: false, autoDelete: false, arguments: ArgumentosFila, cancellationToken: ct);
        await canal.QueueBindAsync(fila, Topologia.Exchange, Topologia.RoutingKeyTerminal(terminalId), cancellationToken: ct);

        if (opcoes.Pausado)
            return;

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += (_, entrega) => ReceberAsync(canal, terminalId, entrega);
        var tag = await canal.BasicConsumeAsync(fila, autoAck: false, consumidor, ct);
        _consumidores.Add((canal, tag));
    }

    private async Task ReceberAsync(IChannel canal, int terminalId, BasicDeliverEventArgs entrega)
    {
        // Já entregue pelo broker, mas estamos parando: sem ack, volta para a fila quando o canal fechar.
        if (_parando)
            return;

        Interlocked.Increment(ref _emVoo);
        try
        {
            var desfecho = await processador.ProcessarAsync(entrega.Body, terminalId, _abortar.Token);
            switch (desfecho)
            {
                case Desfecho.Confirmada:
                    await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false);
                    break;
                case Desfecho.Descartar:
                    await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: false);
                    break;
                case Desfecho.TentarDeNovo when Tentativa(entrega) >= MaxTentativas:
                    logger.LogWarning("Terminal {TerminalId}: {MaxTentativas} tentativas esgotadas; mensagem vai para a DLQ.", terminalId, MaxTentativas);
                    await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: false);
                    break;
                case Desfecho.TentarDeNovo:
                    await Task.Delay(EsperaAntesDoRequeue, _abortar.Token);
                    await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true);
                    break;
            }
        }
        catch (OperationCanceledException) when (_abortar.IsCancellationRequested)
        {
            // Prazo de shutdown esgotado: sem ack, a mensagem volta para a fila.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Terminal {TerminalId}: erro inesperado; devolvendo a mensagem para a fila.", terminalId);
            try
            {
                await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true);
            }
            catch (Exception nackEx)
            {
                logger.LogError(nackEx, "Terminal {TerminalId}: falha ao devolver a mensagem.", terminalId);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _emVoo);
        }
    }

    /// <summary>1 na primeira entrega; x-acquired-count = quantas vezes a mensagem já foi entregue antes.</summary>
    private static long Tentativa(BasicDeliverEventArgs entrega) =>
        entrega.BasicProperties.Headers?.TryGetValue("x-acquired-count", out var anteriores) == true
            ? Convert.ToInt64(anteriores) + 1
            : 1;

    /// <param name="cancellationToken">Cancelado quando o prazo de shutdown (HostOptions.ShutdownTimeout) acaba.</param>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Cancela o stoppingToken e espera o ExecuteAsync terminar (inclusive um setup em andamento).
        await base.StopAsync(cancellationToken);
        _parando = true;

        try
        {
            // 1) Para de receber: o broker não entrega mais nada a esses consumidores.
            foreach (var (canal, tag) in _consumidores)
                await canal.BasicCancelAsync(tag, noWait: false, cancellationToken);

            // 2) Espera as mensagens em processamento terminarem (cada uma faz seu ack/nack).
            if (Volatile.Read(ref _emVoo) > 0)
                logger.LogInformation("Parando: aguardando {EmVoo} mensagem(ns) em processamento.", Volatile.Read(ref _emVoo));
            while (Volatile.Read(ref _emVoo) > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Prazo de shutdown esgotado com {EmVoo} mensagem(ns) em voo; elas voltarão para a fila.", Volatile.Read(ref _emVoo));
            await _abortar.CancelAsync();
        }

        // 3) Fecha canais e conexão. Mensagens entregues e sem ack voltam para a fila.
        foreach (var canal in _canais)
            await FecharAsync(() => canal.CloseAsync());
        if (_conexao is not null)
            await FecharAsync(() => _conexao.CloseAsync());

        logger.LogInformation("Worker parado.");
    }

    private async Task FecharAsync(Func<Task> fechar)
    {
        try
        {
            await fechar();
        }
        catch (Exception ex)
        {
            logger.LogWarning("Erro ao fechar recurso do RabbitMQ: {Erro}", ex.Message);
        }
    }

    public override void Dispose()
    {
        foreach (var canal in _canais)
            canal.Dispose();
        _conexao?.Dispose();
        _abortar.Dispose();
        base.Dispose();
    }
}
