# Reserva de Salas de Reunião

Aplicação web de reserva de salas de reunião — ASP.NET Core, EF Core, SQL Server e Angular.

> Rascunho em construção: as seções abaixo são preenchidas conforme cada decisão é tomada.

## Pré-requisitos

| Ferramenta | Versão usada |
|---|---|
| .NET SDK | 10 (LTS) |
| Node.js | 24 |
| Docker | Necessário para os testes de integração (Testcontainers) e para o docker-compose |

## Como executar

_A definir._

## Como executar os testes

_A definir._

## Usuários do seed

_A definir._

## Tempo dedicado

_A definir._

---

## Decisões e trade-offs

### Ambiente e versões

- **.NET 10 (LTS):** o .NET 8 (LTS) e o .NET 9 (STS) encerram o suporte em 10/11/2026. O .NET 10 tem suporte até novembro de 2028, então é a única opção que continua atendendo ao requisito de "versão com suporte ativo" depois da entrega.
- **Angular na versão estável atual**, criado via `npx @angular/cli@latest`, sem depender de CLI global.
- **SQL Server 2022 em container:** garante que o ambiente do avaliador seja igual ao de desenvolvimento. Uma instância local continua possível, bastando trocar a connection string.
- **xUnit:** padrão de fato no ecossistema .NET, com boa integração com `WebApplicationFactory` e com o ciclo de vida de containers do Testcontainers (`IAsyncLifetime`). NUnit ou MSTest atenderiam igualmente; a escolha é de ecossistema, não técnica.
- **Monorepo** (`backend/`, `frontend/`, `docker-compose.yml` na raiz): uma única entrega, clonada uma vez e executada com um único compose.

### Como o código está organizado e por quê

_A definir._

### Exclusão mútua

_A definir._

### Datas e fuso horário

_A definir._

### XSS e armazenamento do token

_A definir._

### O que ficou de fora, melhorias e limitações conhecidas

_A definir._
