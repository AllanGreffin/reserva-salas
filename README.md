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

```
backend/
├── ReservaSalas.slnx
├── src/
│   ├── ReservaSalas.Domain/   entidades e regras de negócio (RN01–RN04), sem dependências
│   └── ReservaSalas.Api/      controllers, serviços, EF Core, migrations, seed, autenticação, tratamento de erros
└── tests/
    └── ReservaSalas.Tests/    Unit/ e Integration/
```

- **Só o domínio é separado da API.** As regras de negócio ficam num projeto que não referencia EF Core nem ASP.NET Core, então é o compilador que garante que elas não dependem de infraestrutura. Isso também deixa os testes unitários das RN01–RN04 simples, sem mocks.
- **O restante fica na API.** O serviço de reservas orquestra transação, controle de concorrência, regras de domínio e histórico. Ele precisa conhecer o SQL Server, porque a exclusão mútua depende de comportamento específico do banco.
- **Sem repositórios genéricos, MediatR ou camadas Application/Infrastructure.** O `DbContext` já faz o papel de Unit of Work e repositório; uma abstração por cima esconderia justamente o comportamento de banco do qual a exclusão mútua depende. Com 3 entidades e 5 operações, essas camadas seriam desproporcionais. Como o domínio já está isolado, extrair uma camada Application mais tarde, se surgirem novos pontos de entrada (workers, filas) ou comportamentos transversais, é um refactor mecânico.
- **Controllers, e não Minimal APIs.** As duas atenderiam. Controllers foram escolhidos pela convenção familiar a times .NET e por darem, com `[ApiController]`, a resposta 400 com Problem Details automática para modelos inválidos. Com poucos endpoints, a vantagem de concisão das Minimal APIs seria pequena.
- **Um único projeto de testes**, com as camadas `Unit/` e `Integration/` separadas por pasta e categoria, conforme pedido no enunciado.

### Exclusão mútua

_A definir._

### Datas e fuso horário

_A definir._

### XSS e armazenamento do token

_A definir._

### O que ficou de fora, melhorias e limitações conhecidas

_A definir._
