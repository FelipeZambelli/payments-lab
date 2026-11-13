# ADR-0002 — Escolher o backend de observabilidade (ControlPay atual e arquitetura-alvo)

| Campo | Valor |
|---|---|
| **Status** | 🟡 **Proposto** (decisão no checkpoint do Tópico 1, com dados) |
| **Data** | 2026-11-13 |
| **Escopo** | ControlPay atual (Azure) + arquitetura-alvo (AWS/EKS). No Lab o backend é LGTM self-hosted por definição (objetivo de aprendizado), então o Lab **não** é objeto deste ADR, mas gera evidência para ele |
| **Decisor(es)** | Felipe (EM) |
| **Consultados** | Allan (levantamento atual), tech leads, segurança/PCI, produto (métricas de negócio) |
| **Informados** | Todos os times; coordenador (processo de incidentes/on-call) |
| **Tipo de porta** | 🔁 **Mão dupla para o backend, se a instrumentação for OpenTelemetry.** 🚪 A porta de mão única é o **padrão de instrumentação**, que terá ADR próprio no Tópico 1 |
| **Revisar em** | Checkpoint do Tópico 1 |

---

## 1. Contexto e problema

- O ControlPay (.NET Framework 4.6.2, App Service Windows com 7 instâncias, Azure SQL, Service Bus com fila por terminal) usa **Application Insights** hoje. A visibilidade é limitada.
- **Incidentes recentes mostram as lacunas:**
  - memory spikes sem diagnóstico fácil;
  - dependência SQL lenta (spike de ~38 s em `GetProprietarioTarifas`);
  - investigação via FREB;
  - pipeline de logs para Kibana quebrado no passado (atraso p50 de ~6,75 min);
  - NHibernate sem spans.
- Faltam **SLOs, observabilidade de negócio** (transações/min por adquirente, taxa de aprovação, latência por PdC) e **observabilidade de mensageria** (profundidade e idade de fila, DLQ).
- A migração para AWS/EKS vai acontecer, mas é lenta: o legado continuará na Azure por um bom tempo. O backend escolhido precisa servir ao legado **agora** e sobreviver à migração, ou a troca precisa ser barata.

### Onde fica a agnosticidade: as três camadas da observabilidade

| Camada | O que é | Agnóstica? |
|---|---|---|
| **1. Instrumentação** | Código que gera traces, métricas e logs dentro da aplicação | **Obrigatório.** Trocar exige mexer em todos os serviços. É a porta de mão única |
| **2. Coleta e roteamento** | OpenTelemetry Collector: recebe, mascara, amostra e envia | **Obrigatório.** É o que permite trocar o backend sem tocar na aplicação |
| **3. Backend** | Armazenamento, consulta, alerta e visualização | **Desejável e negociável.** Com as camadas 1 e 2 em OpenTelemetry, trocar o backend é configuração do Collector |

**Decisão do decisor:** a agnosticidade é **inegociável nas camadas 1 e 2** e **negociável na camada 3**, onde entra como critério com peso (C5). Por isso opções proprietárias de uma nuvem (ex.: Application Insights) permanecem na análise, para o time estudar os trade-offs.

## 2. Direcionadores da decisão (critérios)

| # | Critério | Peso | Por quê importa aqui |
|---|---|---|---|
| C1 | Custo total (ingestão + retenção + infra/operação) | 3 | Telemetria cresce rápido; custo de ingestão é a surpresa clássica |
| C2 | Esforço operacional e bus factor | 3 | Quem opera o stack às 3h da manhã? Concentração de conhecimento |
| C3 | Segurança/PCI (mascaramento, acesso, residência de dados) | 3 | PAN/PII nunca podem chegar ao backend; auditoria |
| C4 | Atrito com o legado (.NET Framework 4.6.2, App Service Windows) | 3 | O ControlPay atual precisa ganhar valor **agora**, não só após a migração |
| C5 | Portabilidade do backend (sobreviver à migração para AWS) | 2 | Evitar refazer dashboards, alertas e consultas no cutover |
| C6 | Capacidades (SLO, alerting, correlação logs↔traces↔métricas, profiling) | 2 | É isso que reduz MTTD/MTTR |
| C7 | Escala de cardinalidade (fila por terminal, PdCs) | 2 | Labels por terminal podem explodir séries e custo |
| C8 | Curva do time | 1 | Ferramentas novas vs. já conhecidas |

