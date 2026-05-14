# Arquitetura do Sistema FluxoCaixa

Documentação arquitetural do sistema de controle de fluxo de caixa diário.

---

## Visão Geral

O FluxoCaixa é composto por dois serviços independentes que se comunicam de forma assíncrona:

| Serviço | Porta | Responsabilidade |
|---|---|---|
| **Operations API** | 5001 | Registrar lançamentos (créditos e débitos), autenticar usuários |
| **Consolidation API** | 5002 | Calcular e expor saldo diário consolidado por comerciante |

**NFR central**: o serviço de Lançamentos nunca pode ficar indisponível se o Consolidado cair.

---

## Diagramas C4

| Nível | Arquivo | Descrição |
|---|---|---|
| Contexto (L1) | [c4/01-context.md](c4/01-context.md) | Visão externa: atores e sistema |
| Containers (L2) | [c4/02-containers.md](c4/02-containers.md) | Serviços, bancos e broker |
| Componentes (L3) | [c4/03-components.md](c4/03-components.md) | Internos do serviço de Lançamentos |

---

## Decisões Arquiteturais (ADRs)

| ADR | Título | Status |
|---|---|---|
| [ADR-001](adr/ADR-001-two-services-async.md) | Separação em dois serviços com comunicação assíncrona | Aceito |
| [ADR-002](adr/ADR-002-transactional-outbox.md) | Transactional Outbox Pattern | Aceito |
| [ADR-003](adr/ADR-003-eventual-consistency.md) | Consistência eventual entre Lançamentos e Consolidado | Aceito |
| [ADR-004](adr/ADR-004-clean-architecture-ddd.md) | Clean Architecture com DDD Tático | Aceito |
| [ADR-005](adr/ADR-005-jwt-auth.md) | Autenticação JWT Bearer HS256 com Refresh Token | Aceito |
| [ADR-006](adr/ADR-006-observability-opentelemetry.md) | Observabilidade com OpenTelemetry e Serilog | Aceito |
| [ADR-007](adr/ADR-007-resilience-polly.md) | Resiliência com Polly v8 — Retry no OutboxPublisherWorker | Aceito |
| [ADR-008](adr/ADR-008-idempotency.md) | Idempotência via Idempotency-Key e Processed Messages | Aceito |

---

## Resumo das Decisões Chave

### Por que dois serviços em vez de monolito?
O NFR central exige que o serviço de Lançamentos seja independente do Consolidado. Com um monolito, uma falha no módulo de consolidação derruba os lançamentos. → [ADR-001](adr/ADR-001-two-services-async.md)

### Como garantir que o evento seja publicado mesmo com falha do broker?
Transactional Outbox Pattern: o evento é gravado no banco **na mesma transação** do lançamento. Um worker independente publica do banco para o broker, com retry. → [ADR-002](adr/ADR-002-transactional-outbox.md)

### O saldo consolidado sempre está atualizado em tempo real?
Não. O sistema aceita **consistência eventual** — o saldo pode estar alguns segundos atrasado. Lançamentos são a fonte de verdade; o consolidado é uma projeção assíncrona. → [ADR-003](adr/ADR-003-eventual-consistency.md)

### Por que Clean Architecture?
Para isolar o domínio de infraestrutura, permitindo testes sem banco ou broker, e evitar DDD anêmico. A regra de dependência (sempre para dentro) é verificada em tempo de CI por testes de arquitetura. → [ADR-004](adr/ADR-004-clean-architecture-ddd.md)

### Por que JWT manual sem ASP.NET Core Identity?
Identity traz 5 tabelas e 3 managers para um microserviço que só precisa emitir tokens — over-engineering. BCrypt work factor 12 segue OWASP. → [ADR-005](adr/ADR-005-jwt-auth.md)

### Como rastrear falhas em produção?
OpenTelemetry (traces + métricas) + Serilog (logs JSON estruturados) + Correlation ID. Vendor-neutral: exporter pode ser trocado sem alterar código. → [ADR-006](adr/ADR-006-observability-opentelemetry.md)

### O que acontece se o RabbitMQ ficar instável?
Retry 3x com backoff exponencial (2s → 4s → 8s + jitter) via Polly v8. Após 3 falhas: status `Failed` na tabela `outbox_messages` para diagnóstico. → [ADR-007](adr/ADR-007-resilience-polly.md)

### Como evitar lançamentos duplicados em retries?
Header `Idempotency-Key` na API + tabela `processed_messages` no consumer. Índices UNIQUE no banco como última linha de defesa. → [ADR-008](adr/ADR-008-idempotency.md)

---

## Stack Tecnológica

| Componente | Tecnologia |
|---|---|
| Runtime | .NET 10 (LTS) |
| API | ASP.NET Core Minimal APIs |
| ORM | EF Core 10 + Npgsql |
| Banco de dados | PostgreSQL 16 |
| Mensageria | RabbitMQ 3.13 + MassTransit 8.5 |
| Autenticação | JWT Bearer HS256 + BCrypt.Net-Next |
| Validação | FluentValidation 11 |
| Resiliência | Microsoft.Extensions.Resilience (Polly v8) |
| Observabilidade | OpenTelemetry + Serilog + prometheus-net |
| Testes | xUnit + FluentAssertions + NSubstitute + NetArchTest |
| Containerização | Docker + docker-compose |
| CI/CD | GitHub Actions |
