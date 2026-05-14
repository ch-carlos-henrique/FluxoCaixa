# ADR-003 — Consistência Eventual entre Lançamentos e Consolidado

## Status
Aceito — 13/05/2026

## Contexto

Com a separação em dois serviços (ADR-001) e a comunicação assíncrona via Transactional Outbox (ADR-002), não é possível manter consistência forte entre o registro de um lançamento e o saldo consolidado sem acoplar a disponibilidade dos dois serviços.

O sistema deve escolher explicitamente entre **disponibilidade** e **consistência imediata** (Teorema CAP / PACELC).

O NFR central é inequívoco: **o serviço de lançamentos não pode ficar indisponível**.

## Decisão

Aceitar **consistência eventual** como modelo de consistência para o saldo diário:

- O saldo consolidado pode estar alguns segundos atrasado em relação aos lançamentos.
- Os lançamentos são a **fonte de verdade** (`cash_entries`); o consolidado é uma **projeção** derivada.
- O `OutboxPublisherWorker` garante que, eventualmente, todos os lançamentos serão refletidos no Consolidado.
- O `TransactionCreatedConsumer` usa idempotência via `processed_messages` para evitar dupla contagem em caso de reentrega (ver ADR-008).

### Garantias fornecidas

| Propriedade | Garantia |
|---|---|
| Disponibilidade dos lançamentos | Nunca bloqueada pela disponibilidade do Consolidado |
| Entrega do evento | "At-least-once" (Outbox + retry) |
| Idempotência no consumidor | "Exactly-once processing" via `processed_messages` |
| Consistência eventual | O saldo refletirá todos os lançamentos após processamento do Outbox |

## Consequências

### Positivas

- Disponibilidade máxima dos lançamentos em qualquer cenário de falha parcial.
- Resiliência: falhas do Consolidado são toleradas sem perda de dados.
- Modelo mental simples: lançamentos são sempre aceitos; consolidado é atualizado assincronamente.

### Negativas / Trade-offs

- Comerciante pode ver saldo do dia desatualizado por alguns segundos após um lançamento.
- Consultas que exigem saldo exato em tempo real não são atendidas por este modelo.
- Requer monitoramento do lag do Outbox (métrica `outbox.messages.pending` — ver ADR-006).
- Não é adequado se o negócio exigir prevenção de overdraft em tempo real (requereria consistência forte ou saga síncrona).

## Alternativas Consideradas

| Alternativa | Motivo da Rejeição |
|---|---|
| Consistência forte (transação distribuída / 2PC) | Lançamentos ficam bloqueados enquanto Consolidado estiver indisponível — viola NFR central |
| Saga síncrona (choreography com compensação) | Mesmo problema de disponibilidade: o lançamento aguarda confirmação do Consolidado |
| CQRS com read model síncrono no mesmo serviço | Elimina a separação de domínios; viola o requisito de serviços independentes |

## Relação com Princípios SOLID

- **SRP**: o Consolidado é responsável apenas por agregar; não valida nem processa lançamentos originais.
- **DIP**: o Consolidado depende de contratos de mensagem (`TransactionCreatedMessage`), não de chamadas diretas ao serviço de Lançamentos.