**Restrições inegociáveis:**
- Instrumentação em **OpenTelemetry** (camada 1). Nada de SDK proprietário novo.
- **OpenTelemetry Collector** entre aplicação e backend (camada 2), mesmo com um único backend.
- Nenhum PAN/PII sai do processo sem mascaramento (SDK) e nova redação no Collector.
- Controle de acesso por papel aos dados de telemetria.
- Retenção definida e compatível com auditoria.

## 3. Opções consideradas

1. **A. Azure Monitor / Application Insights.** Evoluir o que existe.
   - Legado: Application Insights .NET SDK 3.x, baseado em OpenTelemetry por baixo, com pacote para ASP.NET em .NET Framework 4.6.2+.
   - Serviços novos: Azure Monitor OpenTelemetry Distro. Não misturar com o SDK 3.x na mesma aplicação.
2. **B. Stack LGTM self-hosted:** Grafana Alloy, **Loki** (logs), **Grafana**, **Tempo** (traces), **Mimir/Prometheus** (métricas), Pyroscope (profiling). Tudo operado pela empresa: hoje na Azure (AKS/Container Apps), amanhã no EKS.
3. **C. Grafana Cloud.** O mesmo LGTM, gerenciado pela Grafana Labs (SaaS neutro de nuvem).
   - Free tier: 10k séries, 50 GB de logs/traces/profiles, 14 dias de retenção.
   - Pro: a partir de US$ 19/mês + uso.
4. **D. Híbrido via Collector.** Instrumentação única em OTel, com fan-out no Collector. Ex.: App Insights para APM/diagnóstico do legado + Prometheus/Grafana para métricas de negócio, SLO e mensageria. Tende a ser **transitório** (enquanto o legado vive na Azure).
5. **E. Prometheus e Grafana gerenciados pela nuvem + Loki e Tempo:**
   - **Métricas:** Prometheus gerenciado pela nuvem (Azure Monitor managed service for Prometheus → Amazon Managed Service for Prometheus).
   - **Visualização e alertas:** Grafana gerenciado (Azure Managed Grafana → Amazon Managed Grafana).
   - **Logs:** **Loki**. **Traces:** **Tempo**.
   - As nuvens **não oferecem** Loki/Tempo gerenciados. Eles seriam **self-hosted** (ou via Grafana Cloud apenas para logs/traces). O Grafana gerenciado consulta Loki e Tempo como data sources.
   - Resultado: métricas e visualização gerenciadas (baixa operação), logs e traces operados por nós, com **mesmo formato e mesmas consultas** (PromQL, LogQL, TraceQL) nas duas nuvens.
6. **Manter como está.** Descartada: as lacunas da seção 1 já custam incidentes.

## 4. Análise preliminar das opções (sem notas ainda)

### A — Azure Monitor / Application Insights
- ✅ Menor atrito com o legado (C4): já existe, o SDK 3.x preserva a API `TelemetryClient` e roda em .NET Framework 4.6.2. Zero infra para operar (C2). Time já conhece (C8).
- ❌ Proprietário da Azure: **portabilidade fraca (C5)**. Na AWS, ou a telemetria cruza nuvens ou dashboards, alertas e consultas (KQL) são refeitos. Custo por GB ingerido (C1) pode crescer muito com traces completos.
- ⚠️ **A verificar:**
  - **Histogramas:** o Azure Monitor "achata" histogramas OTel em agregações simples (suporte a percentis anunciado como em andamento), e isso afeta SLO de latência (C6).
  - Custo atual de ingestão, a ser levantado pelo Allan.
  - Retenção e controle de acesso.
- 🎓 **Valor de estudo:** é o contraponto clássico "conveniência e integração vs. lock-in". Útil para o time praticar a leitura de trade-offs.

