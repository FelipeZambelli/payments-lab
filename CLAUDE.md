# Instruções para o assistente de código

- A especificação do projeto está em SPEC.md; o tópico atual (observabilidade) está em SPEC-T1.md.
  Siga os dois; em caso de dúvida, pergunte antes de inventar.
- SPEC-T1.md libera OpenTelemetry e define regras de telemetria (nada de PAN, cartaoToken ou valor em
  spans, métricas ou baggage; logs com message templates) e a estrutura de pastas por semana
  (criar cada pasta só na semana em que for usada).
- Respeite a seção "Não fazer" do SPEC.md: nada de testes, CI, Kubernetes ou Terraform até ser pedido.
- Nunca aceite, gere ou armazene números de cartão (PAN). Use apenas cartaoToken (Guid).
- Repositório público: nenhum segredo em código, nenhuma referência a empresas ou clientes reais.
- Trabalhe em etapas pequenas; ao fim de cada etapa, diga como testar manualmente.
- Ambiente: macOS Apple Silicon; SQL Server roda como linux/amd64 via Rosetta. Imagens amd64 só onde
  não houver alternativa ARM64, sempre com `platform` explícito.
