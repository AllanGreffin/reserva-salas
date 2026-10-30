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

O `dotnet-ef` é uma ferramenta local (`backend/dotnet-tools.json`), com a versão fixada no repositório. A migration `Seed` cria as salas e os usuários.

### Chave de assinatura do JWT

A chave **não fica em nenhum arquivo versionado**. A API não sobe se ela estiver ausente ou tiver menos de 32 caracteres.

Em desenvolvimento, use user-secrets (gravados fora do repositório, no perfil do usuário):

```bash
cd backend
dotnet user-secrets set "Jwt:ChaveAssinatura" "<uma chave aleatória com pelo menos 32 caracteres>" --project src/ReservaSalas.Api
```

Em containers ou em produção, use a variável de ambiente `Jwt__ChaveAssinatura`.

### API

```bash
cd backend
dotnet run --project src/ReservaSalas.Api --launch-profile http   # http://localhost:5221
```

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/auth/login` | Login (anônimo). Devolve o token, a expiração e o usuário. |
| `GET` | `/api/auth/eu` | Usuário do token |
| `GET` | `/api/salas` | Salas |
| `GET` | `/api/reservas` | Reservas, com `podeEditar` e `podeCancelar` para o usuário autenticado |
| `GET` | `/api/reservas/{id}` | Uma reserva |
| `POST` | `/api/reservas` | Cria (201 + `Location`) |
| `PUT` | `/api/reservas/{id}` | Edita título, sala e horário |
| `POST` | `/api/reservas/{id}/cancelamento` | Cancela |

Datas devem ser enviadas em ISO 8601 **com fuso explícito** (`2030-01-15T13:00:00Z` ou `2030-01-15T10:00:00-03:00`).

## Como executar os testes

```bash
cd backend
dotnet test                                         # todos (precisa do Docker rodando)
dotnet test --filter-trait "Category=Unit"          # só unitários
dotnet test --filter-trait "Category=Integration"   # só integração
```

- **Testes de integração** rodam contra um SQL Server 2022 real, num container criado pelo Testcontainers. Basta o Docker estar rodando. A primeira execução baixa a imagem (~1,5 GB).
- **Sem Docker**, aponte para uma instância existente pela variável de ambiente `RESERVASALAS_TESTES_SQL` (uma connection string). Os testes usam o banco `ReservaSalasTestes` nessa instância e **o recriam a cada execução**, então não aponte para um banco com dados que importam.

```bash
export RESERVASALAS_TESTES_SQL="Server=localhost;Trusted_Connection=True;TrustServerCertificate=True"
```

O projeto usa xUnit v3 com a Microsoft.Testing.Platform, habilitada em `backend/global.json`. No SDK do .NET 10, o runner antigo (VSTest) não é mais suportado para o xUnit v3.

## Usuários do seed

| Login | Senha | Perfil |
|---|---|---|
| `ana` | `Ana@2026` | Comum |
| `bruno` | `Bruno@2026` | Comum |
| `admin` | `Admin@2026` | Administrador |

Salas: Sala Azul (4 lugares), Sala Verde (8) e Auditório (30).

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

### Autenticação e autorização

- **Senhas:** `PasswordHasher<T>` do ASP.NET Core Identity (PBKDF2-HMAC-SHA512, 100 mil iterações, salt aleatório, formato versionado), **sem** adotar o Identity completo. As tabelas, o `UserManager` e os fluxos de cadastro do Identity seriam desproporcionais para três usuários fixos. BCrypt e Argon2 exigiriam bibliotecas de terceiros.
- **Seed via `HasData` numa migration:** o seed é versionado com o schema e aplicado pelo mesmo `database update`, inclusive nos testes de integração. Os hashes foram gerados uma única vez e estão como literais. Um hash com salt não revela a senha, e as senhas de demonstração estão documentadas acima, como pede o enunciado.
- **JWT HS256 com expiração de 1 hora, sem refresh token.** HS256 basta porque a própria API emite e valida os tokens. Validação de emissor, audiência, tempo de vida, assinatura e algoritmo. O `ClockSkew` foi reduzido para 30 segundos: o padrão do ASP.NET aceita tokens até 5 minutos depois de expirados.
- **Claims:** `sub` (id), `name` e `role` (perfil). O perfil vem do token, e não de uma releitura do banco: a API fica stateless, ao custo de uma mudança de perfil só valer após a expiração do token.
- **Login:** `POST /api/auth/login`. Credenciais inválidas retornam 401 com uma mensagem genérica. Quando o login não existe, o código ainda verifica um hash falso, para que o tempo de resposta não revele quais logins existem (medido: ~80 ms para login existente e ~74 ms para inexistente; sem isso, o inexistente responderia em 1 ou 2 ms).
- **Rate limiting no login:** 5 tentativas por minuto por IP (429 depois disso), configurável em `Login:TentativasPorMinuto`. Limitações conhecidas: o contador fica em memória, por instância, e atrás de um proxy seria preciso configurar `ForwardedHeaders` para obter o IP real. É uma proteção de melhor esforço contra força bruta, diferente da exclusão mútua, que precisa ser exata e por isso fica no banco.
- **Seguro por padrão:** uma *fallback policy* exige autenticação em todos os endpoints. Só o login (e o OpenAPI em desenvolvimento) é anônimo.
- **Propriedade (403):** `Reserva.PodeSerAlteradaPor` (dono ou administrador) fica no Domain, com teste unitário. O `ReservaService` a verifica dentro da transação, depois de carregar a reserva e antes de alterá-la. Reserva inexistente responde 404 antes do 403: todas as reservas são visíveis a todos, então a existência não é segredo. O *resource-based authorization* do ASP.NET (`IAuthorizationService`) foi descartado por espalhar a regra entre controller e handler.
- **O responsável é sempre o usuário do token:** os DTOs de entrada não têm campo de usuário, então o cliente não consegue forjar o dono da reserva.

### Endpoints e tratamento de erros

- **Controllers finos:** só traduzem HTTP. As regras ficam no domínio, a escrita no `ReservaService` (transação + lock) e a leitura no `ReservaConsultas`. Essa separação mantém o serviço de escrita focado no protocolo de concorrência.
- **Cancelamento é `POST /api/reservas/{id}/cancelamento`, não `DELETE`:** a RN04 diz que cancelar não exclui, e um `DELETE` que não apaga enganaria quem consome a API. A edição é `PUT` porque o cliente envia todos os campos editáveis.
- **`podeEditar` e `podeCancelar` na resposta:** são calculados pelos mesmos métodos do domínio que protegem as operações (`PodeSerEditadaPor`, `PodeSerCanceladaPor`), então o frontend não reimplementa regra nenhuma para decidir os botões. Por isso a listagem carrega as entidades, em vez de projetar em SQL. Os flags são um retrato do momento da consulta: se o estado mudar antes da ação (a reserva terminou ou foi cancelada por outro), o backend revalida e responde com o erro adequado. Não são controle de acesso.
- **Tratamento global com `IExceptionHandler` e Problem Details (RFC 9457) em todas as respostas de erro** (diferencial D2):

| Situação | Status |
|---|---|
| Corpo inválido, campo ausente, data sem fuso | 400 (`ValidationProblemDetails`, automático pelo `[ApiController]`) |
| Sem token, token inválido ou expirado | 401 |
| Reserva de outro usuário (sem ser administrador) | 403 |
| Reserva inexistente | 404 |
| Conflito de horário (RN03) | 409 |
| Regra de negócio (RN01, RN02, RN04, reserva encerrada, sala inexistente) | 422, com o campo `codigo` |
| Tentativas de login demais | 429 |
| Erro inesperado | 500, com mensagem genérica |

- **422 para regra de negócio, 400 para requisição malformada:** a requisição de um 422 é bem formada, mas a operação não é permitida. O 409 fica reservado para o conflito de horário, para que o frontend dê um feedback específico ("horário ocupado").
- **Só erros inesperados são logados como erro**, via `ILogger`, com método e caminho. Exceções de regra são comportamento esperado; registrá-las como erro geraria ruído e alarmes falsos.
- **Nenhum detalhe interno fora do ambiente de desenvolvimento:** o 500 traz só uma mensagem genérica e o `traceId`, que permite achar o log correspondente. Em Development, a resposta também inclui a exceção.
- **Respostas de erro sem corpo** (o 401 do JwtBearer, o 429 do rate limiter, o 404 de rota) também viram Problem Details, via `UseStatusCodePages()`.

### Testes

- **Unitários** (`Unit/`): RN01 a RN04, a tabela da seção 3.3 transcrita linha a linha, as permissões e as decisões adicionais. Testam o domínio puro, com o "agora" fixo passado como parâmetro.
- **Integração** (`Integration/`): a API real em memória (`WebApplicationFactory`), com login de verdade pelos usuários do seed, contra um SQL Server real. Um único container para toda a execução (assembly fixture do xUnit v3). Cada teste usa um dia futuro exclusivo, então os testes rodam em paralelo sem limpar o banco.
  - **T1:** 20 requisições simultâneas e sobrepostas, divididas entre **duas instâncias independentes da API** que compartilham o banco, como duas réplicas atrás de um balanceador. Exatamente uma recebe 201, as outras 19 recebem 409, e o banco tem uma única reserva ativa no intervalo. Um lock em memória passaria com uma instância só e falharia aqui.
  - **T2:** a falha é forçada **dentro do SQL Server**, por uma CHECK constraint temporária que rejeita o histórico de títulos com um marcador. Quando ela dispara, a reserva já foi inserida na transação, então a ausência da reserva prova o rollback. Cobre a criação e a edição. Um trigger foi descartado: o SQL Server proíbe triggers em tabelas gravadas com `OUTPUT` sem `INTO`, que é como o EF Core lê os Ids gerados, e o trigger quebrava os inserts dos outros testes. Um interceptor do EF, por sua vez, poderia abortar o lote antes de qualquer insert chegar ao banco, e o teste passaria sem provar nada.
  - **T3:** um usuário Comum recebe 403 ao editar e ao cancelar a reserva de outro, e o estado no banco (incluindo a quantidade de registros de histórico) é idêntico antes e depois. Um teste de controle confirma que o administrador consegue cancelar.
- **Os testes pegam as falhas que deveriam pegar.** O `ReservaService` foi sabotado de propósito, e cada sabotagem foi desfeita depois:

| Sabotagem | Testes que falharam |
|---|---|
| Remover o lock da sala | T1 |
| Remover a verificação de propriedade | T3 |
| Gravar reserva e histórico em dois `SaveChanges`, sem transação explícita | T2 (criação) **e T1** |
| Dois `SaveChanges`, com a transação explícita | nenhum |

  A terceira linha mostra que a transação explícita tem **duas** funções: atomicidade (T2) e **manter o lock** até o commit. Sem uma transação, o `UPDLOCK` é liberado ao fim do próprio `SELECT`, e a exclusão mútua deixa de existir (T1).

### Modelo de dados e índices

- **A disponibilidade não é um campo da sala.** Ela é calculada a partir das reservas ativas, como pede o enunciado.
- **Índice da verificação de conflito (RN03):** `IX_Reservas_SalaId_Fim_Ativas` em `(SalaId, Fim) INCLUDE (Inicio) WHERE Status = 'Ativa'`. Um B-tree só faz seek por uma desigualdade. Fazer o seek por `Fim > @inicio` descarta todas as reservas que já terminaram, enquanto por `Inicio < @fim` o custo cresceria com todo o histórico da sala. `Inicio` é incluído para evitar key lookup, e as reservas canceladas ficam fora do índice. Com esse índice, o EF Core não cria um índice separado para a FK `SalaId`, porque ela já é o prefixo do composto. Como salas nunca são excluídas, isso não afeta a validação da FK.
- **`Usuarios.Login` único:** atende a consulta do login e garante que não haja logins duplicados.
- **Enums gravados como texto** (`Status`, `Perfil`, `Acao`): ficam legíveis em consultas diretas, e reordenar o enum no C# não corrompe dados. O custo de espaço é irrelevante neste volume.
- **Chaves `int IDENTITY`:** são sequenciais e adequadas para o índice clusterizado. GUIDs não trariam ganho, porque todas as reservas já são visíveis a todos os usuários.
- **Tamanhos explícitos** nas colunas de texto, em vez do `nvarchar(max)` padrão, exceto em `Dados` do histórico (JSON de tamanho variável).
- **Nenhuma exclusão em cascata (`Restrict`):** reservas nunca são excluídas (o cancelamento é lógico, RN04), e cascatas a partir de `Usuarios` gerariam múltiplos caminhos até `ReservasHistorico`, o que o SQL Server rejeita.

### Exclusão mútua

**O problema:** verificar conflito e depois inserir (*check-then-act*) não é seguro sob concorrência. Duas requisições simultâneas veem "sem conflito" e as duas inserem. Uma transação comum não impede isso, ainda mais com RCSI (*Read Committed Snapshot Isolation*), em que um `SELECT` lê a última versão commitada sem esperar transações em andamento. RCSI vem ligado no banco criado pelas migrations e é o padrão no Azure SQL. Como a API pode rodar em várias instâncias, a serialização precisa acontecer no banco.

**A solução: lock pessimista na linha da sala.** Toda criação e edição segue o mesmo protocolo, numa transação explícita, dentro do `ReservaService` (o único ponto de escrita de reservas):

```sql
SELECT Id FROM Salas WITH (UPDLOCK, ROWLOCK) WHERE Id = @salaId  -- fila por sala
-- verificação de conflito (Reserva.ConflitaCom)
-- INSERT/UPDATE da reserva + INSERT do histórico
COMMIT                                                            -- libera a sala
```

- O `UPDLOCK` é incompatível com outro `UPDLOCK`: requisições para a mesma sala esperam na primeira instrução, em qualquer instância da API, e salas diferentes não se bloqueiam. Quando a segunda requisição obtém o lock, a primeira já fez commit, então a verificação de conflito enxerga a reserva recém-gravada e devolve 409.
- Hints de lock ignoram o versionamento do RCSI, então a solução é correta com ou sem RCSI.
- O que fica bloqueado é uma linha identificada pela PK, independentemente de índices ou do plano de execução.
- Na edição, o lock é na sala de **destino**, porque mover uma reserva para fora de uma sala só libera horário. O cancelamento não pega lock, pelo mesmo motivo.

**Alternativas consideradas:**

| Alternativa | Por que não |
|---|---|
| `SERIALIZABLE` / `UPDLOCK, HOLDLOCK` na consulta de conflito | Os *key-range locks* dependem do índice e do plano (um scan bloqueia a tabela inteira), e o `SERIALIZABLE` puro leva a deadlock entre duas inserções concorrentes. A correção fica difícil de provar. |
| `sp_getapplock` por sala | Equivalente em granularidade e uma boa segunda opção, mas o lock é um nome sem relação com o dado. Bloquear a própria sala deixa a intenção mais explícita. |
| Exclusion constraint | Seria o ideal (a regra garantida pelo próprio banco), mas existe no PostgreSQL e não no SQL Server. |
| Tabela de slots com índice único | Impõe uma granularidade de tempo que o enunciado não pede e multiplica as linhas. |
| Concorrência otimista na sala | Transforma contenção em falha: reservas sem conflito na mesma sala falhariam sem motivo real. |
| Trigger de validação | Em READ COMMITTED, o trigger de uma transação não enxerga a linha não commitada da outra. |
| Lock em memória / distribuído (Redis) | Em memória não funciona com várias instâncias. No Redis, o lock vive fora da transação do banco e pode expirar no meio dela. |

**Custo conhecido:** escritas na mesma sala são serializadas mesmo quando os horários não se sobrepõem. Para salas de reunião, o volume de escritas por sala é baixo demais para isso importar.

**Verificação:** com 20 requisições simultâneas e sobrepostas na mesma sala, o `ReservaService` criou 1 reserva e rejeitou 19 por conflito. Uma versão sem o lock, com a mesma transação, criou as 20. O SQL gerado para `ConflitaCom` contém o literal `[Status] = N'Ativa'`, condição para o SQL Server usar o índice filtrado. O teste de integração T1 cobre esse cenário de forma automatizada, com duas instâncias da API.

### Histórico (RN05)

Cada criação, edição e cancelamento grava um `ReservaHistorico` (ação, usuário, data/hora em UTC) **na mesma transação** da operação. `Dados` guarda um JSON com o estado **antes** e **depois** da operação (`antes` é nulo na criação). Assim, os campos alterados podem ser derivados sem um formato de diff específico.

### Datas e fuso horário

- **`DateTimeOffset` normalizado para UTC, em colunas `datetimeoffset`.** O offset faz parte do valor em memória, no banco e no JSON, então ser inequívoco é uma garantia do tipo, não de configuração. Toda data recebida é convertida para UTC (`ToUniversalTime()`) antes de ser gravada.
- **Por que não `DateTime` em UTC:** o SQL Server não guarda o `Kind`. O EF Core devolve `DateTimeKind.Unspecified`, e o JSON sai sem o sufixo `Z`, o que faz o navegador interpretar o valor como hora local. Isso só se resolve com um `ValueConverter` que alguém pode esquecer. Hora local sem fuso foi descartada por ser exatamente o caso que a RN06 proíbe.
- **Comparações corretas:** em `datetimeoffset`, o SQL Server compara pelo instante UTC, então a verificação de conflito da RN03 continua correta mesmo com offsets diferentes.
- **Tráfego:** ISO 8601 com offset. O frontend envia em UTC (`toISOString()`) e converte para o fuso do navegador só na exibição.
- **Datas sem fuso são rejeitadas na entrada (400).** Por padrão, o System.Text.Json aceita `"2030-01-15T10:00:00"` e assume o fuso do servidor, então a mesma requisição significaria instantes diferentes conforme a máquina. Um `JsonConverter<DateTimeOffset>` exige `Z` ou `±hh:mm`.
- **"Agora" injetável:** a RN02 depende do horário atual, então ele vem de `TimeProvider` (substituído por `FakeTimeProvider` nos testes). O domínio recebe o instante como parâmetro e continua sem dependências.

### XSS e armazenamento do token

_A definir._

### Dependências adicionais

- **`System.Security.Cryptography.Pkcs` referenciado diretamente:** o `Microsoft.Data.SqlClient` 6.1.7, trazido pelo EF Core, depende da versão 9.0.11, que tem vulnerabilidades conhecidas (GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7). A referência direta força uma versão corrigida e pode ser removida quando o SqlClient atualizar.

### O que ficou de fora, melhorias e limitações conhecidas

_Em construção._

- **Sem retry automático de falhas transitórias do banco (`EnableRetryOnFailure`).** A estratégia de retry do EF Core é incompatível com transações abertas manualmente, como as do `ReservaService`, a menos que cada operação seja envolvida em `Database.CreateExecutionStrategy().ExecuteAsync(...)`. Para um SQL Server local ou em container, falhas transitórias são raras. Num banco em nuvem (Azure SQL), esse seria o próximo passo.
