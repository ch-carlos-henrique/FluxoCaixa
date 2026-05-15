# FluxoCaixa

Sistema de controle de fluxo de caixa diário com dois serviços independentes (.NET 10).

> **NFR central**: o serviço de Lançamentos nunca pode ficar indisponível se o Consolidado cair.

---

## Serviços

| Serviço | Porta | Responsabilidade |
|---|---|---|
| **Operations API** | 5001 | Registrar lançamentos de crédito e débito, autenticar usuários (JWT) |
| **Consolidation API** | 5002 | Calcular e expor saldo diário consolidado por comerciante |

## Fluxo de Dados

```
Comerciante
    │
    ├─── POST /api/transactions ──► Operations API (5001)
    │                                    │
    │                               [outbox_messages]  ← mesma transação
    │                                    │
    │                               OutboxPublisher Worker
    │                                    │
    │                               RabbitMQ
    │                                    │
    │                               Consolidation API Consumer
    │                                    │
    │                               [daily_balances]
    │
    └─── GET /api/consolidation/daily ──► Consolidation API (5002)
```

O lançamento persiste em `cash_entries` e uma intenção de publicação em `outbox_messages` **na mesma transação**. A publicação no broker é feita de forma assíncrona pelo `OutboxPublisherWorker`, garantindo que mesmo uma queda do RabbitMQ ou do Consolidado não cause perda de dados.

## Documentação

- [Visão Geral da Arquitetura](architecture/ARCHITECTURE.md)
- Diagramas C4:
  - [Contexto (L1)](architecture/c4/01-context.md)
  - [Containers (L2)](architecture/c4/02-containers.md)
  - [Componentes (L3)](architecture/c4/03-components.md)
- ADRs — Decisões Arquiteturais:
  - [ADR-001 — Dois Serviços + Comunicação Assíncrona](architecture/adr/ADR-001-two-services-async.md)
  - [ADR-002 — Transactional Outbox](architecture/adr/ADR-002-transactional-outbox.md)
  - [ADR-003 — Consistência Eventual](architecture/adr/ADR-003-eventual-consistency.md)
  - [ADR-004 — Clean Architecture + DDD](architecture/adr/ADR-004-clean-architecture-ddd.md)
  - [ADR-005 — JWT Bearer HS256](architecture/adr/ADR-005-jwt-auth.md)
  - [ADR-006 — OpenTelemetry + Serilog](architecture/adr/ADR-006-observability-opentelemetry.md)
  - [ADR-007 — Polly v8 Retry](architecture/adr/ADR-007-resilience-polly.md)
  - [ADR-008 — Idempotência](architecture/adr/ADR-008-idempotency.md)
