# C4 — Nível 3: Diagrama de Componentes — Operations Service

## Diagrama

```mermaid
C4Component
    title FluxoCaixa.Operations — Diagrama de Componentes (Nível 3)

    Container_Boundary(operationsApi, "Operations API (host)") {
        Component(endpoints, "Endpoints Minimal API", "C# / ASP.NET Core", "Define rotas HTTP. Aplica [Authorize]. Delega para handlers via DI.")
        Component(authMiddleware, "JWT Middleware", "ASP.NET Core Authentication", "Valida Bearer token HS256. Popula HttpContext.User com claims.")
        Component(correlationMiddleware, "CorrelationId Middleware", "C# Middleware", "Lê ou gera X-Correlation-ID. Injeta no LogContext do Serilog.")
        Component(rateLimiter, "Rate Limiter", "ASP.NET Core Rate Limiting", "300 req/min por usuário/IP. Proteção anti-abuso (DoS).")
        Component(exceptionHandler, "GlobalExceptionHandler Middleware", "C# Middleware", "Captura exceções não tratadas. Retorna 500 JSON padronizado.")
    }

    Container_Boundary(operationsApp, "Operations Application") {
        Component(createHandler, "CreateTransactionHandler", "ICommandHandler", "1. Valida command via FluentValidation.\n2. Verifica idempotência.\n3. Cria Transaction via factory.\n4. Persiste + dispara Outbox.")
        Component(getByIdHandler, "GetTransactionByIdHandler", "IQueryHandler", "Busca lançamento por TransactionId. Retorna DTO ou NotFound.")
        Component(getByDateHandler, "GetTransactionsByDateHandler", "IQueryHandler", "Lista lançamentos de um comerciante por data.")
        Component(validator, "CreateTransactionCommandValidator", "FluentValidation AbstractValidator", "Valida Amount > 0, MerchantId não-vazio, Currency 3 letras, OccurredAt, IdempotencyKey.")
        Component(authServiceIface, "IAuthService", "Interface", "Contrato para login, refresh e geração de JWT.")
    }

    Container_Boundary(operationsDomain, "Operations Domain") {
        Component(transaction, "Transaction", "Aggregate Root", "CreateCredit / CreateDebit (factory methods).\nInvariantes: merchantId não-vazio, money válido.\nDispara TransactionCreatedEvent.")
        Component(money, "Money", "Value Object", "Amount (> 0) + Currency (ISO-4217, 3 letras).\nRetorna Result<Money> em vez de exceção.")
        Component(idempotencyKey, "IdempotencyKey", "Value Object", "Máx. 128 chars. Create() valida; FromTrusted() para EF Core.")
        Component(transactionType, "TransactionType", "Value Object (rich enum)", "Credit ou Debit. Validação de string case-sensitive.")
        Component(domainEvent, "TransactionCreatedEvent", "Domain Event", "Encapsulado no aggregate. Capturado pelo DbContext.")
    }

    Container_Boundary(operationsInfra, "Operations Infrastructure") {
        Component(dbContext, "TransactionDbContext", "EF Core DbContext", "SaveChangesAsync intercepta DomainEvents e persiste OutboxMessages na mesma transação.")
        Component(outboxWorker, "OutboxPublisherWorker", "BackgroundService", "Ciclo: lê Pending → publica via IPublishEndpoint → atualiza status.\nPolly: retry 3x backoff exponencial (2s→4s→8s) + jitter.")
        Component(transactionRepo, "TransactionRepository", "ITransactionRepository", "FindByIdAsync, FindByIdempotencyKeyAsync, AddAsync, SaveChangesAsync.")
        Component(authServiceImpl, "AuthService", "IAuthService impl", "Login com BCrypt.Verify. Gera JWT HS256. Gerencia refresh token.")
        Component(userRepo, "UserRepository", "IUserRepository", "FindByEmailAsync para autenticação.")
        Component(telemetry, "OperationsTelemetry", "ActivitySource + Meter", "Spans de domínio. Counters: transactions.created, outbox.published, outbox.failed. Gauge: outbox.pending.")
    }

    Rel(endpoints, createHandler, "Dispatch CreateTransactionCommand")
    Rel(endpoints, getByIdHandler, "Dispatch GetTransactionByIdQuery")
    Rel(endpoints, getByDateHandler, "Dispatch GetTransactionsByDateQuery")
    Rel(endpoints, authServiceIface, "Login / Refresh")

    Rel(createHandler, validator, "Valida command")
    Rel(createHandler, transaction, "Transaction.CreateCredit / CreateDebit")
    Rel(createHandler, transactionRepo, "AddAsync + SaveChangesAsync")

    Rel(transaction, money, "Compõe Money")
    Rel(transaction, idempotencyKey, "Compõe IdempotencyKey")
    Rel(transaction, transactionType, "Compõe TransactionType")
    Rel(transaction, domainEvent, "Adiciona ao DomainEvents")

    Rel(transactionRepo, dbContext, "Usa DbContext")
    Rel(dbContext, domainEvent, "Converte em OutboxMessage (mesma tx)")
    Rel(outboxWorker, dbContext, "Lê outbox_messages Pending")
    Rel(outboxWorker, telemetry, "Registra métricas por ciclo")

    Rel(authServiceImpl, userRepo, "FindByEmailAsync")
```

## Fluxo Principal: POST /api/transactions

```
Cliente → [JWT Middleware] → [Rate Limiter] → [CorrelationId] → Endpoint
  → CreateTransactionHandler
      → Validator (FluentValidation)
      → ITransactionRepository.FindByIdempotencyKeyAsync (check idempotência)
      → Transaction.CreateCredit/CreateDebit (domain factory)
      → ITransactionRepository.AddAsync
      → DbContext.SaveChangesAsync
          → Intercepta TransactionCreatedEvent
          → INSERT cash_entries + INSERT outbox_messages (mesma transação)
  ← 201 Created + TransactionDto

[Em background]
OutboxPublisherWorker
  → SELECT outbox_messages WHERE status = 'Pending'
  → IPublishEndpoint.Publish<TransactionCreatedMessage>
  → UPDATE outbox_messages SET status = 'Published'
  → [RabbitMQ] → Consolidation API Consumer
```
