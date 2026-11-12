# payments-lab — especificação para geração

## Contexto
Laboratório pessoal de estudos de DevOps/Cloud que simula, de forma simplificada e fictícia, um backend de pagamentos TEF:
uma API recebe pedidos de autorização e os entrega a terminais via fila RabbitMQ (uma fila por terminal).
Não é produção. Repositório público. Priorizar código claro e idiomático sobre features.

## Stack
- .NET 10, ASP.NET Core Minimal APIs, C# atual, nullable habilitado.
- SQL Server 2022 (container), acesso via Dapper (sem ORM; migrations com DbUp em scripts SQL versionados).
- RabbitMQ 4.x via RabbitMQ.Client 7.x (API async).
- Docker Compose em deploy/compose/. Dev Container em .devcontainer/.
- Ambiente de desenvolvimento: macOS Apple Silicon (ARM64). O serviço sqlserver no Compose deve usar
  `platform: linux/amd64` (emulação via Rosetta); os demais serviços devem rodar nativos em ARM64.

## Projetos
- src/Lab.Contracts: records das mensagens (AutorizacaoCriada, AutorizacaoConfirmada).
- src/Autorizador.Api:
  - POST /autorizacoes {terminalId, valor (decimal), bandeira, cartaoToken (Guid), idempotencyKey}
    -> grava (status=Pendente), publica em exchange topic "terminais" com routing key "terminal.{terminalId}", retorna 202 {autorizacaoId}.
    -> idempotência: mesma idempotencyKey retorna o mesmo autorizacaoId sem republicar.
  - GET /autorizacoes/{id}
  - POST /autorizacoes/{id}/confirmacao -> status=Confirmada
  - /health/live e /health/ready (ready verifica SQL e RabbitMQ).
- src/Terminal.Worker (BackgroundService):
  - Declara e consome filas "terminal.{id}" para uma lista configurável de terminais (TERMINAIS=1-50).
  - Filas com DLX "terminais.dlx". Prefetch configurável. Ack manual após confirmar na API.
  - Shutdown gracioso: ao receber SIGTERM, para de consumir, termina mensagens em voo, fecha canal.
- tools/LoadGen: console que dispara N transações/seg aleatórias para M terminais.

## Chaos (variáveis de ambiente)
- CHAOS_SQL_DELAY_MS (API): atraso artificial antes de cada query.
- CHAOS_ERROR_RATE (API): fração 0..1 de requisições que retornam 500.
- CHAOS_WORKER_PAUSED (Worker): se true, não consome.

## Regras de segurança
- Nunca aceitar/armazenar PAN. Apenas cartaoToken (Guid).
- Nenhum segredo no código; usar variáveis de ambiente / .env (fora do git) com .env.example versionado.
- Nenhuma referência a empresas, produtos ou clientes reais. Domínio 100% fictício.

## Não fazer (fica para tópicos futuros)
- Não adicionar OpenTelemetry, logging estruturado avançado, testes, CI, Kubernetes ou Terraform.
  Deixar apenas ILogger padrão. Cada um desses entra em um tópico específico da trilha.

## Aceite
- `docker compose up` sobe tudo sem passos manuais (migrations rodam no startup da API), em Mac M1.
- README com: como subir, como gerar carga, como ligar cada chaos flag.