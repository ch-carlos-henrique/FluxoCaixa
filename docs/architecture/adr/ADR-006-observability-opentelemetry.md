# ADR-006 — Observabilidade com OpenTelemetry e Serilog

## Status
Aceito — 13/05/2026

## Contexto

Sistemas distribuídos requerem observabilidade para diagnóstico de falhas, rastreamento de requisições entre serviços e monitoramento de performance. Sem uma estratégia de observabilidade, problemas em produção são difíceis de diagnosticar e o comportamento do sistema sob carga é opaco.

Os três pilares da observabilidade são: **traces** (rastreamento distribuído), **métricas** e **logs**.

## Decisão

### Traces e Métricas: OpenTelemetry

Padrão aberto e vendor-neutral, com suporte nativo no .NET 10.

- **Pacotes**: `OpenTelemetry.Extensions.Hosting`, `Instrumentation.AspNetCore`, `Instrumentation.Http`, `Exporter.Console`.
- `ActivitySource` customizado por serviço para spans de negócio (`FluxoCaixa.Operations`, `FluxoCaixa.Consolidation`).

**Métricas customizadas (Operations):**

| Métrica | Tipo | Descrição |
|---|---|---|
| `transactions.created` | Counter | Total de lançamentos criados |
| `outbox.messages.published` | Counter | Total de mensagens publicadas no broker |
| `outbox.messages.failed` | Counter | Total de mensagens que falharam após retry |
| `outbox.messages.pending` | ObservableGauge | Mensagens pendentes no outbox (lag do worker) |

### Logs: Serilog

- `Serilog.AspNetCore` com `JsonFormatter` — logs estruturados em JSON (query por campo).
- Enriquecedores: `WithMachineName`, `WithThreadId`.
- `CorrelationIdMiddleware` injeta `CorrelationId` no `LogContext` — propaga entre todos os logs de uma requisição.
- `UseSerilogRequestLogging` substitui o logging padrão de requisições do ASP.NET Core.

### Exportação

- **Desenvolvimento / Docker**: Console Exporter — sem necessidade de coletor externo.
- **Produção** (evolução): substituir por OTLP Exporter (Jaeger, Grafana Tempo, Azure Monitor) sem alterar código de instrumentação.

### Health Checks

- `/health` via `Microsoft.Extensions.Diagnostics.HealthChecks`.
- `AddDbContextCheck<TransactionDbContext>` e `AddDbContextCheck<DailyConsolidationDbContext>` verificam conectividade com PostgreSQL.

### Métricas de Infraestrutura

- `/metrics` (Prometheus) via `prometheus-net.AspNetCore` — permite scraping por Prometheus/Grafana.

## Consequências

### Positivas

- **Vendor-neutral**: trocar exportador (Console → Jaeger → Grafana → Azure Monitor) sem alterar código de instrumentação.
- `CorrelationId` propaga contexto entre todos os logs de uma requisição, facilitando diagnóstico.
- Métrica `outbox.messages.pending` permite alertar sobre lag de processamento antes que afete usuários.
- Métricas de negócio (`transactions.created`) habilitam dashboards de produto sem ETL adicional.

### Negativas / Trade-offs

- Console Exporter não é adequado para produção (sem retenção, sem query, sem alertas) — requer Jaeger/Grafana em produção.
- Volume de logs em JSON pode ser verboso em desenvolvimento (filtrar por namespace via `Serilog.MinimumLevel.Override`).

## Alternativas Consideradas

| Alternativa | Motivo da Rejeição |
|---|---|
| DataDog SDK proprietário | Vendor lock-in; custo por host; dificulta migração |
| Application Insights SDK | Vendor lock-in no Azure; Microsoft recomenda OpenTelemetry como caminho oficial |
| Logs apenas em texto plano | Sem capacidade de filtrar/agregar por campo estruturado |
| Sem métricas customizadas | Impossível monitorar lag do outbox e volume de lançamentos sem instrumentação adicional |

## Relação com Princípios SOLID

- **OCP**: novos exportadores são adicionados via configuração, sem modificar código de instrumentação.
- **DIP**: código de domínio não depende de SDK de observabilidade específico — instrumentação ocorre na borda (API/Infrastructure).
- **SRP**: `OperationsTelemetry` e `ConsolidationTelemetry` são classes com responsabilidade única — centralizar as definições de `ActivitySource` e `Meter`.
