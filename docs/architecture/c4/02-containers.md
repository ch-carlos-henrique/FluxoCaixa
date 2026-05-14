# C4 — Nível 2: Diagrama de Containers

## Diagrama

```mermaid
C4Container
    title FluxoCaixa — Diagrama de Containers (Nível 2)

    Person(comerciante, "Comerciante", "Registra lançamentos e consulta saldo consolidado")

    System_Boundary(fluxoCaixa, "FluxoCaixa") {
        Container(operationsApi, "Operations API", ".NET 10 Minimal API\nPorta 5001", "Registra lançamentos de crédito e débito. Emite e autentica usuários. Persiste eventos no Outbox atomicamente.")

        Container(outboxWorker, "OutboxPublisher Worker", ".NET BackgroundService\n(in-process com Operations API)", "Lê mensagens Pending no banco a cada ciclo e publica no RabbitMQ. Retry 3x com backoff exponencial (Polly v8).")

        Container(consolidationApi, "Consolidation API", ".NET 10 Minimal API\nPorta 5002", "Consome eventos de lançamento via MassTransit. Calcula e expõe saldo diário consolidado.")

        ContainerDb(operationsDb, "Operations DB", "PostgreSQL 16", "Tabelas: cash_entries, outbox_messages, users.\nÍndices: (merchant_id, idempotency_key) UNIQUE, (status, created_at).")

        ContainerDb(consolidationDb, "Consolidation DB", "PostgreSQL 16", "Tabelas: daily_balances, processed_messages.\nÍndices: (merchant_id, date) UNIQUE, (event_id) UNIQUE.")

        Container(messageBroker, "Message Broker", "RabbitMQ 3.13\nPorta 5672 / 15672", "Exchange: transaction-created. Desacopla temporalmente produção e consumo de eventos. DLQ habilitado.")
    }

    Rel(comerciante, operationsApi, "POST /api/transactions\nPOST /api/auth/login\nGET /api/transactions/{id}", "HTTPS / JWT")
    Rel(comerciante, consolidationApi, "GET /api/consolidation/daily\nGET /api/consolidation/range", "HTTPS / JWT")

    Rel(operationsApi, operationsDb, "Persiste cash_entries + outbox_messages\n(mesma transação EF Core)", "TCP / EF Core")
    Rel(outboxWorker, operationsDb, "Lê Pending; atualiza status Published/Failed", "TCP / EF Core")
    Rel(outboxWorker, messageBroker, "Publica TransactionCreatedMessage", "AMQP / MassTransit")

    Rel(consolidationApi, messageBroker, "Consome TransactionCreatedMessage", "AMQP / MassTransit")
    Rel(consolidationApi, consolidationDb, "Lê e atualiza daily_balances;\npersiste processed_messages", "TCP / EF Core")
```

## Descrição dos Containers

| Container | Tecnologia | Responsabilidade Principal |
|---|---|---|
| Operations API | .NET 10 Minimal API | Registrar lançamentos, autenticar usuários, expor endpoints REST |
| OutboxPublisher Worker | .NET BackgroundService | Publicar eventos pendentes no broker com retry (co-residente na Operations API) |
| Consolidation API | .NET 10 Minimal API | Consumir eventos, calcular saldo diário, expor endpoints de consulta |
| Operations DB | PostgreSQL 16 | Persistência de lançamentos, outbox e usuários |
| Consolidation DB | PostgreSQL 16 | Persistência de saldos consolidados e controle de idempotência |
| Message Broker | RabbitMQ 3.13 | Transporte assíncrono de eventos entre os dois serviços |

## Decisões Arquiteturais Relevantes neste Nível

- **Sem Redis**: PostgreSQL com índices adequados é suficiente para 50 RPS — Redis documentado como evolução para escala maior (ADR-004).
- **Sem API Gateway**: rate limiting implementado diretamente em cada API (ADR-009 — evolução futura).
- **Bancos separados**: cada serviço tem seu próprio banco — bounded contexts com schema isolado (tabelas de histórico de migração separadas: `__ef_migrations_history_ops` e `__ef_migrations_history_cons`).
- **OutboxWorker co-residente**: roda in-process com a Operations API para compartilhar o mesmo `DbContext` sem lock distribuído.
