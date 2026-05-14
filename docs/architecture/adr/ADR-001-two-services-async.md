# ADR-001 — Separação em Dois Serviços com Comunicação Assíncrona

## Status
Aceito — 13/05/2026

## Contexto

O sistema de fluxo de caixa possui dois domínios distintos com requisitos diferentes de disponibilidade:

- **Lançamentos** (Operations): registro de créditos e débitos em tempo real. Alta disponibilidade obrigatória.
- **Consolidado Diário** (Consolidation): agregação dos lançamentos por dia. Pode ter latência de processamento.

**NFR central**: o serviço de lançamentos **não pode ficar indisponível** se o consolidado cair.

Pico esperado: **50 requisições/segundo** no consolidado, com máximo de 5% de perda.

## Decisão

Separar o sistema em dois serviços independentes (`FluxoCaixa.Operations.API` e `FluxoCaixa.Consolidation.API`) que se comunicam de forma **assíncrona via RabbitMQ** com MassTransit.

O serviço de Lançamentos publica eventos `TransactionCreated`; o serviço de Consolidado consome esses eventos de forma independente, sem dependência de disponibilidade do produtor.

## Consequências

### Positivas

- Lançamentos ficam disponíveis mesmo se o Consolidado estiver fora do ar.
- Cada serviço pode ser escalado independentemente conforme o perfil de carga.
- Responsabilidades claramente separadas (SRP — um serviço, uma responsabilidade de negócio).
- Deployments independentes, sem risco de regressão cruzada entre domínios.

### Negativas / Trade-offs

- Maior complexidade operacional: dois bancos, dois processos, um broker.
- Consistência eventual: o consolidado pode estar alguns segundos atrasado em relação aos lançamentos (ver ADR-003).
- Dois pipelines de CI/CD a manter.

## Alternativas Consideradas

| Alternativa | Motivo da Rejeição |
|---|---|
| Monolito único | Viola o NFR: queda do módulo de consolidação derruba os lançamentos |
| Comunicação síncrona (HTTP entre serviços) | Lançamentos dependem da disponibilidade do Consolidado — mesmo problema do monolito |
| Microsserviços completos (>2 serviços) | Over-engineering para o escopo; dois serviços atendem o requisito com menor complexidade operacional |

## Relação com Princípios SOLID

- **SRP**: cada serviço tem uma única responsabilidade de negócio.
- **ISP**: cada serviço expõe apenas os endpoints necessários para seu domínio.
- **DIP**: a comunicação entre serviços ocorre via contratos de mensagem (`TransactionCreatedMessage`), não via referência direta.
