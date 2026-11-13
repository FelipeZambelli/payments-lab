# payments-lab — Tópico 1 (observabilidade)

## Escopo liberado
- OpenTelemetry SDK (.NET) em Autorizador.Api e Terminal.Worker: traces, métricas e logs via OTLP.
- Configuração só por variáveis OTEL_* padrão (endpoint, service.name, resource attributes).
- Propagação W3C Trace Context nos headers das mensagens RabbitMQ (inject no publish, extract no consume),
  seguindo as semantic conventions de messaging.
- Spans manuais: autorizacao.criar, autorizacao.confirmar. Métricas de negócio via System.Diagnostics.Metrics
  (Meter "PaymentsLab"), SEM terminalId como atributo de métrica.
- Novos botões de caos: CHAOS_MEMORY_LEAK (Semana 5).
- Stack de observabilidade em deploy/compose/observability/ (Collector, Prometheus, Loki, Tempo, Grafana, Pyroscope),
  ligado por um compose override, para o Lab ainda subir sem ela.

## Regras
- Nenhum PAN, cartaoToken completo ou valor em atributos de span/métrica ou em baggage.
- Logs com message templates (sem interpolação de string) e sem logar objetos de request inteiros.
- Tudo deve rodar no MacBook M1 (ARM64); imagens amd64 só onde não houver alternativa, com platform explícito.

## Aceite (Semana 1)
- Um POST /autorizacoes gera um único trace com spans de HTTP, SQL, publish, process e confirmação,
  visível no dashboard standalone do Aspire.

## Estrutura (criar cada pasta só na semana em que for usada)
deploy/compose/observability/
├── collector-agent.yaml        # Semana 3
├── collector-gateway.yaml      # Semana 3
├── prometheus/                 # Semana 4 (rules do Sloth/Pyrra na Semana 6)
├── loki/  tempo/               # Semana 4
├── pyroscope/                  # Semana 5
└── grafana/                    # Semana 4 (provisioning: datasources, correlações, dashboards)
docs/
├── adr/                        # mini-ADRs do Lab (ex.: Collector vs. Alloy, Semana 4)
├── runbooks/                   # Semanas 5 e 7
└── postmortems/                # Semana 7