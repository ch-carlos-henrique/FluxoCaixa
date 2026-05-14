# ADR-002 — Transactional Outbox Pattern

## Status
Aceito — 13/05/2026

## Contexto

Ao persistir um lançamento, é necessário **publicar um evento** no broker (RabbitMQ) para atualizar o Consolidado. Esse é o problema clássico do "two generals": não é possível garantir atomicidade entre um `INSERT` no banco e um `PUBLISH` no broker em uma única operação sem um mecanismo adicional.

Cenários de falha sem o padrão:

- Publicamos **antes** de salvar → banco falha → evento publicado sem dado persistido → Consolidado contabiliza lançamento inexistente.
- Salvamos **antes** de publicar → broker falha → dado persistido sem evento → Consolidado **nunca** atualiza.

Ambos os cenários resultam em inconsistência permanente.

## Decisão

Implementar o **Transactional Outbox Pattern**:

1. `TransactionDbContext.SaveChangesAsync` intercepta os `DomainEvents` da entidade e persiste um registro na tabela `outbox_messages` **na mesma transação** que o lançamento em `cash_entries`.
2. Um `BackgroundService` (`OutboxPublisherWorker`) lê periodicamente os registros com status `Pending` e os publica no RabbitMQ.
3. Após publicação bem-sucedida, o status é atualizado para `Published`.
4. Após 3 falhas consecutivas (Polly retry — ver ADR-007), o status é atualizado para `Failed` (DLQ interno).

### Schema da tabela `outbox_messages`

| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | UUID | Identificador único da mensagem |
| `event_type` | TEXT | Nome do tipo do evento (ex: `TransactionCreatedEvent`) |
| `payload` | JSON | Serialização do evento |
| `status` | TEXT | `Pending` → `Published` ou `Failed` |
| `created_at` | TIMESTAMPTZ | Momento de criação |
| `published_at` | TIMESTAMPTZ | Momento de publicação bem-sucedida |
| `retry_count` | INT | Número de tentativas realizadas |

## Consequências

### Positivas

- Garantia "at-least-once delivery": o evento **sempre será publicado** após o banco confirmar o lançamento.
- Tolerância a falhas transitórias do broker sem perda de dados.
- Audit trail de publicações na tabela `outbox_messages`.
- Não há dependência de transações distribuídas (2PC) ou saga neste fluxo.

### Negativas / Trade-offs

- Latência adicional: o Consolidado é atualizado de forma assíncrona (segundos após o lançamento).
- Possibilidade de publicação duplicada ("at-least-once") → tratada pelo ADR-008 (Idempotência).
- `OutboxPublisherWorker` deve ser co-localizado com o processo que escreve no banco para acesso ao mesmo `DbContext`.
- Tabela `outbox_messages` cresce com o tempo — requer política de retenção para registros `Published`.

## Alternativas Consideradas

| Alternativa | Motivo da Rejeição |
|---|---|
| Publicar direto no broker após `SaveChangesAsync` | Sem garantia: falha entre banco e broker causa inconsistência permanente |
| Change Data Capture (Debezium / Kafka Connect) | Complexidade de infraestrutura desproporcionalmente alta para o escopo |
| Saga com compensação | Adequado para fluxos multi-serviço complexos; excessivo para este caso de dois serviços com um único evento |
| Azure Service Bus com transações de sessão | Dependência de cloud provider — documentado como evolução futura para produção |

## Relação com Princípios SOLID

- **SRP**: `OutboxPublisherWorker` tem responsabilidade única — publicar mensagens pendentes.
- **OCP**: novos tipos de evento podem ser adicionados sem modificar o worker (lê payload JSON genérico).
- **DIP**: o worker depende de `IPublishEndpoint` (abstração MassTransit), não do transporte concreto (RabbitMQ).
