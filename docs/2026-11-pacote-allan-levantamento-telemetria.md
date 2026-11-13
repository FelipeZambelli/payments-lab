# Pacote de delegação — Levantamento da telemetria atual do ControlPay

| Campo | Valor |
|---|---|
| **Dono (accountable)** | Allan (tech lead) |
| **Executores** | Allan; pode envolver quem achar necessário do time |
| **Patrocinador** | Felipe (EM) |
| **Início / marco final** | Semana de 16/11/2026 → **sexta, 04/12/2026** |
| **ADRs relacionados** | ADR-0002 — Backend de observabilidade (Proposto) |
| **Iniciativa** | Observabilidade, SRE e resposta a incidentes |

---

## 1. Contexto e problema

Vamos decidir qual será o backend de observabilidade do ControlPay daqui para frente (ADR-0002). As opções em análise são:
- evoluir o Application Insights;
- stack Grafana (LGTM) operado por nós;
- Grafana Cloud;
- um híbrido via OpenTelemetry Collector.

A decisão precisa servir ao ControlPay de hoje (.NET Framework 4.6.2 no App Service) **e** sobreviver à migração para AWS.

Hoje não temos números confiáveis sobre o que coletamos, quanto custa, o que é ruído e onde estão os buracos. Os incidentes recentes mostram as lacunas:
- memory spikes difíceis de diagnosticar;
- dependência SQL lenta (spike de ~38 s em `GetProprietarioTarifas`);
- investigações via FREB;
- NHibernate sem spans.

**Sem esse retrato, qualquer decisão vira opinião.**

## 2. Resultado esperado

Um retrato factual da telemetria atual do ControlPay (volume, custo, retenção, alertas, cardinalidade e lacunas), com fonte e data de cada número, suficiente para comparar as opções do ADR-0002.

## 3. Critério de sucesso (mensurável)

| Métrica | Linha de base hoje | Alvo | Como medir |
|---|---|---|---|
| Itens do levantamento (seção 7) respondidos | 0 de 6 | **6 de 6**, cada um com fonte e data | Revisão no retorno de 04/12 |
| Números verificáveis | — | 100% com query, print ou link de origem | Revisão no retorno de 04/12 |

Se algum item não puder ser respondido, a resposta "não dá para saber porque X" também conta, desde que explique o motivo e o que seria preciso para descobrir.

## 4. Decisões já tomadas vs. em aberto

- **Já decidido (não rediscutir sem fato novo):**
  - A instrumentação futura será baseada em **OpenTelemetry**.
  - Haverá um **OpenTelemetry Collector** entre aplicação e backend.
  - PAN/PII nunca chegam ao backend: mascaramento no SDK e no Collector.
- **Em aberto, você decide:**
  - Como coletar os dados (queries, portal, CLI, conversas com o time).
  - Formato da entrega (doc, planilha, apresentação).
- **Em aberto, mas fora deste pacote:**
  - Qual backend escolher. Essa decisão sai depois, com estes dados e outros insumos. Sua opinião é bem-vinda no retorno, separada dos fatos.

## 5. Restrições

- 🔒 **Segurança:** **apenas leitura em produção.** Nada de alterar configuração, amostragem, alertas ou retenção durante o levantamento. Se encontrar algo grave (ex.: dado sensível em log), me avise na hora (ver seção 6).
- 💰 **Custo:** nenhum gasto novo previsto.
- ⏱️ **Prazo:** 04/12/2026. Tempo parcial, sem atrapalhar entregas do time.
- 🧱 **Técnicas:** nenhuma adicional.

## 6. Nível de autonomia combinado

- [x] **1 — Investiga e me traz os fatos; eu decido.**

**Me escale imediatamente se:**
- encontrar PAN, PII ou segredo em logs ou telemetria;
- descobrir custo de telemetria muito acima do esperado;
- perceber que o prazo de 04/12 está em risco por mais de 1 semana;
- o levantamento exigir qualquer alteração em produção.

## 7. Entregáveis

Responder os 6 itens abaixo, cada um com **fonte e data**:

- [ ] **7.1 Volume e custo.** Ingestão do Application Insights / Log Analytics nos últimos 30 dias (GB/dia médio e pico) e custo mensal atribuído à telemetria.
- [ ] **7.2 Composição do volume.** Quanto é requests, dependências, traces/logs, exceções, métricas customizadas. Top 5 fontes ou operações que mais geram volume.
- [ ] **7.3 Retenção.** Retenção configurada hoje e retenção exigida (auditoria, PCI, contratos). Consultar segurança/compliance se necessário.
- [ ] **7.4 Alertas.** Lista dos alertas existentes. Quantos dispararam nos últimos 90 dias e, na sua avaliação, quantos foram **ruído** (dispararam sem ação necessária). Quantos incidentes recentes **não** foram detectados por alerta (o cliente avisou antes)?
- [ ] **7.5 Cardinalidade.** Nº de filas/terminais ativos no Service Bus hoje, ritmo de crescimento e projeção para 12 meses. Nº de PdCs ativos.
- [ ] **7.6 Lacunas.** O que o time **não consegue ver** hoje e que já atrapalhou investigação. Ex.: NHibernate sem spans, memory spikes, FREB, SQL lento, filas paradas, correlação entre API e cliente TEF.

**Opcional (bem-vindo):** sua opinião sobre as opções do ADR-0002, separada dos fatos.

### Sugestões de onde buscar (não obrigatório)

Volume por tipo de dado (Log Analytics workspace do App Insights; `Quantity` está em MB):
```kusto
Usage
| where TimeGenerated > ago(30d) and IsBillable == true
| summarize GB = sum(Quantity) / 1024 by DataType
| order by GB desc
```

Nº de filas no Service Bus:
```bash
az servicebus queue list --resource-group <rg> --namespace-name <namespace> --query "length(@)"
```

Custo: Cost Management, filtrado pelo recurso do App Insights / Log Analytics, últimos 3 meses.

Alertas: Azure Monitor → Alerts (histórico) e Alert rules.

## 8. Checkpoints

| Quando | Formato | O que levar |
|---|---|---|
| Entrega (semana de 16/11) | 1:1, 15 min | Dúvidas sobre o escopo; como pretende coletar |
| **~27/11** | 1:1, 15 min | O que já encontrou; algo travado? Algum achado que mereça escalar? |
| **04/12** | 1:1, 30 min | Apresentação dos 6 itens + opinião (opcional) |

## 9. Recursos e apoio

- Acesso de leitura ao App Insights, Log Analytics, Cost Management e Service Bus de produção (confirmar se já tem).
- ADR-0002 para contexto das opções (posso compartilhar).
- Felipe disponível para dúvidas no meio do caminho.

## 10. Riscos conhecidos

| Risco | Prob. | Impacto | Mitigação |
|---|---|---|---|
| Falta de permissão de leitura em algum recurso | Média | Médio | Verificar acessos na primeira semana |
| Retenção exigida não estar documentada em lugar nenhum | Média | Médio | Registrar como lacuna e indicar quem precisaria definir |
| Concorrência com entregas do time | Média | Baixo | Tempo parcial; prazo com folga; avisar se apertar |

## 11. Ensino e disseminação

Ao fim, apresentar o retrato em 10 minutos para os outros tech leads. Isso nivela o conhecimento sobre a telemetria atual e evita que ele fique concentrado em uma pessoa.

---
### Registro de acompanhamento
| Data | Status (🟢🟡🔴) | Nota |
|---|---|---|
| | | Pacote entregue |