### B — LGTM self-hosted
- ✅ Controle total, custo de infra em vez de ingestão (C1 potencialmente menor em volume alto), **portabilidade máxima (C5)**: é o mesmo stack no EKS. Capacidades completas (C6), incluindo profiling.
- ❌ **Esforço operacional e bus factor altos (C2):** são quatro ou cinco componentes com estado (armazenamento de objetos, compactação, retenção, upgrades, escala) para operar e proteger. Exige time de plataforma. Atrito médio com o legado (C4): o App Service Windows exige o Collector hospedado fora do app.
- ⚠️ **A verificar:** onde hospedar na Azure antes do EKS (Container Apps, AKS, VM) e o custo dessa infra.

### C — Grafana Cloud
- ✅ Mesmas capacidades e portabilidade do LGTM (C5, C6) **sem operar o stack** (C2). Free tier generoso para POC. Cobrança previsível por série/GB. Neutro de nuvem.
- ❌ Custo por série ativa pode explodir com cardinalidade alta (C7: fila por terminal). Dados fora do perímetro Azure/AWS (C3).
- ⚠️ **A verificar:** região e residência de dados (há região no Brasil ou na América do Sul?), requisitos PCI do fornecedor, custo projetado com a cardinalidade real.

### D — Híbrido via Collector
- ✅ Aproveita o App Insights para diagnóstico do legado (C4) e usa Prometheus/Grafana onde o App Insights é fraco (SLO, histogramas, negócio, mensageria). Instrumentação única em OTel mantém a porta de mão dupla (C5).
- ❌ Dois backends para conhecer, dois custos e mais complexidade no Collector (C2, C8).
- ⚠️ **A verificar:** se a complexidade se paga ou se uma opção única resolve. Se escolhida, definir **data de saída** do lado App Insights (ex.: cutover para AWS).

### E — Prometheus/Grafana gerenciados pela nuvem + Loki e Tempo
- ✅ **Métricas e visualização sem operar** (C2 baixo para essas partes). PromQL e dashboards Grafana são **os mesmos nas duas nuvens**, então dashboards e alertas migram com pouco retrabalho (C5 bom). Histogramas nativos do Prometheus resolvem SLO de latência (C6). Dados ficam no perímetro da nuvem (C3).
- ❌ **Loki e Tempo continuam self-hosted** (C2 médio): o esforço operacional cai em relação à B, mas não some. Os serviços gerenciados de Prometheus/Grafana são diferentes em cada nuvem (configuração, IAM, preço), então a migração troca o serviço, mas preserva formato e consultas (C5 bom, não máximo). Cobrança por amostra ingerida/consultada no Prometheus gerenciado exige atenção à cardinalidade (C7).
- ⚠️ **A verificar:**
  - Preço do Azure Monitor managed Prometheus e do Amazon Managed Prometheus com a cardinalidade real.
  - Onde hospedar Loki e Tempo na Azure antes do EKS.
  - Alternativa: Loki/Tempo via Grafana Cloud. Isso viraria um "E + C".

### Matriz de decisão
**Preenchida no checkpoint do Tópico 1**, com:
- os dados do levantamento do Allan;
- custo medido/projetado no Lab;
- a experiência real de operar o LGTM no Lab, que serve de proxy de C2 para as opções B e E.

| Critério (peso) | A | B | C | D | E |
|---|---|---|---|---|---|
| C1 Custo (3) | | | | | |
| C2 Operação/bus factor (3) | | | | | |
| C3 Segurança/PCI (3) | | | | | |
| C4 Atrito com legado (3) | | | | | |
| C5 Portabilidade do backend (2) | | | | | |
| C6 Capacidades (2) | | | | | |
| C7 Cardinalidade (2) | | | | | |
| C8 Curva (1) | | | | | |
| **Total (máx. 95)** | | | | | |

## 5. Dados que faltam (insumo para a decisão)

