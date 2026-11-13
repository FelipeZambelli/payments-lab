# Pacote de delegação — <Nome da iniciativa>

> Objetivo do template: o tech lead consegue executar **sem precisar voltar para perguntar o óbvio**, e você consegue acompanhar **sem microgerenciar**.
> Regra de ouro: delegue o **problema e o critério de sucesso**, não a solução. A solução só entra aqui se já estiver decidida em ADR.

| Campo | Valor |
|---|---|
| **Dono (accountable)** | Tech lead responsável |
| **Executores** | Pessoas/time |
| **Patrocinador** | Felipe (EM) |
| **Início / marco final** | AAAA-MM-DD → AAAA-MM-DD |
| **ADRs relacionados** | ADR-NNNN |
| **Tópico da trilha** | ex.: 1. Observabilidade |

---

## 1. Contexto e problema
Por que isso importa para o negócio/produto. O que dói hoje (incidente, custo, risco, lentidão). 3–6 linhas, com números quando houver.

## 2. Resultado esperado
Como o mundo fica quando isso estiver pronto. Uma frase clara.

## 3. Critério de sucesso (mensurável)
| Métrica | Linha de base hoje | Alvo | Como medir |
|---|---|---|---|
| | | | |

## 4. Decisões já tomadas vs. em aberto
- **Já decidido (não rediscutir sem fato novo):**
  - ...
- **Em aberto (você decide):**
  - ...
- **Em aberto (propõe e decidimos juntos):**
  - ...

## 5. Restrições
- 🔒 **Segurança / PCI:** ex.: nenhum PAN/PII em logs/telemetria; segredos só via Key Vault.
- 💰 **Custo:** teto mensal, necessidade de aprovação acima de X.
- ⏱️ **Prazo / janelas:** janelas de mudança, períodos de congelamento (fim de mês, Black Friday).
- 🧱 **Técnicas:** stack legado (.NET Framework 4.6.2, App Service Windows), dependências de outros times.

## 6. Nível de autonomia combinado
Marcar **um** (escala de delegação):
- [ ] 1 — **Investiga e me traz os fatos**; eu decido.
- [ ] 2 — **Propõe opções com recomendação**; decidimos juntos.
- [ ] 3 — **Decide e me informa antes** de executar.
- [ ] 4 — **Decide, executa e me informa depois.**
- [ ] 5 — **Total autonomia**; só reporta no checkpoint.

**Me escale imediatamente se:** ex.: risco de indisponibilidade em produção, custo acima do teto, dado sensível exposto, prazo em risco > 1 semana.

## 7. Entregáveis
- [ ] ...
- [ ] Documentação/runbook atualizado
- [ ] Demo para o time (quando aplicável)

## 8. Checkpoints
| Quando | Formato | O que levar |
|---|---|---|
| Semana 1 | 1:1 de 15 min | Plano e riscos |
| Meio | Async (doc/PR) | Progresso vs. critério |
| Final | Demo + retro | Métricas, aprendizados |

## 9. Recursos e apoio
Material de estudo (link do Lab/workshop), pessoas de referência, orçamento, acessos necessários.

## 10. Riscos conhecidos
| Risco | Prob. | Impacto | Mitigação |
|---|---|---|---|
| | | | |

## 11. Ensino e disseminação
Como o conhecimento sai da cabeça do dono (bus factor): pareamento, tech talk, runbook, review cruzado.

---
### Registro de acompanhamento
| Data | Status (🟢🟡🔴) | Nota |
|---|---|---|
| | | |
