# ADR-004 — Clean Architecture com DDD Tático

## Status
Aceito — 13/05/2026

## Contexto

Sistemas de domínio financeiro têm regras de negócio complexas que precisam ser testadas independentemente de infraestrutura. Domínios anêmicos (apenas DTOs + serviços procedurais) tornam o código difícil de manter, testar e evoluir.

## Decisão

Adotar **Clean Architecture** com 4 camadas por serviço e **DDD Tático** no domínio.

### Camadas e Regra de Dependência (sempre para dentro)

```
API  →  Application  →  Domain
Infrastructure  →  Application  →  Domain
```

| Camada | Responsabilidade | Dependências |
|---|---|---|
| **Domain** | Entidades ricas, value objects, domain events, interfaces de repositório | Nenhuma externa |
| **Application** | Handlers CQRS, validators, DTOs, interfaces de serviço | Apenas Domain |
| **Infrastructure** | EF Core, repositórios concretos, workers, serviços externos | Application + Domain |
| **API** | Minimal API endpoints, middleware, configuração de DI | Application + Infrastructure |

### DDD Tático — Elementos Implementados

**Entidades Ricas:**
- `Transaction`: factory methods `CreateCredit`/`CreateDebit`, invariantes (merchantId não-vazio), dispara `TransactionCreatedEvent`.
- `DailyBalance`: factory `CreateForMerchant`, método `Apply(transactionType, amount)`, invariante `Balance = TotalCredits - TotalDebits` (calculado, nunca armazenado).

**Value Objects** (imutáveis, com validação embutida):
- `Money` — Amount + Currency ISO-4217 (3 letras)
- `IdempotencyKey` — máximo 128 caracteres
- `TransactionType` — rich enum: `Credit` / `Debit`
- `DailyBalanceDate` — wrapper imutável sobre `DateOnly`
- `TransactionId` — wrapper sobre `Guid`

**Domain Events:**
- `TransactionCreatedEvent` — encapsulado no aggregate, capturado pelo `DbContext.SaveChangesAsync` e convertido em `OutboxMessage` atomicamente.

**Result Pattern:**
- `Result<T>` customizado — sem exceções para fluxo de negócio. Erros são valores, não exceções.

### CQRS sem Biblioteca

Interfaces próprias `ICommandHandler<TCommand>` e `IQueryHandler<TQuery, TResult>` — sem MediatR. Cada handler tem responsabilidade única.

### Bounded Contexts Independentes

Operations e Consolidation são bounded contexts separados. `DailyBalance.Apply` recebe primitivas (`string transactionType, decimal amount`) em vez do `TransactionCreatedEvent` do Operations.Domain, evitando acoplamento entre BCs.

## Consequências

### Positivas

- Domain 100% testável sem infraestrutura (55 testes passando sem dependência de banco ou broker).
- Regras de negócio centralizadas nas entidades — sem vazamento para handlers ou controllers.
- Facilita adição de novos handlers sem modificar código existente (OCP).
- Architecture tests (`NetArchTest`) verificam a regra de dependência em tempo de build/CI.

### Negativas / Trade-offs

- Mais arquivos e camadas comparado a uma solução procedural simples.
- CQRS customizado exige disciplina — sem pipeline behaviors automáticos de cross-cutting concerns (evolução futura: MediatR justificado para sistemas com muitos handlers e middleware de logging/validation).

## Alternativas Consideradas

| Alternativa | Motivo da Rejeição |
|---|---|
| Arquitetura em camadas clássica | Tende a DDD anêmico; regras de negócio vazam para serviços de aplicação |
| Arquitetura hexagonal (ports & adapters) | Equivalente em resultado; nomenclatura diferente — Clean Architecture é mais amplamente conhecida |
| MediatR | Justificável em monolito com muitos handlers e cross-cutting concerns; overhead desnecessário para 2 microserviços menores |
| Sem CQRS | Handlers com múltiplas responsabilidades — mais difícil de testar e evoluir |

## Relação com Princípios SOLID

- **SRP**: cada handler tem responsabilidade única (um comando ou uma query).
- **OCP**: novos handlers são adicionados sem modificar handlers existentes.
- **LSP**: value objects são substituíveis em qualquer contexto que aceite seu tipo base.
- **ISP**: `ICommandHandler` e `IQueryHandler` são interfaces enxutas e focadas.
- **DIP**: domínio depende de `ITransactionRepository` e `IDailyBalanceRepository` (abstrações); Infrastructure implementa essas interfaces.