| Dado | Fonte | Dono | Prazo |
|---|---|---|---|
| Ingestão atual do App Insights (GB/dia) e custo mensal | Azure Cost Management / App Insights | **Allan** | 04/12/2026 |
| Top fontes de volume (requests, dependências, traces, exceções) | App Insights | **Allan** | 04/12/2026 |
| Retenção configurada e retenção exigida (auditoria/PCI) | App Insights + segurança | **Allan** + segurança | 04/12/2026 |
| Alertas existentes e quantos são ruído | App Insights / Azure Monitor | **Allan** | 04/12/2026 |
| Nº de terminais/filas ativos e crescimento (cardinalidade) | Service Bus | **Allan** | 04/12/2026 |
| Lacunas conhecidas (NHibernate sem spans, FREB, memory spikes, SQL lento) | Time | **Allan** | 04/12/2026 |
| Suporte do Azure Monitor a histogramas/percentis via OTel | Docs + teste no Lab | Felipe | Tópico 1 |
| Região/residência de dados e postura PCI do Grafana Cloud | Grafana Labs | Felipe | Tópico 1 |
| Custo de operar LGTM (infra + horas) | Lab | Felipe | Tópico 1 |
| Preço do Prometheus gerenciado (Azure e AWS) com a cardinalidade real | Calculadoras de preço + dados do Allan | Felipe | Tópico 1 |
| Hospedagem de Loki/Tempo na Azure antes do EKS (opção E) | Lab | Felipe | Tópico 1 |
| Quem seria o time de plataforma (Team Topologies) | Felipe + tech leads | Felipe | Tópico 1 / 3b |

## 6. Decisão
*A preencher no checkpoint do Tópico 1.*

**Decisões já tomadas, válidas para qualquer opção:**
- Camada 1: instrumentação em **OpenTelemetry** (o SDK 3.x do App Insights no legado já é OTel por baixo).
- Camada 2: **OpenTelemetry Collector** entre aplicação e backend. Ele é o ponto de redação de PAN/PII, amostragem e troca de backend.
- Redação de PAN/PII no SDK **e** no Collector.
- Camada 3: agnosticidade **negociável**, avaliada pelo critério C5.

## 7. Consequências esperadas
*A preencher com a decisão.*

## 8. Gatilhos de revisão (depois de aceito)
- Custo de telemetria acima do limite combinado (a definir) por 2 meses seguidos.
- Início do cutover para AWS/EKS.
- Mudança de preço ou de política de dados do fornecedor escolhido.
- Cardinalidade (nº de terminais) crescendo acima da faixa projetada.

## 9. Execução
- Pacote de delegação **"Levantamento da telemetria atual do ControlPay"** → Allan (autonomia nível 1, retorno em 04/12/2026).
- Lab do Tópico 1:
  - stack LGTM self-hosted (base de comparação para B e E);
  - teste do exporter Azure Monitor (histogramas, correlação), para A e D;
  - estimativa de custo do Prometheus gerenciado, para E.

## 10. Referências
- Application Insights SDK 3.x para .NET: https://techcommunity.microsoft.com/blog/azureobservabilityblog/announcing-application-insights-sdk-3-x-for-net/4493988
- Migração para OpenTelemetry no Azure Monitor: https://learn.microsoft.com/en-gb/azure/azure-monitor/app/migrate-to-opentelemetry
- Azure Monitor OpenTelemetry (incl. .NET Framework 4.6.2+): https://docs.azure.cn/en-us/azure-monitor/app/opentelemetry-enable?tabs=net
- Azure Monitor — instrumentações OTel e histogramas: https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-add-modify
- Grafana Cloud pricing 2026: https://last9.io/blog/grafana-cloud-pricing/

---
### Histórico
| Data | Mudança | Autor |
|---|---|---|
| 2026-11-13 | Criado (Proposto) com critérios e pesos aprovados pelo Felipe | Claude + Felipe |
| 2026-11-13 | Adicionado o modelo de três camadas (agnosticidade obrigatória em instrumentação e coleta, negociável no backend). Nova **opção E** (Prometheus/Grafana gerenciados pela nuvem + Loki e Tempo). Opção A mantida para estudo do time | Felipe |
