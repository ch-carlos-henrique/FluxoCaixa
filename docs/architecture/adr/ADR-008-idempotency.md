# ADR-008 — Idempotência via Idempotency-Key e Processed Messages

## Status
Aceito — 13/05/2026

## Contexto

Em sistemas distribuídos com retry automático (ADR-007) e entrega "at-least-once" (ADR-002), é inevitável que mensagens e requisições sejam entregues mais de uma vez. Em um sistema financeiro, duplicação de lançamentos resulta em saldos incorretos e perda de confiança dos usuários — a consequência é diretamente monetária.

Dois pontos de duplicidade independentes precisam ser tratados:

1. **API de Lançamentos**: cliente pode reenviar a mesma requisição por timeout, falha de rede ou retry automático do SDK.
2. **Consumer do Consolidado**: MassTransit pode reentregar a mesma mensagem por falha de ACK, restart do consumer ou reprocessamento de DLQ.

## Decisão

### Idempotência na API de Lançamentos

- O cliente envia o header `Idempotency-Key` (UUID ou string qualquer, máx. 128 chars) em cada requisição de criação.
- O `CreateTransactionHandler` verifica a existência do `idempotency_key` na tabela `cash_entries` **antes** de criar um novo lançamento (via `ITransactionRepository.FindByIdempotencyKeyAsync`).
- **Se já existe**: retorna o lançamento existente com status 200 — sem nova persistência, sem novo evento no Outbox.
- **Se não existe**: cria o lançamento normalmente.
- Índice `UNIQUE (merchant_id, idempotency_key)` na tabela `cash_entries` garante a invariante no banco como última linha de defesa.

### Idempotência no Consumer do Consolidado

- Antes de processar um `TransactionCreatedMessage`, o `TransactionCreatedConsumer` verifica a tabela `processed_messages` via `IProcessedMessageRepository.ExistsAsync(messageId)`.
- **Se já processado**: mensagem é ignorada com ACK (sem reprocessamento, sem erro).
- **Se não processado**: processa `DailyBalance.Apply(...)`, persiste a atualização e registra `AddAsync(messageId)` na mesma operação.
- Índice `UNIQUE (event_id)` na tabela `processed_messages` garante a invariante no banco.

### Schema das tabelas

**`cash_entries` (índice de idempotência):**
```sql
CREATE UNIQUE INDEX ix_cash_entries_merchant_idempotency
ON cash_entries (merchant_id, idempotency_key);
```

**`processed_messages`:**

| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | UUID | Identificador do registro |
| `event_id` | UUID | `MessageId` do `TransactionCreatedMessage` |
| `processed_at` | TIMESTAMPTZ | Momento do processamento |

## Consequências

### Positivas

- Retries do cliente são completamente seguros: mesma `Idempotency-Key` → mesma resposta, sem duplicação.
- Reentrega "at-least-once" do broker não causa dupla contagem no saldo diário.
- Audit trail completo em `processed_messages` — rastreável para diagnóstico.
- Proteção em dois níveis: lógica de aplicação (handler/consumer) + restrição de banco (índice único).

### Negativas / Trade-offs

- `Idempotency-Key` deve ser gerado e gerenciado pelo cliente — requer disciplina na integração.
- Tabela `processed_messages` cresce indefinidamente — requer política de retenção/limpeza (ex: purgar registros com mais de 30 dias).
- Lookup adicional no banco a cada requisição e a cada mensagem consumida.
- Janela de idempotência: se `processed_messages` for limpo, mensagens antigas podem ser reprocessadas.

## Alternativas Consideradas

| Alternativa | Motivo da Rejeição |
|---|---|
| Sem idempotência | Duplicação de dados em qualquer retry — inaceitável em sistema financeiro |
| Redis para controle de idempotência | Dependência adicional de infraestrutura; PostgreSQL com índice único é suficiente para o volume atual (50 RPS) |
| Exactly-once delivery nativo (Kafka transactions) | Requer troca de broker (RabbitMQ → Kafka) — over-engineering para o escopo |
| Idempotência apenas no banco (sem check no handler) | Race condition possível sem check prévio em alta concorrência; também prejudica a resposta ao cliente (erro 500 vs. 200 idempotente) |

## Relação com Princípios SOLID

- **SRP**: `IProcessedMessageRepository` tem responsabilidade única — rastrear mensagens processadas.
- **OCP**: novas fontes de mensagens ou novos tipos de evento podem adotar o mesmo mecanismo de idempotência sem modificar a infraestrutura existente.
- **DIP**: `IProcessedMessageRepository` é definido no Application layer; implementação concreta em Infrastructure.
