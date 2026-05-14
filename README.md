# FluxoCaixa

Sistema de controle de fluxo de caixa diário com dois serviços independentes (.NET 10).

> **NFR central**: o serviço de Lançamentos nunca pode ficar indisponível se o Consolidado cair.

---

## Visão Geral

```
Comerciante
    │
    ├─── POST /api/transactions ──► Operations API (porta 5001)
    │                                    │
    │                               [outbox_messages]
    │                                    │
    │                               OutboxPublisher Worker
    │                                    │
    │                               RabbitMQ (transaction-created)
    │                                    │
    │                               Consolidation API Consumer
    │                                    │
    │                               [daily_balances]
    │
    └─── GET /api/consolidation/daily ──► Consolidation API (porta 5002)
```

| Serviço | Porta | Responsabilidade |
|---|---|---|
| Operations API | 5001 | Registrar lançamentos, autenticar usuários |
| Consolidation API | 5002 | Calcular e expor saldo diário consolidado |

---

## Pré-requisitos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Docker + Docker Compose)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (para rodar testes localmente)
- [k6](https://k6.io/docs/get-started/installation/) (opcional — para load test)

---

## Como Rodar Localmente

```bash
# 1. Clonar o repositório
git clone https://github.com/ch-carlos-henrique/FluxoCaixa.git
cd FluxoCaixa

# 2. Subir o ambiente completo
docker-compose up --build

# 3. Aguardar as APIs ficarem disponíveis
# Operations API:    http://localhost:5001/health
# Consolidation API: http://localhost:5002/health
# RabbitMQ UI:       http://localhost:15672  (guest / guest)
# OpenAPI (Scalar):  http://localhost:5001/scalar
#                    http://localhost:5002/scalar
```

As migrations são aplicadas automaticamente na inicialização. O seed de desenvolvimento cria os seguintes usuários:

| Email | Senha | Role | merchantId |
|---|---|---|---|
| `admin@fluxocaixa.dev` | `Admin@123` | Admin | — |
| `merchant@fluxocaixa.dev` | `Merchant@123` | Merchant | `00000000-0000-0000-0000-000000000001` |
| `merchant01@fluxocaixa.dev` … `merchant20@fluxocaixa.dev` | `Merchant@123` | Merchant | UUIDs sequenciais |

---

## Como Obter um Token JWT

**Linux / macOS / Git Bash / WSL:**
```bash
curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"merchant@fluxocaixa.dev","password":"Merchant@123"}' \
  | jq -r '.accessToken'
```

**Windows PowerShell:**
```powershell
$response = Invoke-RestMethod -Method Post -Uri http://localhost:5001/api/auth/login `
  -ContentType "application/json" `
  -Body '{"email":"merchant@fluxocaixa.dev","password":"Merchant@123"}'
$token = $response.accessToken
```

---

## Exemplos de Uso (cURL / Git Bash)

> No Windows, use Git Bash, WSL ou substitua `curl` por `Invoke-RestMethod` no PowerShell.

### Registrar um lançamento de crédito

```bash
TOKEN="<token-obtido-acima>"
MERCHANT_ID="00000000-0000-0000-0000-000000000001"

curl -s -X POST http://localhost:5001/api/transactions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d "{
    \"merchantId\": \"$MERCHANT_ID\",
    \"type\": \"Credit\",
    \"amount\": 150.00,
    \"currency\": \"BRL\",
    \"description\": \"Venda no cartão\",
    \"occurredAt\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"
  }" | jq .
```

### Consultar saldo diário consolidado

```bash
curl -s "http://localhost:5002/api/consolidation/daily?merchantId=$MERCHANT_ID&date=$(date +%Y-%m-%d)" \
  -H "Authorization: Bearer $TOKEN" | jq .
```

### Testar idempotência (segundo POST com mesma Idempotency-Key retorna o mesmo lançamento)

```bash
IDEMPOTENCY_KEY="meu-uuid-fixo-aqui"

# Primeira chamada → 201 Created
curl -s -X POST http://localhost:5001/api/transactions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $IDEMPOTENCY_KEY" \
  -d "{\"merchantId\":\"$MERCHANT_ID\",\"type\":\"Debit\",\"amount\":50.00,\"currency\":\"BRL\",\"description\":\"Fornecedor\",\"occurredAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}"

# Segunda chamada com mesma chave → 200 OK com mesmo ID (sem duplicação)
curl -s -X POST http://localhost:5001/api/transactions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $IDEMPOTENCY_KEY" \
  -d "{\"merchantId\":\"$MERCHANT_ID\",\"type\":\"Debit\",\"amount\":50.00,\"currency\":\"BRL\",\"description\":\"Fornecedor\",\"occurredAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}"
```

---

## Como Rodar os Testes

```bash
# Todos os testes (unit + architecture)
dotnet test

# Apenas unit tests das operações
dotnet test tests/FluxoCaixa.Operations.UnitTests/

# Apenas testes de arquitetura
dotnet test tests/FluxoCaixa.Architecture.Tests/

# Com relatório detalhado
dotnet test -v n
```

**Resultado esperado**: 55 testes aprovados (36 Operations unit + 12 Consolidation unit + 7 Architecture).

---

## Demo NFR: Lançamentos Resistem à Queda do Consolidado

```bash
# 1. Derrubar o consolidado
docker-compose stop consolidation-api

# 2. Registrar lançamentos normalmente — deve retornar 201
curl -s -X POST http://localhost:5001/api/transactions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d "{\"merchantId\":\"$MERCHANT_ID\",\"type\":\"Credit\",\"amount\":200.00,\"currency\":\"BRL\",\"description\":\"Teste NFR\",\"occurredAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}"

# 3. Subir o consolidado novamente
docker-compose start consolidation-api

# 4. Aguardar alguns segundos e consultar saldo — deve refletir os lançamentos feitos
curl -s "http://localhost:5002/api/consolidation/daily?merchantId=$MERCHANT_ID&date=$(date +%Y-%m-%d)" \
  -H "Authorization: Bearer $TOKEN" | jq .
```

---

## Load Test (k6)

```bash
# Requer k6 instalado e ambiente Docker rodando
k6 run tests/load/daily-balance-50rps.js
```

**Meta**: 50 RPS no Consolidado, p95 < 200ms, taxa de erro < 5%.

---

## Como Aplicar Migrations Manualmente

```bash
# Operations (se necessário)
dotnet ef database update \
  --project src/FluxoCaixa.Operations.Infrastructure \
  --startup-project src/FluxoCaixa.Operations.API

# Consolidation (se necessário)
dotnet ef database update \
  --project src/FluxoCaixa.Consolidation.Infrastructure \
  --startup-project src/FluxoCaixa.Consolidation.API
```

> Em ambiente Docker, as migrations são aplicadas automaticamente via `ApplyMigrationsAndSeedAsync` no startup das APIs.

---

## Estratégia de Escalabilidade em Produção

- **Escalonamento horizontal stateless**: cada API pode ter N réplicas atrás de um load balancer (ex: Azure Application Gateway, NGINX ou AWS ALB).
- **HTTPS/TLS terminado no balanceador**: APIs internas comunicam via HTTP; TLS na borda.
- **RabbitMQ em cluster**: múltiplos nós com quorum queues para alta disponibilidade do broker.
- **PostgreSQL com réplica de leitura**: separação de carga de leitura/escrita para o Consolidado.

---

## Evoluções Futuras

| Evolução | Justificativa |
|---|---|
| Azure Service Bus (produção) | SLA maior que RabbitMQ self-hosted; outbox compatível sem mudança de código |
| Redis para cache de leitura | Justificável a partir de ~500 RPS ou SLA < 50ms de latência |
| API Gateway centralizado | Rate limiting unificado, auth única, observabilidade centralizada |
| OIDC/Entra ID | Múltiplos provedores de login, SSO corporativo |
| Argon2id em vez de BCrypt | Maior resistência a GPU/ASIC — primeira opção OWASP 2024 |
| Circuit breaker (Polly) | Justificado quando houver chamadas HTTP síncronas entre serviços |
| MediatR com pipeline behaviors | Justificado em sistemas com muitos handlers e cross-cutting concerns |
| SAGA pattern | Fluxos com múltiplos serviços e rollback compensatório |
| Particionamento por merchant_id | Escala acima de 50 RPS com isolamento de dados por comerciante |

---

## Arquitetura e Decisões

- [Visão Geral da Arquitetura](docs/ARCHITECTURE.md)
- [ADR-001: Dois serviços + comunicação assíncrona](docs/architecture/adr/ADR-001-two-services-async.md)
- [ADR-002: Transactional Outbox](docs/architecture/adr/ADR-002-transactional-outbox.md)
- [ADR-003: Consistência eventual](docs/architecture/adr/ADR-003-eventual-consistency.md)
- [ADR-004: Clean Architecture + DDD](docs/architecture/adr/ADR-004-clean-architecture-ddd.md)
- [ADR-005: JWT Bearer HS256](docs/architecture/adr/ADR-005-jwt-auth.md)
- [ADR-006: OpenTelemetry + Serilog](docs/architecture/adr/ADR-006-observability-opentelemetry.md)
- [ADR-007: Polly v8 — Retry](docs/architecture/adr/ADR-007-resilience-polly.md)
- [ADR-008: Idempotência](docs/architecture/adr/ADR-008-idempotency.md)

---

## Estrutura do Repositório

```
FluxoCaixa/
├── src/
│   ├── FluxoCaixa.Operations.API/       # Lançamentos + Auth (porta 5001)
│   ├── FluxoCaixa.Operations.Application/
│   ├── FluxoCaixa.Operations.Domain/
│   ├── FluxoCaixa.Operations.Infrastructure/
│   ├── FluxoCaixa.Consolidation.API/    # Saldo Consolidado (porta 5002)
│   ├── FluxoCaixa.Consolidation.Application/
│   ├── FluxoCaixa.Consolidation.Domain/
│   └── FluxoCaixa.Consolidation.Infrastructure/
├── tests/
│   ├── FluxoCaixa.Operations.UnitTests/
│   ├── FluxoCaixa.Consolidation.UnitTests/
│   ├── FluxoCaixa.Architecture.Tests/
│   └── load/
│       └── daily-balance-50rps.js       # k6 load test
├── docs/
│   ├── ARCHITECTURE.md
│   └── architecture/
│       ├── adr/                         # ADR-001 a ADR-008
│       └── c4/                          # Diagramas C4 (Mermaid)
├── docker-compose.yml
├── docker-compose.override.yml
└── FluxoCaixa.slnx
```

---

## Stack

.NET 10 · EF Core 10 · PostgreSQL 16 · RabbitMQ 3.13 · MassTransit 8.5 · JWT Bearer · BCrypt · FluentValidation · Polly v8 · OpenTelemetry · Serilog · xUnit · Docker

