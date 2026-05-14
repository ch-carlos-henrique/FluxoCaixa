# ADR-005 — Autenticação JWT Bearer HS256 com Refresh Token

## Status
Aceito — 13/05/2026

## Contexto

O sistema requer autenticação para proteger os endpoints da API. As opções variam de JWT manual a frameworks completos de identidade. Para um microserviço que apenas precisa emitir e validar tokens, frameworks completos adicionam complexidade sem valor proporcional.

Requisitos:
- Controle de acesso por roles (`Admin`, `Merchant`)
- Resource-level authorization: comerciante só acessa seus próprios dados
- Senhas armazenadas de forma segura contra ataques de força bruta e rainbow table
- Proteção contra os principais vetores do OWASP Top 10

## Decisão

### Autenticação

- **JWT Bearer HS256** com access token de expiração curta + refresh token armazenado na tabela `users`.
- **Tabela `users` manual** com campos: `id`, `email`, `password_hash`, `role`, `merchant_id`, `refresh_token`, `refresh_token_expires_at`.
- **ASP.NET Core Identity descartado**: traz 5 tabelas extras (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserTokens`) e 3 managers (`UserManager`, `RoleManager`, `SignInManager`) para um microserviço que só precisa emitir tokens — over-engineering documentado.

### Hash de Senha

- **BCrypt.Net-Next** com work factor 12 (~300 hashes/s — intencional por design de segurança).
- Salt embutido no próprio hash — simplifica o armazenamento (não requer coluna separada para o salt).
- **SHA-512 descartado**: projetado para velocidade (~10 bilhões de hashes/s em GPU) — inseguro para senhas. BCrypt é projetado especificamente para ser computacionalmente custoso, seguindo recomendação da OWASP Password Storage Cheat Sheet.

### Autorização

- Roles via claims JWT: `Admin` acessa todos os recursos; `Merchant` acessa apenas seus próprios recursos.
- Resource check no handler: claim `merchantId` (do JWT) vs. `request.MerchantId` → `403 Forbidden` se divergir.

### Proteções OWASP Top 10

| Risco OWASP | Mitigação Implementada |
|---|---|
| A01 — Broken Access Control | Resource check (merchantId claim vs. request) + role-based authorization |
| A02 — Cryptographic Failures | HS256 + HTTPS/TLS em produção; BCrypt work factor 12 para senhas |
| A03 — Injection | FluentValidation (input validation); EF Core com parâmetros (SQL injection) |
| A04 — Insecure Design | JWT expiração curta + refresh token rotation |
| A07 — Identification and Authentication Failures | BCrypt work factor 12 (~300/s); rate limiting (300 req/min por usuário) |
| A10 — SSRF | Sem chamadas HTTP externas iniciadas pelos serviços |

## Consequências

### Positivas

- Implementação simples: 1 pacote BCrypt + JWT Bearer nativo no ASP.NET Core.
- Controle total sobre o schema de usuários — sem migrações automáticas do Identity.
- Auditável: estrutura de tabela clara e sem campos ocultos de framework.
- Sem dependência de serviço externo para autenticação.

### Negativas / Trade-offs

- Chave HS256 compartilhada entre instâncias — escala horizontal requer distribuição segura do secret (variável de ambiente / secrets manager em produção).
- Sem suporte nativo a múltiplos provedores de login (Google, Azure AD) — evolução documentada abaixo.
- Refresh token no banco requer limpeza periódica de registros expirados.
- Sem rotação automática de chaves — HS256 exige regeneração manual do secret em caso de comprometimento.

## Alternativas Consideradas

| Alternativa | Motivo da Rejeição |
|---|---|
| ASP.NET Core Identity | 5 tabelas extras + 3 managers — over-engineering para emissão de tokens simples em microserviço |
| OAuth2/OIDC externo (Entra ID, Keycloak) | Dependência de serviço externo; adequado para produção empresarial (documentado como evolução futura) |
| Argon2id (Konscious.Security.Cryptography) | Primeira opção OWASP 2024; maior resistência a GPU/ASIC; salt deve ser armazenado separadamente — complexidade adicional não justificada no escopo atual |
| RS256 (chave assimétrica) | Necessário apenas com múltiplos serviços validando tokens sem compartilhar secret; HS256 é suficiente para 2 serviços internos |
| Sem refresh token | Exigiria access tokens de longa duração — risco de segurança maior em caso de vazamento |

## Evoluções Futuras

- **OIDC/Entra ID**: substituir JWT manual quando o sistema precisar de múltiplos provedores de login ou SSO corporativo.
- **Argon2id**: substituir BCrypt para maior resistência a ataques modernos em GPU/ASIC (Konscious.Security.Cryptography — requer salt armazenado separadamente).
- **RS256**: migrar para chave assimétrica se múltiplos serviços externos precisarem validar tokens sem acesso ao secret.

## Relação com Princípios SOLID

- **SRP**: `AuthService` tem responsabilidade única — gerar e validar tokens.
- **DIP**: `IAuthService` e `IUserRepository` são interfaces no Application layer; implementações concretas em Infrastructure — o domínio não conhece BCrypt ou JWT.
