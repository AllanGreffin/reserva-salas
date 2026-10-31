# Reserva de Salas de Reunião

Aplicação web de reserva de salas de reunião — ASP.NET Core, EF Core, SQL Server e Angular.

Todos os requisitos obrigatórios estão implementados, assim como os diferenciais D1 (concorrência otimista), D2 (Problem Details), D3 (filtro e paginação) e D4 (docker-compose).

**Respostas às perguntas da seção "Decisões e trade-offs":**
[exclusão mútua](#exclusão-mútua) ·
[organização do código](#como-o-código-está-organizado-e-por-quê) ·
[XSS e armazenamento do token](#xss-e-armazenamento-do-token) ·
[datas e fuso horário](#datas-e-fuso-horário) ·
[o que ficou de fora e limitações](#o-que-ficou-de-fora-melhorias-e-limitações-conhecidas)

## Pré-requisitos

| Ferramenta | Versão usada | Para quê |
|---|---|---|
| Docker | 29 (com Docker Compose) | Executar a aplicação com um comando; testes de integração |
| .NET SDK | 10 (LTS) | Desenvolvimento e testes do backend |
| Node.js | 24 | Desenvolvimento do frontend |

Para apenas **rodar** a aplicação, basta o Docker.

## Como executar

### Com Docker (um comando)

```bash
cp .env.example .env     # preencha SQL_SENHA_SA e JWT_CHAVE (instruções no arquivo)
docker compose up --build
```

A aplicação fica em **http://localhost:8080**, com os usuários do seed abaixo. A ordem de subida é automática: o SQL Server fica saudável, as migrations (schema + seed) são aplicadas e só então a API e o frontend sobem. A primeira execução baixa as imagens e compila tudo, o que leva alguns minutos.

```bash
docker compose down -v   # para tudo e apaga o volume do banco
```

### Sem Docker (desenvolvimento)

Requer o .NET 10, o Node 24 e uma instância do SQL Server.

#### Banco de dados

A connection string fica em `ConnectionStrings:ReservaSalas`. Em desenvolvimento, o `appsettings.Development.json` aponta para uma instância local com autenticação do Windows, sem senha no arquivo. Para usar outra instância, sobrescreva com a variável de ambiente `ConnectionStrings__ReservaSalas`.

```bash
cd backend
dotnet tool restore
dotnet ef database update -p src/ReservaSalas.Api
```

O `dotnet-ef` é uma ferramenta local (`backend/dotnet-tools.json`), com a versão fixada no repositório. A migration `Seed` cria as salas e os usuários.

#### Chave de assinatura do JWT

A chave **não fica em nenhum arquivo versionado**. A API não sobe se ela estiver ausente ou tiver menos de 32 caracteres.

Em desenvolvimento, use user-secrets (gravados fora do repositório, no perfil do usuário):

```bash
cd backend
dotnet user-secrets set "Jwt:ChaveAssinatura" "<uma chave aleatória com pelo menos 32 caracteres>" --project src/ReservaSalas.Api
```

Em containers ou em produção, use a variável de ambiente `Jwt__ChaveAssinatura`.

#### API

```bash
cd backend
dotnet run --project src/ReservaSalas.Api --launch-profile http   # http://localhost:5221
```

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/auth/login` | Login (anônimo). Devolve o token, a expiração e o usuário. |
| `GET` | `/api/auth/eu` | Usuário do token |
| `GET` | `/api/salas` | Salas |
| `GET` | `/api/reservas?salaId=&de=&ate=&pagina=&tamanhoPagina=` | Reservas paginadas (D3), com `podeEditar` e `podeCancelar` para o usuário autenticado. Todos os filtros são opcionais; `de`/`ate` em ISO 8601 com fuso |
| `GET` | `/api/reservas/{id}` | Uma reserva |
| `POST` | `/api/reservas` | Cria (201 + `Location`) |
| `PUT` | `/api/reservas/{id}` | Edita título, sala e horário; exige a `versao` recebida na leitura (D1) |
| `POST` | `/api/reservas/{id}/cancelamento` | Cancela |

Datas devem ser enviadas em ISO 8601 **com fuso explícito** (`2030-01-15T13:00:00Z` ou `2030-01-15T10:00:00-03:00`).

#### Frontend

Com a API rodando em `http://localhost:5221`:

```bash
cd frontend
npm install
npm start          # http://localhost:4200
npm test           # testes unitários (Vitest)
```

O `ng serve` repassa `/api` para a API (`frontend/proxy.conf.json`), então o navegador fala com uma única origem e a API não precisa liberar CORS.

## Como executar os testes

```bash
cd backend
dotnet test                                         # todos (precisa do Docker rodando)
dotnet test --filter-trait "Category=Unit"          # só unitários (não precisa de Docker)
dotnet test --filter-trait "Category=Integration"   # só integração
```

- **Testes de integração** rodam contra um SQL Server 2022 real, num container criado pelo Testcontainers. Basta o Docker estar rodando. A primeira execução baixa a imagem (~1,5 GB).
- **Sem Docker**, aponte para uma instância existente pela variável de ambiente `RESERVASALAS_TESTES_SQL` (uma connection string). Os testes usam o banco `ReservaSalasTestes` nessa instância e **o recriam a cada execução**, então não aponte para um banco com dados que importam.

```bash
export RESERVASALAS_TESTES_SQL="Server=localhost;Trusted_Connection=True;TrustServerCertificate=True"
```

Frontend: `cd frontend && npm test` (Vitest).

O backend usa xUnit v3 com a Microsoft.Testing.Platform, habilitada em `backend/global.json`. No SDK do .NET 10, o runner antigo (VSTest) não é mais suportado para o xUnit v3.

## Usuários do seed

| Login | Senha | Perfil |
|---|---|---|
| `ana` | `Ana@2026` | Comum |
| `bruno` | `Bruno@2026` | Comum |
| `admin` | `Admin@2026` | Administrador |

Salas: Sala Azul (4 lugares), Sala Verde (8) e Auditório (30).

## Tempo dedicado

Aproximadamente 10 horas.

---

## Decisões e trade-offs

As cinco primeiras subseções respondem às perguntas obrigatórias do enunciado; as demais detalham as outras decisões.

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

### Como o código está organizado e por quê

```
backend/
├── ReservaSalas.slnx
├── src/
│   ├── ReservaSalas.Domain/         regras de negócio (RN01–RN04), sem dependências
│   │   ├── Entidades/               Reserva, ReservaHistorico, Sala, Usuario
│   │   ├── Enums/                   StatusReserva, PerfilUsuario, AcaoReserva
│   │   └── Excecoes/                RegraNegocioException, ConflitoDeHorarioException, AcessoNegadoException
│   └── ReservaSalas.Api/            organizada por funcionalidade
│       ├── Autenticacao/            login, JWT, usuário atual
│       ├── Reservas/                controller, ReservaService (escrita), ReservaConsultas (leitura), DTOs, filtro
│       ├── Salas/                   controller e consulta de salas
│       ├── Erros/                   tratamento global de exceções
│       ├── Json/                    datas com fuso obrigatório (RN06)
│       └── Data/                    DbContext, configurações do EF, migrations e seed
└── tests/
    └── ReservaSalas.Tests/          Unit/ e Integration/
```

- **Só o domínio é separado da API.** As regras de negócio ficam num projeto que não referencia EF Core nem ASP.NET Core, então é o compilador que garante que elas não dependem de infraestrutura. Isso também deixa os testes unitários das RN01–RN04 simples, sem mocks.
- **O restante fica na API.** O serviço de reservas orquestra transação, controle de concorrência, regras de domínio e histórico. Ele precisa conhecer o SQL Server, porque a exclusão mútua depende de comportamento específico do banco.
- **Sem repositórios genéricos, MediatR ou camadas Application/Infrastructure.** O `DbContext` já faz o papel de Unit of Work e repositório; uma abstração por cima esconderia justamente o comportamento de banco do qual a exclusão mútua depende. Com 3 entidades e 5 operações, essas camadas seriam desproporcionais. Como o domínio já está isolado, extrair uma camada Application mais tarde, se surgirem novos pontos de entrada (workers, filas) ou comportamentos transversais, é um refactor mecânico.
- **Controllers, e não Minimal APIs.** As duas atenderiam. Controllers foram escolhidos pela convenção familiar a times .NET e por darem, com `[ApiController]`, a resposta 400 com Problem Details automática para modelos inválidos. Com poucos endpoints, a vantagem de concisão das Minimal APIs seria pequena.
- **Pastas e namespaces:** o domínio é separado por tipo (`Entidades`, `Enums`, `Excecoes`). Com quatro entidades, essa divisão deixa o domínio legível de relance. A Api é separada por funcionalidade, porque cada funcionalidade reúne vários arquivos próprios (controller, serviço, DTOs). Os namespaces acompanham as pastas, como manda a convenção do .NET.
- **Um único projeto de testes**, com as camadas `Unit/` e `Integration/` separadas por pasta e categoria, conforme pedido no enunciado.

### XSS e armazenamento do token

**Como a aplicação está protegida contra XSS:**

1. **O Angular escapa toda interpolação** (`{{ }}`) e sanitiza bindings de propriedade. Um título como `<img src=x onerror=alert(1)>` é exibido como texto. Isso foi verificado: no DOM, a célula contém `&lt;img ...&gt;`, não há nenhum elemento `<img>` na página e nenhum script executou.
2. **Nenhum `[innerHTML]`, `bypassSecurityTrust*` ou manipulação direta do DOM** no código: as proteções do Angular nunca são desligadas.
3. **A API só devolve JSON**, nunca HTML montado com dados do usuário.
4. **Content Security Policy** no nginx que entrega o frontend (`frontend/nginx.conf`): `default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'`, mais `X-Content-Type-Options`, `Referrer-Policy` e `X-Frame-Options`. É a segunda camada: mesmo que um script fosse injetado, ele não executaria nem conseguiria enviar dados a outro domínio. Isso foi verificado no navegador: um `<script>` inline injetado na página não executou, e uma imagem apontando para um domínio externo foi bloqueada. Com o app em uso normal, o console fica sem nenhuma violação. Para isso, o build de produção do Angular tem o `inlineCritical` desligado, porque essa otimização gera um `<link onload="...">`, que é um handler inline incompatível com uma CSP estrita.

**Onde o token fica e os riscos:** em **`sessionStorage`**.

| Opção | Sobrevive ao F5 | Legível por um script injetado | Observação |
|---|---|---|---|
| `localStorage` | sim | sim | Persiste indefinidamente e é compartilhado entre abas |
| **`sessionStorage`** (escolhida) | sim | sim | Apagado ao fechar a aba e isolado por aba |
| Só em memória | não (novo login a cada F5) | não diretamente | Um XSS ainda faz requisições enquanto a página está aberta |
| Cookie `HttpOnly` | sim | não | Exige autenticação por cookie no backend e proteção contra CSRF |

- **Risco assumido:** como qualquer storage acessível por JavaScript, o `sessionStorage` pode ser lido por um script injetado. A defesa principal é **impedir o XSS** (acima), e o estrago de um token roubado é limitado pela **expiração de 1 hora**.
- **Por que não cookie `HttpOnly`:** é o mais resistente a roubo de token, mas exigiria mudar a autenticação do backend para cookies, tratar CSRF e configurar `SameSite`. É desproporcional para o escopo e fica como melhoria.
- **Por que não só em memória:** obrigaria um novo login a cada F5, e um XSS ativo ainda conseguiria agir enquanto a página estivesse aberta.

### Datas e fuso horário

- **`DateTimeOffset` normalizado para UTC, em colunas `datetimeoffset`.** O offset faz parte do valor em memória, no banco e no JSON, então ser inequívoco é uma garantia do tipo, não de configuração. Toda data recebida é convertida para UTC (`ToUniversalTime()`) antes de ser gravada.
- **Por que não `DateTime` em UTC:** o SQL Server não guarda o `Kind`. O EF Core devolve `DateTimeKind.Unspecified`, e o JSON sai sem o sufixo `Z`, o que faz o navegador interpretar o valor como hora local. Isso só se resolve com um `ValueConverter` que alguém pode esquecer. Hora local sem fuso foi descartada por ser exatamente o caso que a RN06 proíbe.
- **Comparações corretas:** em `datetimeoffset`, o SQL Server compara pelo instante UTC, então a verificação de conflito da RN03 continua correta mesmo com offsets diferentes.
- **Tráfego:** ISO 8601 com offset. O frontend envia em UTC (`toISOString()`) e converte para o fuso do navegador só na exibição.
- **Datas sem fuso são rejeitadas na entrada (400).** Por padrão, o System.Text.Json aceita `"2030-01-15T10:00:00"` e assume o fuso do servidor, então a mesma requisição significaria instantes diferentes conforme a máquina. A regra fica em `DatasComFuso`, que exige `Z` ou `±hh:mm` e é aplicada ao corpo JSON (por um `JsonConverter<DateTimeOffset>`) e à query string da listagem.
- **"Agora" injetável:** a RN02 depende do horário atual, que a API obtém de `TimeProvider` (injetado por DI). O domínio recebe o instante como parâmetro e continua sem dependências, então os testes unitários passam um valor fixo, sem depender do relógio.

### O que ficou de fora, melhorias e limitações conhecidas

**O que ficou de fora:** nenhum requisito. Todos os obrigatórios e os diferenciais D1 a D4 foram implementados.

**Limitações conhecidas:**

- **Escritas na mesma sala são serializadas** pelo lock, mesmo em horários que não se sobrepõem. É aceitável para o volume de reservas de salas de reunião (ver "Exclusão mútua").
- **Token em `sessionStorage`, legível por um script injetado.** O risco é mitigado pelo escape do Angular, pela CSP e pela expiração de 1 hora (ver "XSS e armazenamento do token").
- **Mudança de perfil só vale após o token expirar**, porque o perfil vem do token, e não de uma releitura do banco.
- **Rate limit de login em memória, por instância:** com várias réplicas, o limite efetivo é multiplicado pelo número de réplicas.
- **Sem retry automático de falhas transitórias do banco (`EnableRetryOnFailure`).** A estratégia de retry do EF Core é incompatível com transações abertas manualmente, como as do `ReservaService`, a menos que cada operação seja envolvida em `Database.CreateExecutionStrategy().ExecuteAsync(...)`. Num SQL Server local ou em container, falhas transitórias são raras.
- **No docker-compose, a API conecta como `sa`** e tudo é servido em HTTP.
- **A paginação por offset fica mais lenta em páginas muito profundas** (ver "Filtro e paginação").
- **O histórico (RN05) é gravado, mas não é exibido** na interface: o enunciado não pede uma tela para ele.
- **O frontend não tem testes de ponta a ponta automatizados:** os fluxos foram verificados manualmente no navegador (os cenários estão listados em "Frontend").

**O que eu faria com mais tempo:**

- **CI no GitHub Actions** rodando build e testes (inclusive os de integração com Testcontainers) a cada PR.
- **Testes de ponta a ponta** (Playwright) cobrindo os fluxos hoje verificados manualmente.
- **Autenticação por cookie `HttpOnly` + `SameSite`**, com proteção contra CSRF, em vez de `sessionStorage`; e **refresh token** com rotação, para encurtar o token de acesso.
- **Rate limit distribuído** (Redis) ou no gateway/balanceador.
- **Em produção:** login de banco com permissões mínimas (só DML para a API; DDL só para as migrations), TLS no nginx ou no balanceador e segredos num cofre (Key Vault ou equivalente) em vez de `.env`.
- **Retry de falhas transitórias** com `ExecutionStrategy`, se o banco fosse em nuvem.
- **Observabilidade:** logs estruturados, OpenTelemetry e um endpoint de health check para o orquestrador.
- **Argon2id** para o hash de senhas, se usuários reais passassem a se cadastrar.

---

## Outras decisões

### Regras de negócio: interpretações e decisões

As regras ficam na entidade `Reserva` (projeto Domain), que recebe o "agora" como parâmetro.

- **Violações de regra lançam `RegraNegocioException`** (com um código como `RN01`), traduzida para **422** pelo tratamento global de erros. O 400 fica para requisições malformadas, e o conflito de horário (RN03) responde **409**, como pede o enunciado. Um Result pattern foi descartado: com três operações, propagar resultados por todas as camadas custaria mais do que o ganho de explicitude.
- **RN02 na edição só vale se o início for movido.** Uma reserva em andamento pode ter o título alterado ou o fim estendido. "Já passou" significa `inicio < agora`, então começar exatamente agora é permitido.
- **Regra adicional: reservas que já terminaram não podem ser editadas.** O enunciado não trata do caso. Editar o passado não tem uso real e adulteraria o registro do que aconteceu.
- **Cancelar uma reserva já cancelada é rejeitado.** A alternativa idempotente obrigaria a gerar um histórico duplicado ou a abrir uma exceção silenciosa à RN05. Cancelar uma reserva passada é permitido.
- **RN03 tem uma única definição:** `Reserva.ConflitaCom` devolve uma `Expression<Func<Reserva, bool>>`. O EF Core traduz essa expression para SQL na verificação de conflito, e os testes unitários a compilam e executam em memória. Assim, a regra testada e a regra aplicada no banco não podem divergir.
- **O domínio valida o título** (obrigatório, até 200 caracteres) mesmo com a validação da API. É defesa em profundidade: o domínio não depende de quem o chama.
- **Testes:** xUnit v3 (a v2 está em modo de manutenção) e asserções do próprio xUnit. FluentAssertions foi evitado porque a licença passou a ser comercial a partir da v8.

### Histórico (RN05)

Cada criação, edição e cancelamento grava um `ReservaHistorico` (ação, usuário, data/hora em UTC) **na mesma transação** da operação. `Dados` guarda um JSON com o estado **antes** e **depois** da operação (`antes` é nulo na criação). Assim, os campos alterados podem ser derivados sem um formato de diff específico.

### Autenticação e autorização

- **Senhas:** `PasswordHasher<T>` do ASP.NET Core Identity (PBKDF2-HMAC-SHA512, 100 mil iterações, salt aleatório, formato versionado), **sem** adotar o Identity completo. As tabelas, o `UserManager` e os fluxos de cadastro do Identity seriam desproporcionais para três usuários fixos. BCrypt e Argon2 exigiriam bibliotecas de terceiros.
- **Seed via `HasData` numa migration:** o seed é versionado com o schema e aplicado pelo mesmo `database update`, inclusive nos testes de integração. Os hashes foram gerados uma única vez e estão como literais. Um hash com salt não revela a senha, e as senhas de demonstração estão documentadas acima, como pede o enunciado.
- **JWT HS256 com expiração de 1 hora, sem refresh token.** HS256 basta porque a própria API emite e valida os tokens. Validação de emissor, audiência, tempo de vida, assinatura e algoritmo. O `ClockSkew` foi reduzido para 30 segundos: o padrão do ASP.NET aceita tokens até 5 minutos depois de expirados.
- **Claims:** `sub` (id), `name` e `role` (perfil). O perfil vem do token, e não de uma releitura do banco: a API fica stateless, ao custo de uma mudança de perfil só valer após a expiração do token.
- **Login:** `POST /api/auth/login`. Credenciais inválidas retornam 401 com uma mensagem genérica. Quando o login não existe, o código ainda verifica um hash falso, para que o tempo de resposta não revele quais logins existem (medido: ~80 ms para login existente e ~74 ms para inexistente; sem isso, o inexistente responderia em 1 ou 2 ms).
- **Rate limiting no login:** 5 tentativas por minuto por IP (429 depois disso), configurável em `Login:TentativasPorMinuto`. Atrás do nginx (docker-compose), a API lê o IP real do `X-Forwarded-For` (ver "Execução com Docker"). Limitação conhecida: o contador fica em memória, por instância. É uma proteção de melhor esforço contra força bruta, diferente da exclusão mútua, que precisa ser exata e por isso fica no banco.
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
| Conflito de horário (RN03) | 409, com `codigo: CONFLITO_DE_HORARIO` |
| Reserva alterada por outra pessoa desde a leitura (D1) | 409, com `codigo: VERSAO_DESATUALIZADA` |
| Regra de negócio (RN01, RN02, RN04, reserva encerrada, sala inexistente) | 422, com o campo `codigo` |
| Tentativas de login demais | 429 |
| Erro inesperado | 500, com mensagem genérica |

- **422 para regra de negócio, 400 para requisição malformada:** a requisição de um 422 é bem formada, mas a operação não é permitida. O 409 fica para conflitos com o estado atual: horário ocupado (RN03) ou versão desatualizada (D1), distinguidos pelo `codigo` para que o frontend dê o feedback certo.
- **Só erros inesperados são logados como erro**, via `ILogger`, com método e caminho. Exceções de regra são comportamento esperado; registrá-las como erro geraria ruído e alarmes falsos.
- **Nenhum detalhe interno fora do ambiente de desenvolvimento:** o 500 traz só uma mensagem genérica e o `traceId`, que permite achar o log correspondente. Em Development, a resposta também inclui a exceção.
- **Respostas de erro sem corpo** (o 401 do JwtBearer, o 429 do rate limiter, o 404 de rota) também viram Problem Details, via `UseStatusCodePages()`.

### Modelo de dados e índices

- **A disponibilidade não é um campo da sala.** Ela é calculada a partir das reservas ativas, como pede o enunciado.
- **Índice da verificação de conflito (RN03):** `IX_Reservas_SalaId_Fim_Ativas` em `(SalaId, Fim) INCLUDE (Inicio) WHERE Status = 'Ativa'`. Um B-tree só faz seek por uma desigualdade. Fazer o seek por `Fim > @inicio` descarta todas as reservas que já terminaram, enquanto por `Inicio < @fim` o custo cresceria com todo o histórico da sala. `Inicio` é incluído para evitar key lookup, e as reservas canceladas ficam fora do índice. Com esse índice, o EF Core não cria um índice separado para a FK `SalaId`, porque ela já é o prefixo do composto. Como salas nunca são excluídas, isso não afeta a validação da FK.
- **Índice da listagem (D3):** `IX_Reservas_Inicio_Id` em `(Inicio, Id)` atende o filtro de período (`Inicio < @ate`) e a ordenação (`ORDER BY Inicio, Id`) sem *sort*. A listagem inclui canceladas, então não pode usar o índice filtrado de conflito. `SalaId` ficou fora da chave: com poucas salas, o filtro por sala é pouco seletivo e é aplicado sobre o resultado deste índice. Com centenas de salas, um índice `(SalaId, Inicio)` passaria a compensar.
- **`Usuarios.Login` único:** atende a consulta do login e garante que não haja logins duplicados.
- **Enums gravados como texto** (`Status`, `Perfil`, `Acao`): ficam legíveis em consultas diretas, e reordenar o enum no C# não corrompe dados. O custo de espaço é irrelevante neste volume.
- **Chaves `int IDENTITY`:** são sequenciais e adequadas para o índice clusterizado. GUIDs não trariam ganho, porque todas as reservas já são visíveis a todos os usuários.
- **Tamanhos explícitos** nas colunas de texto, em vez do `nvarchar(max)` padrão, exceto em `Dados` do histórico (JSON de tamanho variável).
- **Nenhuma exclusão em cascata (`Restrict`):** reservas nunca são excluídas (o cancelamento é lógico, RN04), e cascatas a partir de `Usuarios` gerariam múltiplos caminhos até `ReservasHistorico`, o que o SQL Server rejeita.

### Testes

- **Unitários** (`Unit/`): RN01 a RN04, a tabela da seção 3.3 transcrita linha a linha, as permissões, as decisões adicionais e a validação de datas com fuso (`DatasComFuso`). Testam o domínio puro, com o "agora" fixo passado como parâmetro.
- **Integração** (`Integration/`): a API real em memória (`WebApplicationFactory`), com login de verdade pelos usuários do seed, contra um SQL Server real. Um único container, compartilhado pelas classes de integração por uma **fixture de coleção** do xUnit. O xUnit só a cria quando algum teste de integração executa, então **os testes unitários rodam sem Docker**. Uma *assembly fixture* subiria o container sempre, mesmo filtrando só os unitários (verificado pelos eventos do Docker: rodar só os unitários não cria nenhum container). Os testes de integração rodam em sequência entre si, o que custa poucos segundos, porque o tempo é dominado por subir o container. Cada teste usa um dia futuro exclusivo, então os testes não dependem do que os outros gravaram.
  - **T1:** 20 requisições simultâneas e sobrepostas, divididas entre **duas instâncias independentes da API** que compartilham o banco, como duas réplicas atrás de um balanceador. Exatamente uma recebe 201, as outras 19 recebem 409, e o banco tem uma única reserva ativa no intervalo. Um lock em memória passaria com uma instância só e falharia aqui.
  - **T2:** a falha é forçada **dentro do SQL Server**, por uma CHECK constraint temporária que rejeita o histórico de títulos com um marcador. Quando ela dispara, a reserva já foi inserida na transação, então a ausência da reserva prova o rollback. Cobre a criação e a edição. Um trigger foi descartado: o SQL Server proíbe triggers em tabelas gravadas com `OUTPUT` sem `INTO`, que é como o EF Core lê os Ids gerados, e o trigger quebrava os inserts dos outros testes. Um interceptor do EF, por sua vez, poderia abortar o lote antes de qualquer insert chegar ao banco, e o teste passaria sem provar nada.
  - **T3:** um usuário Comum recebe 403 ao editar e ao cancelar a reserva de outro, e o estado no banco (incluindo a quantidade de registros de histórico) é idêntico antes e depois. Um teste de controle confirma que o administrador consegue cancelar.
  - **D1** (`ConcorrenciaOtimistaTests`): a Ana e o admin leem a mesma versão; o admin salva e recebe 200; a Ana salva e recebe 409 `VERSAO_DESATUALIZADA`, sem sobrescrever. Também prova que os dois 409 são distinguíveis e que um `PUT` sem versão recebe 400.
  - **D3** (`ListagemTests`): filtro por sala; período por sobreposição, incluindo a borda semiaberta (uma reserva que termina exatamente no `de` não entra); paginação com totais, ordem e nenhuma reserva repetida ou perdida entre páginas; 400 para `tamanhoPagina` acima de 100, `pagina` 0, data sem fuso e período invertido. Cada teste cria as próprias reservas num dia exclusivo e filtra por ele.
- **Os testes pegam as falhas que deveriam pegar.** O código foi sabotado de propósito (no `ReservaService` e, na última linha, no `ReservaConsultas`), e cada sabotagem foi desfeita depois:

| Sabotagem | Testes que falharam |
|---|---|
| Remover o lock da sala | T1 |
| Remover a verificação de propriedade | T3 |
| Não usar a versão enviada pelo cliente na edição | D1 |
| Gravar reserva e histórico em dois `SaveChanges`, sem transação explícita | T2 (criação) **e T1** |
| Dois `SaveChanges`, com a transação explícita | nenhum |
| Filtro de período com `Fim >= de` em vez de `Fim > de` | D3 (borda semiaberta) |

  A quarta linha mostra que a transação explícita tem **duas** funções: atomicidade (T2) e **manter o lock** até o commit. Sem uma transação, o `UPDLOCK` é liberado ao fim do próprio `SELECT`, e a exclusão mútua deixa de existir (T1).

### Frontend

```
frontend/src/app/
├── core/auth/        Sessao (token e usuário), authInterceptor, authGuard
├── core/api/         ReservasApi: toda a comunicação HTTP fica aqui, fora dos componentes
├── core/erros/       mensagemDeErro: Problem Details → texto para o usuário
├── core/datas.ts     conversão entre datetime-local (hora local) e ISO UTC
├── features/login/   Login
├── features/reservas ReservasLista, ReservaForm (criação e edição), validador fimDepoisDoInicio
└── shared/           Cabecalho, Alerta
```

- **Sem biblioteca de componentes:** são quatro telas simples, e o `<input type="datetime-local">` nativo resolve data e hora. Um CSS próprio evita dependências pesadas.
- **Reactive Forms (tipados), e não template-driven:** a regra "fim posterior ao início" envolve dois campos, então vira um validador do `FormGroup`. É uma função pura e testável; no template-driven, exigiria uma diretiva customizada espalhada pelo HTML. Signal Forms foram evitados por ainda serem experimentais.
- **O mesmo componente cria e edita:** o modo vem da rota (`/reservas/nova` ou `/reservas/:id/editar`).
- **Autenticação centralizada:** o `authInterceptor` anexa o token **só a requisições para `/api`**, para não vazá-lo se o app chamar outro domínio. Um 401 da API (fora o login) encerra a sessão e leva ao login com "Sua sessão expirou", guardando a página atual para voltar depois. O `authGuard` protege as rotas e também considera a expiração do token.
- **Feedback por status:** 401, 403, 404, 409 e 429 têm mensagens próprias, voltadas à ação na tela. Os dois tipos de 409 são distinguidos pelo `codigo`: horário ocupado ("Esse horário já está reservado nesta sala") ou versão desatualizada (o formulário avisa e recarrega os dados atuais para o usuário revisar). 400 e 422 mostram o texto da API, que explica a regra. 500 mostra o `traceId` para o suporte. Depois de um 409 ou 422, o formulário mantém os dados para o usuário ajustar.
- **Filtros e paginação na URL** (D3): ver a seção "Filtro e paginação".
- **Ações conforme o perfil:** os botões seguem os `podeEditar` e `podeCancelar` da API, sem regra replicada no cliente. Como esses flags são um retrato do momento, a lista é recarregada depois de qualquer erro numa ação e quando a aba volta a ficar visível.
- **Proteção contra open redirect:** o parâmetro `retorno` do login só aceita caminhos internos (`/...`, e não `//...`).
- **Datas:** o `datetime-local` entrega a hora local, que é enviada em UTC (`toISOString()`). A exibição usa o `DatePipe` com locale pt-BR, no fuso do navegador.
- **Testes:** o enunciado não exige testes de frontend. Há testes unitários para o validador de datas, para o interceptor (anexa o token só à API, ignora token expirado, trata o 401 e não redireciona no 401 do próprio login) e para a tradução de erros (os dois tipos de 409).

**Verificado no navegador**, com a API e o frontend rodando:

- redirecionamento ao login sem sessão;
- login inválido e válido;
- validação do formulário sem requisição à API;
- criação;
- 409 com os dados preservados no formulário;
- edição pela URL de uma reserva de outro usuário (aviso e "Salvar" desabilitado);
- cancelamento com o flag desatualizado: um admin cancelou antes, a resposta foi 422 e a lista foi recarregada;
- token adulterado: 401, sessão encerrada e retorno à página após o login;
- edição com conversão de fuso (10h local ↔ 13:00Z);
- `retorno=//exemplo-malicioso.com` ignorado.

### Concorrência otimista na edição (D1)

Sem controle, se duas pessoas editam a mesma reserva (por exemplo, o dono e um administrador), quem salva por último sobrescreve a outra em silêncio (*lost update*).

- **`rowversion` do SQL Server** na coluna `Reservas.Versao`, mapeada com `IsRowVersion()`. O banco muda o valor a cada `UPDATE`, e o EF Core passa a gravar com `WHERE Id = @id AND Versao = @original`. Se nenhuma linha for afetada, alguém alterou antes, e o EF lança `DbUpdateConcurrencyException`, traduzida para 409. Um contador ou uma data de alteração manuais dependeriam de código lembrar de atualizá-los; a `rowversion` é garantida pelo banco.
- **A versão viaja no corpo:** a resposta traz `versao` (base64), e o `PUT` a exige. No serviço, ela é marcada como valor original da entidade carregada (`Entry(...).OriginalValue`), então o `UPDATE` compara com a versão que **o cliente viu**, e não com a que acabou de ser lida. Isso também cobre uma alteração que aconteça entre a leitura e a gravação dentro da própria requisição.
- **Por que não `ETag`/`If-Match`:** é o padrão HTTP para isso, mas a resposta padrão a um `If-Match` que não confere é 412, e o enunciado pede 409. O campo no corpo atende ao enunciado sem distorcer a semântica de um header.
- **Dois 409, um `codigo` para cada:** `CONFLITO_DE_HORARIO` e `VERSAO_DESATUALIZADA`. Sem isso, quem perdesse a corrida da edição leria "horário ocupado", o que seria falso.
- **Escopo:** só a edição exige a versão do cliente, como pede o enunciado. Cancelar algo que outra pessoa acabou de editar não perde dados, e editar algo cancelado já é barrado pela RN04.
- **Verificado:** o teste de integração D1 (Ana e admin leem a mesma versão; o admin salva e recebe 200; a Ana salva e recebe 409, com o banco mantendo os valores do admin e um único histórico de edição) falha se o serviço deixar de usar a versão do cliente. No navegador, com o formulário da Ana aberto, o admin editou a reserva por fora: o salvamento da Ana recebeu 409, o formulário recarregou com os dados do admin, e o salvamento seguinte, já com a versão nova, passou.

### Filtro e paginação (D3)

- **Paginação por offset** (`pagina`/`tamanhoPagina`, com `Skip`/`Take` e um `COUNT`), com resposta em envelope: `{ itens, pagina, tamanhoPagina, totalItens, totalPaginas }`. Permite "página X de Y" e ir direto a qualquer página. A alternativa, *keyset* (cursor), é estável sob inserções e eficiente em qualquer profundidade, mas não tem total nem acesso direto. Ela faria sentido para rolagem infinita ou volumes grandes; para salas de reunião, o volume por consulta filtrada é pequeno.
- **Ordenação determinística** (`Inicio` e, como desempate, `Id`). Ordenando só por `Inicio`, reservas no mesmo horário podem trocar de posição entre consultas e aparecer em duas páginas ou em nenhuma.
- **O período traz as reservas que se sobrepõem a `[de, ate)`**, e não só as que começam nele: uma reunião das 9h às 11h aparece ao filtrar das 10h às 12h. É a mesma semântica de intervalo semiaberto da RN03. Os dois limites são opcionais.
- **RN06 também na query string:** a exigência de fuso explícito foi extraída para `DatasComFuso` e é usada pelo conversor JSON e pelo filtro. Sem isso, `de=2026-11-01T10:00` seria aceito na query string e interpretado no fuso do servidor.
- **Limites:** `tamanhoPagina` de 1 a 100 (padrão 20) e `pagina` a partir de 1; fora disso, 400. Sem limite, um cliente poderia pedir a tabela inteira de uma vez. Uma página além do fim devolve uma lista vazia com os totais corretos.
- **Frontend:** filtros por sala e por dia (o dia final entra inteiro: "até 07/11" vira `[..., 08/11 00:00)` no fuso do navegador) e controles de página. **O estado fica na URL** (`/reservas?sala=2&de=2026-12-01&pagina=2`), então F5, o botão voltar e links compartilhados preservam os filtros. A consulta usa `switchMap`, que descarta respostas antigas se o usuário trocar de página ou de filtro rapidamente. Depois de cancelar uma reserva, a lista recarrega na mesma página.
- **Verificado no navegador**, com 28 reservas:
  - paginação (20 + 8);
  - filtro por sala e dia (22 reservas em 2 páginas; o dia 01/12 em São Paulo foi enviado como `[03:00Z, 03:00Z do dia seguinte)`);
  - F5 mantendo filtros, campos e página;
  - o botão voltar;
  - o período invertido bloqueado no formulário;
  - "Limpar" restaurando a lista completa.

### Execução com Docker (D4)

```
navegador ──► web (nginx :8080) ──► /api ──► api (.NET :8080) ──► db (SQL Server)
                                                 ▲
                         migrations (aplica e termina)
```

- **Só o frontend é exposto** (`localhost:8080`). API e banco ficam na rede interna do compose, inacessíveis de fora. O nginx serve o Angular (com fallback de SPA) e repassa `/api` à API, então o navegador fala com uma única origem e não há CORS.
- **Imagens multi-stage e sem root:** a imagem final da API tem só o runtime do ASP.NET (sem SDK nem código-fonte) e roda com o usuário `app` das imagens do .NET. O frontend usa o `nginx-unprivileged`.
- **Migrations num serviço próprio** (EF Core *migration bundle*), que roda depois que o banco fica saudável e antes da API (`service_completed_successfully`). Aplicar migrations na inicialização da API exigiria dar à aplicação permissão de DDL só para isso, e uma migration com falha derrubaria a API.
- **Segredos no `.env`** (não versionado), documentados no `.env.example`. O compose usa `${VAR:?mensagem}` e se recusa a subir se faltar a senha do banco ou a chave JWT.
- **IP real atrás do proxy:** o nginx **sobrescreve** o `X-Forwarded-For` com o IP do cliente, e a API o lê (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`). Sem isso, a API veria o IP do nginx em todas as requisições, e o rate limit de login seria global: cinco tentativas de qualquer pessoa bloqueariam todo mundo. Esse modo confia no header vindo de qualquer origem, o que só é seguro porque a API não é acessível de fora da rede do compose. Verificado: forjar o header não burla o limite, e outro cliente continua conseguindo entrar enquanto o primeiro está bloqueado.
- **Lockfile do npm gerado em Linux:** o `package-lock.json` gerado no Windows não incluía dependências opcionais de plataforma usadas pelo build no Linux, e o `npm ci` falhava no container. O lock foi regenerado num container Linux com a mesma versão do npm.
- **Limitações:** a API conecta ao banco como `sa` (em produção, haveria um login com permissões mínimas), e o compose serve HTTP (em produção, o TLS terminaria no nginx ou num balanceador).

### Ambiente e versões

- **.NET 10 (LTS):** o .NET 8 (LTS) e o .NET 9 (STS) encerram o suporte em 10/11/2026. O .NET 10 tem suporte até novembro de 2028, então é a única opção que continua atendendo ao requisito de "versão com suporte ativo" depois da entrega.
- **Angular 22** (a versão estável atual), criado via `npx @angular/cli@22`, sem depender de CLI global. Usa o padrão do CLI: componentes standalone, zoneless (sem `zone.js`), signals e Vitest.
- **SQL Server 2022 em container:** garante que o ambiente do avaliador seja igual ao de desenvolvimento. Uma instância local continua possível, bastando trocar a connection string.
- **xUnit:** padrão de fato no ecossistema .NET, com boa integração com `WebApplicationFactory` e com o ciclo de vida de containers do Testcontainers (`IAsyncLifetime`). NUnit ou MSTest atenderiam igualmente; a escolha é de ecossistema, não técnica.
- **Monorepo** (`backend/`, `frontend/`, `docker-compose.yml` na raiz): uma única entrega, clonada uma vez e executada com um único compose.

### Dependências adicionais

- **`System.Security.Cryptography.Pkcs` referenciado diretamente:** o `Microsoft.Data.SqlClient` 6.1.7, trazido pelo EF Core, depende da versão 9.0.11, que tem vulnerabilidades conhecidas (GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7). A referência direta força uma versão corrigida e pode ser removida quando o SqlClient atualizar.
