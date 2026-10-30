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

_Em construção._

### Banco de dados

A connection string fica em `ConnectionStrings:ReservaSalas`. Em desenvolvimento, o `appsettings.Development.json` aponta para uma instância local com autenticação do Windows, sem senha no arquivo. Para usar outra instância, sobrescreva com a variável de ambiente `ConnectionStrings__ReservaSalas`.

```bash
cd backend
dotnet tool restore
dotnet ef database update -p src/ReservaSalas.Api
```

O `dotnet-ef` é uma ferramenta local (`backend/dotnet-tools.json`), com a versão fixada no repositório.

## Como executar os testes

```bash
cd backend
dotnet test                                  # todos
dotnet test --filter-trait "Category=Unit"   # só unitários
```

O projeto usa xUnit v3 com a Microsoft.Testing.Platform, habilitada em `backend/global.json`. No SDK do .NET 10, o runner antigo (VSTest) não é mais suportado para o xUnit v3.

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

### Regras de negócio: interpretações e decisões

As regras ficam na entidade `Reserva` (projeto Domain), que recebe o "agora" como parâmetro.

- **Violações de regra lançam `RegraNegocioException`** (com um código como `RN01`), traduzida para **422** pelo tratamento global de erros. O 400 fica para requisições malformadas, e o conflito de horário (RN03) responde **409**, como pede o enunciado. Um Result pattern foi descartado: com três operações, propagar resultados por todas as camadas custaria mais do que o ganho de explicitude.
- **RN02 na edição só vale se o início for movido.** Uma reserva em andamento pode ter o título alterado ou o fim estendido. "Já passou" significa `inicio < agora`, então começar exatamente agora é permitido.
- **Regra adicional: reservas que já terminaram não podem ser editadas.** O enunciado não trata do caso. Editar o passado não tem uso real e adulteraria o registro do que aconteceu.
- **Cancelar uma reserva já cancelada é rejeitado.** A alternativa idempotente obrigaria a gerar um histórico duplicado ou a abrir uma exceção silenciosa à RN05. Cancelar uma reserva passada é permitido.
- **RN03 tem uma única definição:** `Reserva.ConflitaCom` devolve uma `Expression<Func<Reserva, bool>>`. O EF Core traduz essa expression para SQL na verificação de conflito, e os testes unitários a compilam e executam em memória. Assim, a regra testada e a regra aplicada no banco não podem divergir.
- **O domínio valida o título** (obrigatório, até 200 caracteres) mesmo com a validação da API. É defesa em profundidade: o domínio não depende de quem o chama.
- **Testes:** xUnit v3 (a v2 está em modo de manutenção) e asserções do próprio xUnit. FluentAssertions foi evitado porque a licença passou a ser comercial a partir da v8.

### Modelo de dados e índices

- **A disponibilidade não é um campo da sala.** Ela é calculada a partir das reservas ativas, como pede o enunciado.
- **Índice da verificação de conflito (RN03):** `IX_Reservas_SalaId_Fim_Ativas` em `(SalaId, Fim) INCLUDE (Inicio) WHERE Status = 'Ativa'`. Um B-tree só faz seek por uma desigualdade. Fazer o seek por `Fim > @inicio` descarta todas as reservas que já terminaram, enquanto por `Inicio < @fim` o custo cresceria com todo o histórico da sala. `Inicio` é incluído para evitar key lookup, e as reservas canceladas ficam fora do índice. Com esse índice, o EF Core não cria um índice separado para a FK `SalaId`, porque ela já é o prefixo do composto. Como salas nunca são excluídas, isso não afeta a validação da FK.
- **`Usuarios.Login` único:** atende a consulta do login e garante que não haja logins duplicados.
- **Enums gravados como texto** (`Status`, `Perfil`, `Acao`): ficam legíveis em consultas diretas, e reordenar o enum no C# não corrompe dados. O custo de espaço é irrelevante neste volume.
- **Chaves `int IDENTITY`:** são sequenciais e adequadas para o índice clusterizado. GUIDs não trariam ganho, porque todas as reservas já são visíveis a todos os usuários.
- **Tamanhos explícitos** nas colunas de texto, em vez do `nvarchar(max)` padrão, exceto em `Dados` do histórico (JSON de tamanho variável).
- **Nenhuma exclusão em cascata (`Restrict`):** reservas nunca são excluídas (o cancelamento é lógico, RN04), e cascatas a partir de `Usuarios` gerariam múltiplos caminhos até `ReservasHistorico`, o que o SQL Server rejeita.

### Exclusão mútua

_A definir._

### Datas e fuso horário

- **`DateTimeOffset` normalizado para UTC, em colunas `datetimeoffset`.** O offset faz parte do valor em memória, no banco e no JSON, então ser inequívoco é uma garantia do tipo, não de configuração. Toda data recebida é convertida para UTC (`ToUniversalTime()`) antes de ser gravada.
- **Por que não `DateTime` em UTC:** o SQL Server não guarda o `Kind`. O EF Core devolve `DateTimeKind.Unspecified`, e o JSON sai sem o sufixo `Z`, o que faz o navegador interpretar o valor como hora local. Isso só se resolve com um `ValueConverter` que alguém pode esquecer. Hora local sem fuso foi descartada por ser exatamente o caso que a RN06 proíbe.
- **Comparações corretas:** em `datetimeoffset`, o SQL Server compara pelo instante UTC, então a verificação de conflito da RN03 continua correta mesmo com offsets diferentes.
- **Tráfego:** ISO 8601 com offset. O frontend envia em UTC (`toISOString()`) e converte para o fuso do navegador só na exibição.
- **"Agora" injetável:** a RN02 depende do horário atual, então ele vem de `TimeProvider` (substituído por `FakeTimeProvider` nos testes). O domínio recebe o instante como parâmetro e continua sem dependências.

### XSS e armazenamento do token

_A definir._

### Dependências adicionais

- **`System.Security.Cryptography.Pkcs` referenciado diretamente:** o `Microsoft.Data.SqlClient` 6.1.7, trazido pelo EF Core, depende da versão 9.0.11, que tem vulnerabilidades conhecidas (GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7). A referência direta força uma versão corrigida e pode ser removida quando o SqlClient atualizar.

### O que ficou de fora, melhorias e limitações conhecidas

_A definir._
