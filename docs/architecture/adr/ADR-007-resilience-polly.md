# ADR-007 — Resiliência com Polly v8 (Retry no OutboxPublisherWorker)

## Status
Aceito — 13/05/2026

## Contexto

O `OutboxPublisherWorker` publica mensagens do banco de dados para o RabbitMQ. O broker pode apresentar falhas transitórias: conexão interrompida, sobrecarga momentânea, reinicialização do container. Sem uma política de retry, qualquer falha transitória resulta em mensagens `Pending` acumuladas indefinidamente ou, em uma implementação ingênua, em perda silenciosa de mensagens.

Além disso, em reinicializações simultâneas de múltiplas instâncias do worker, um retry sem jitter pode criar um efeito "thundering herd" — todos os workers tentam publicar ao mesmo tempo, sobrecarregando o broker no momento de recuperação.

## Decisão

Implementar **retry com backoff exponencial e jitter** via `Microsoft.Extensions.Resilience` (Polly v8) no `OutboxPublisherWorker`:

### Política de Retry

| Parâmetro | Valor | Justificativa |
|---|---|---|
| Tentativas máximas | 3 | Equilibra resiliência e tempo de falha detectável |
| Delay base | 2 segundos | Tempo suficiente para recuperação transitória do broker |
| Multiplicador | 2x (exponencial) | Delays: 2s → 4s → 8s |
| Jitter | Habilitado | Distribui retries de múltiplas instâncias no tempo |
| Após falha total | Status `Failed` | DLQ interno — mensagem rastreável para reprocessamento |

### DLQ Interno

Mensagens com status `Failed` ficam na tabela `outbox_messages` para análise e reprocessamento manual ou automático. A métrica `outbox.messages.failed` (ADR-006) permite alertar sobre esse estado.

### Por que NÃO Circuit Breaker neste serviço

O circuit breaker é o padrão correto para **chamadas síncronas** entre serviços (ex: API Gateway → microserviço downstream). Neste sistema, não há chamadas HTTP síncronas entre os dois serviços — a comunicação é exclusivamente assíncrona via RabbitMQ através do Outbox.

Cenários onde circuit breaker seria justificado no futuro:
- Adição de um API Gateway com chamadas síncronas para os microserviços
- Integração com APIs externas de pagamento ou validação de CNPJ
- Consumer do Consolidado fazendo chamadas HTTP para enriquecer dados

## Consequências

### Positivas

- Falhas transitórias do RabbitMQ são toleradas automaticamente sem intervenção manual.
- Jitter evita thundering herd em reinícios simultâneos de múltiplas instâncias.
- Mensagens `Failed` são rastreáveis e reprocessáveis sem perda de dados.
- Separação clara entre falha transitória (retry) e falha permanente (status `Failed`).

### Negativas / Trade-offs

- Mensagens `Failed` requerem processo de reprocessamento (manual ou worker de reconciliação).
- Em falha prolongada do broker (> ~14 segundos por mensagem), o worker acumula mensagens `Failed` — necessário monitoramento ativo da métrica.
- Sem circuit breaker: em falha do broker, o worker continua tentando a cada ciclo (sem abrir circuito).

## Alternativas Consideradas

| Alternativa | Motivo da Rejeição |
|---|---|
| Retry sem backoff (linear) | Pode sobrecarregar o broker já estressado com tentativas em frequência constante |
| Circuit breaker no worker | Sem chamadas síncronas entre serviços — padrão correto mas não aplicável aqui; documentado como evolução futura |
| Dead Letter Queue nativo do RabbitMQ | Complementar ao Outbox, não substituto; o Outbox já fornece persistência e rastreamento no banco |
| Sem retry | Falha transitória resulta em mensagem nunca publicada — inconsistência entre lançamentos e saldo |
| Saga com compensação | Excessivo para este padrão de retentativa local; adequado para fluxos multi-serviço com rollback |

## Relação com Princípios SOLID

- **SRP**: a política de resiliência é encapsulada no `OutboxPublisherWorker`; handlers de domínio e repositórios não conhecem a política de retry.
- **OCP**: a política pode ser alterada (ex: adicionar circuit breaker futuro) sem modificar a lógica de publicação.
- **DIP**: `Microsoft.Extensions.Resilience` fornece abstrações de pipeline; o worker não depende diretamente de tipos concretos do Polly.
