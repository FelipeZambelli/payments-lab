# ADR-NNNN — <Título curto, no imperativo: "Usar X para Y">

> Template baseado em MADR 4.0, adaptado para a camada de EM da trilha.
> Regra de ouro: **um ADR = uma decisão.** Se aparecerem duas, quebre em dois.
> Nome do arquivo: `docs/adr/NNNN-titulo-em-kebab-case.md` (numeração sequencial, nunca reaproveitada).

| Campo | Valor |
|---|---|
| **Status** | Proposto · Aceito · Rejeitado · Depreciado · Substituído por ADR-XXXX |
| **Data** | AAAA-MM-DD (da última mudança de status) |
| **Escopo** | Lab · ControlPay atual · Arquitetura-alvo (AWS) · Organização/times |
| **Decisor(es)** | Quem bate o martelo |
| **Consultados** | Quem opinou antes (tech leads, segurança, produto, finanças) |
| **Informados** | Quem precisa saber depois |
| **Tipo de porta** | 🚪 Mão única (caro reverter) · 🔁 Mão dupla (barato reverter) |
| **Revisar em** | Data ou gatilho objetivo (ver "Gatilhos de revisão") |

---

## 1. Contexto e problema
O que está acontecendo, por que precisa decidir **agora**, e o que acontece se não decidir.
Fatos e números, não opiniões. Linkar incidentes, métricas, custos.

## 2. Direcionadores da decisão (critérios)
Critérios explícitos, com peso. Defina **antes** de avaliar as opções (evita racionalizar a favorita).

| # | Critério | Peso (1–3) | Por quê importa aqui |
|---|---|---|---|
| C1 | ex.: Custo total (licença + infra + operação) | 3 | |
| C2 | ex.: Esforço operacional / skill do time | 3 | |
| C3 | ex.: Segurança e PCI DSS | 3 | |
| C4 | ex.: Portabilidade (Azure → AWS) | 2 | |
| C5 | ex.: Maturidade / ecossistema | 1 | |

**Restrições inegociáveis** (eliminam opções, não pontuam): ex.: "nenhum PAN sai do perímetro", "dados no Brasil".

## 3. Opções consideradas
1. **Opção A** — uma linha
2. **Opção B** — uma linha
3. **Opção C** — uma linha
4. *(Sempre considerar)* **Não fazer nada / manter como está**

## 4. Análise das opções

### Opção A — <nome>
- **Como funciona:** 2–3 linhas.
- ✅ Prós:
- ❌ Contras:
- 💰 Custo estimado: (com premissas)
- 🔁 Reversibilidade: o que custaria sair dela depois?

*(repetir para cada opção)*

### Matriz de decisão

| Critério (peso) | A | B | C | Nada |
|---|---|---|---|---|
| C1 (3) | | | | |
| C2 (3) | | | | |
| ... | | | | |
| **Total ponderado** | | | | |

> A matriz ajuda a conversa; ela não decide sozinha. Se o total contradiz a intuição, investigue o porquê e registre.

## 5. Decisão
"Escolhemos **Opção X** porque <ligar aos critérios C1, C3...>."
Se for decisão parcial ou provisória, dizer explicitamente o que ficou em aberto.

## 6. Consequências
- **Positivas:**
- **Negativas / custos que aceitamos:**
- **Riscos e mitigação:**
- **O que muda para o time** (processo, skills, on-call):

## 7. Validação e medição
Como saberemos que a decisão foi boa? Métrica + alvo + quando medir.
Ex.: "custo de ingestão ≤ R$ X/mês após 60 dias"; "MTTD de incidentes de fila < 5 min".

## 8. Gatilhos de revisão
Eventos objetivos que reabrem este ADR. Ex.: "volume de telemetria > N GB/dia", "início da migração para EKS", "mudança de licença do fornecedor".

## 9. Execução
- Pacote de delegação: link
- Dono da implementação:
- Prazo / marco:

## 10. Referências
Links para docs oficiais, POCs do Lab, benchmarks, discussões.

---
### Histórico
| Data | Mudança | Autor |
|---|---|---|
| | Criado (Proposto) | |
