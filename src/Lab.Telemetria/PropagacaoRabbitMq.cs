using System.Diagnostics;
using System.Text;
using OpenTelemetry.Context.Propagation;

namespace Lab.Telemetria;

/// <summary>
/// Propagação W3C manual pelos headers AMQP: o publish injeta "traceparent"/"tracestate" e o consumer
/// extrai, para que o span de process seja filho do span de publish (um único trace).
///
/// Usa um TraceContextPropagator próprio, e não o Propagators.DefaultTextMapPropagator (que inclui
/// baggage): assim só o contexto de trace atravessa a fila, nunca baggage.
/// </summary>
public static class PropagacaoRabbitMq
{
    private static readonly TraceContextPropagator Propagador = new();

    /// <summary>Grava traceparent (e tracestate, se houver) como string nos headers.</summary>
    public static void Injetar(ActivityContext contexto, IDictionary<string, object?> headers)
    {
        if (contexto == default)
            return;

        Propagador.Inject(new PropagationContext(contexto, default), headers,
            static (h, chave, valor) => h[chave] = valor);
    }

    /// <summary>Lê o contexto dos headers; devolve default se não houver traceparent válido.</summary>
    public static ActivityContext Extrair(IDictionary<string, object?>? headers)
    {
        if (headers is null)
            return default;

        return Propagador.Extract(default, headers, static (h, chave) =>
            h.TryGetValue(chave, out var valor) && valor is not null
                // O RabbitMQ.Client entrega headers string como byte[] (UTF-8).
                ? [valor is byte[] bytes ? Encoding.UTF8.GetString(bytes) : valor.ToString()!]
                : []).ActivityContext;
    }
}
