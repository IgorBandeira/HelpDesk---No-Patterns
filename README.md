# `Domain-Driven Design, Clean Architecture e Hexagonal Architecture: um estudo de caso sobre refatoração de arquiteturas em camadas.`

# `ETAPA 1/2:`

# `Projeto com arquitetura em camadas simples`

# 🧭 HelpDesk API — Sistema de Gerenciamento de Chamados

## 📘 Visão Geral

O **HelpDesk** é um sistema completo de **gerenciamento de tickets de suporte técnico**, projetado em **.NET** com **Entity Framework Core**, **Swagger** e **integração AWS S3** para armazenamento de anexos.  
Inclui autenticação simplificada via header `userId`, controle de acesso por papéis (Requester, Agent, Manager), **notificações automáticas por e-mail** e **monitoramento de SLA**.

## 🧩 Estrutura Geral do Projeto

```
📦 HelpDesk.sln
│
├─ 📁 HelpDesk/                                  # 🌐 Projeto principal da API (ASP.NET Core)
│  │
│  ├─ Properties/
│  │  └─ launchSettings.json                      # Perfis de execução: IIS Express / Kestrel / ambiente Development
│  │
│  ├─ Controllers/                                # 🎯 Camada de Entrada (REST Controllers)
│  │  ├─ AttachmentsController.cs                 # Upload/List/Get/Delete de anexos (com regras de extensão, limite, autor)
│  │  ├─ CategoriesController.cs                  # CRUD de categorias com limite de 2 níveis e validações
│  │  ├─ CommentsController.cs                    # Comentários públicos/internos + regras de visibilidade e ACL
│  │  ├─ SwaggerExportController.cs               # Endpoint opcional para exportar Swagger JSON
│  │  ├─ TicketsController.cs                     # Controller principal: criação, edição, workflow, SLA, reopen/cancel
│  │  └─ UsersController.cs                       # Criação, edição e deleção de usuários + validações de tickets ativos
│  │
│  ├─ Data/
│  │  └─ AppDbContext.cs                          # EF Core DbContext (MySQL) com DbSets e configurações
│  │
│  ├─ HostedServices/
│  │  └─ SlaBackgroundService.cs                  # Serviço em segundo plano: monitora SLA e dispara alertas
│  │
│  ├─ Migrations/                                 # Arquivos gerados do EF: schema inicial + seeds
│  │
│  ├─ Models/
│  │  ├─ AttachmentModel.cs                       # Entidade de anexos (chaves S3, metadados, autor)
│  │  ├─ CategoryModel.cs                         # Entidade categoria com ParentId (1 nível de hierarquia)
│  │  ├─ DTOs.cs                                  # Todos os DTOs usados nos endpoints (requests/responses)
│  │  ├─ Enums.cs                                 # Status, Prioridades, Visibilidades e helpers ToSla()
│  │  ├─ TicketActionModel.cs                     # Log de ações automáticas do ticket (auditoria)
│  │  ├─ TicketCommentModel.cs                    # Comentários do ticket + visibilidade
│  │  ├─ TicketModel.cs                           # Entidade principal, incluindo cálculo de SLA e datas
│  │  └─ UserModel.cs                             # Usuário com Role (Requester, Agent, Manager)
│  │
│  ├─ Options/
│  │  ├─ S3Options.cs                             # Configurações tipadas p/ S3 ({Bucket, BaseUrl, Region...})
│  │  └─ SmtpOptions.cs                           # Config SMTP (Host, Porta, Credenciais, DisableDelivery)
│  │
│  ├─ Services/
│  │  ├─ EmailService.cs                          # Envio de e-mail via MailKit (SMTP Google/Gmail)
│  │  ├─ FileStorageService.cs                    # Abstração S3: upload/delete de arquivos
│  │  └─ NotificationService.cs                   # Orquestra notificações por e-mail (SLA + TicketActions)
│  │
│  ├─ appsettings.json                            # Connection string e configs
│  ├─ appsettings.Development.json                # Overrides locais para ambiente Dev
│  ├─ HelpDesk.http                               # Arquivo para testar endpoints via VS/REST Client
│  ├─ Program.cs                                  # Boot da aplicação: DI, Swagger, HealthChecks, HostedService
│  └─ HelpDesk.csproj
│
│
└─ 📁 HelpDesk.Tests/                             # 🧪 Projeto de testes (unitários + integração)
   │
   ├─ 📁 IntegrationTests/                        # 🌐 Testes end-to-end (API real via HttpClient)
   │  │
   │  ├─ 📁 Attachments/
   │  │  └─ Attachments_Integration_Upload_Tests.cs 
   │  │       # Testa upload real (multipart form), bloqueios de extensão, 201 Created
   │  │
   │  ├─ 📁 Categories/
   │  │  └─ Categories_Integration_Tests.cs        # CRUD completo de categorias via API
   │  │
   │  ├─ 📁 Comments/
   │  │  └─ Comments_Integration_Tests.cs          # Comentários + visibilidade + autor
   │  │
   │  ├─ 📁 Tickets/
   │  │  ├─ Tickets_Integration_Create_Tests.cs    # POST /tickets + regras de validação
   │  │  ├─ Tickets_Integration_ListAndDetails_Tests.cs # GET /tickets + filtros + detalhes
   │  │  ├─ Tickets_Integration_Workflow_Tests.cs  # Workflow real: assign, status, reopen/cancel
   │  │  └─ (...)
   │  │
   │  ├─ 📁 Users/
   │  │  └─ Users_Integration_Tests.cs             # Criação e bloqueios quando existem tickets ativos
   │  │
   │  └─ HelpDeskApiFactory.cs                     # WebApplicationFactory<Program> com:
   │                                               # - SQLite in-memory compartilhado
   │                                               # - Mock de Amazon S3
   │                                               # - FileStorageService NOOP
   │                                               # - SMTP desabilitado (DisableDelivery)
   │                                               # - Seed básico (Requester/Agent/Manager)
   │
   │
   ├─ 📁 UnitTests/                                # 🧩 Testes de regra de negócio (sem HTTP)
   │  │
   │  ├─ 📁 Attachments/
   │  │  └─ Attachments_Tests.cs                   # Extensões proibidas, ticket fechado, autor
   │  │
   │  ├─ 📁 Categories/
   │  │  └─ Categories_Tests.cs                    # Hierarquia e nomes duplicados
   │  │
   │  ├─ 📁 Comments/
   │  │  └─ Comments_Tests.cs                      # Visibilidade e ACL
   │  │
   │  ├─ 📁 Services/
   │  │  └─ SlaBackgroundService_Tests.cs          # 85% SLA, duplicidade, ignora fechados
   │  │
   │  ├─ 📁 Tickets/
   │  │  ├─ Tickets_Assign_Tests.cs                # Permissões Manager/Agent
   │  │  ├─ Tickets_Create_Tests.cs                # Validações de criação + SLA
   │  │  ├─ Tickets_List_Tests.cs                  # Filtros, paginação
   │  │  ├─ Tickets_ReopenCancel_Tests.cs          # Motivo obrigatório, comentários internos
   │  │  ├─ Tickets_Status_Tests.cs                # Workflow autorizado + bloqueios
   │  │  └─ Tickets_Update_Tests.cs                # PATCH: 400 sem alterações
   │  │
   │  ├─ 📁 Users/
   │  │  └─ Users_Tests.cs                         # Bloqueios de exclusão e validações de input
   │  │
   │  └─ 📁 Utilities/
   │     ├─ TestDbContextFactory.cs                # DbContext InMemory para UNIT tests
   │     └─ TestHelpers.cs                         # Mocks (S3, SMTP), builders, helpers de headers
   │
   └─ HelpDesk.Tests.csproj                        # xUnit, FluentAssertions, Moq, SQLite, Mvc.Testing

```

---

## 🔐 Autenticação e Papéis

A API usa **autenticação via cabeçalho HTTP**:

```http
userId: 1
```

> Esse identificador é validado no banco de dados em todas as rotas protegidas.

**Papéis suportados:**

- 🧑‍💼 `Manager`: pode criar, editar e excluir usuários, categorias e tickets.
- 👩‍💻 `Agent`: pode atuar em tickets atribuídos e alterar status.
- 🙋‍♂️ `Requester`: cria e gerencia seus próprios tickets.

---

## 🎫 TicketsController (`/api/tickets`)

Gerencia todo o ciclo de vida de um ticket, desde a criação até o fechamento.

### 🔹 Regras Gerais

- **Criação:** apenas `Requester` e `Manager`.
- **Edição:** somente o dono (Requester) ou Manager.
- **Cancelamento e Reabertura:** requer motivo obrigatório (`reason`).
- **SLA:** automático conforme prioridade (Crítica = 8h, Alta = 24h, Média = 48h, Baixa = 72h).
- **Histórico:** cada ação gera uma entrada em `TicketActions`.

### 🔸 Endpoints Principais

| Método                             | Descrição                                                                  |
| ---------------------------------- | -------------------------------------------------------------------------- |
| `GET /api/tickets`                 | Lista tickets com filtros (status, prioridade, datas, usuários, SLA, etc). |
| `GET /api/tickets/{id}`            | Retorna detalhes completos (comentários, anexos, ações).                   |
| `POST /api/tickets`                | Cria novo ticket.                                                          |
| `PATCH /api/tickets/{id}`          | Atualiza título, descrição, prioridade ou categoria.                       |
| `POST /api/tickets/{id}/assign`    | Atribui o ticket a um `Agent`.                                             |
| `POST /api/tickets/{id}/requester` | Altera o `Requester` do ticket.                                            |
| `POST /api/tickets/{id}/status`    | Atualiza o status (Em Análise → Em Andamento → Resolvido → Fechado).       |
| `POST /api/tickets/{id}/reopen`    | Reabre ticket (Resolvido/Fechado → Em Análise). Requer `reason`.           |
| `POST /api/tickets/{id}/cancel`    | Cancela ticket (Novo/Em Análise). Requer `reason`.                         |

### ⚙️ Status Possíveis

```
Novo → Em Análise → Em Andamento → Resolvido → Fechado / Cancelado
```

### 🧾 TicketActions

Cada alteração relevante gera um log automático:

- Mudança de status, prioridade, categoria, descrição ou responsável.
- Comentários e cancelamentos também são registrados.

---

## 💬 CommentsController (`/api/tickets/{ticketId}/comments`)

Permite incluir comunicação entre Requester, Agent e Manager dentro de um ticket.

### 🔹 Regras

- Comentários **internos** só podem ser criados por `Requester`, `Agent` ou `Manager` participantes do ticket.
- **Visibilidades:** `Público` ou `Interno`.
- **Limite:** 4000 caracteres.

### 🔸 Endpoints

| Método         | Descrição                           |
| -------------- | ----------------------------------- |
| `POST`         | Adiciona um novo comentário.        |
| `GET`          | Lista comentários do ticket.        |
| `GET /{id}`    | Retorna comentário específico.      |
| `PUT /{id}`    | Atualiza mensagem (apenas o autor). |
| `DELETE /{id}` | Exclui comentário (apenas o autor). |

---

## 📎 AttachmentsController (`/api/tickets/{ticketId}/attachments`)

Gerencia **anexos** de um ticket, com armazenamento no **Amazon S3**.

### 🔹 Regras

- Apenas tickets **ativos** (não `Fechado`/`Cancelado`) aceitam operações de anexo.
- Cabeçalho obrigatório: `userId` deve existir no banco.
- Tamanho máximo por arquivo: **10 MB**.
- Extensões **bloqueadas**: `.exe`, `.bat`, `.sh`.
- Chave de armazenamento: `tickets/{ticketId}/{fileName}`.

### 🔸 Endpoints

| Método         | Descrição                                                                                                                                                                 |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST`         | **Upload** de arquivo (`multipart/form-data`, campo `file`). Valida tamanho/ extensão, persiste metadados e salva no S3. Retorna `AttachmentResponseDto` com `PublicUrl`. |
| `GET`          | **Lista** anexos do ticket (ordem decrescente por `Id`). Retorna `AttachmentListItemDto[]`.                                                                               |
| `GET /{id}`    | **Detalhe** de um anexo do ticket.                                                                                                                                        |
| `DELETE /{id}` | **Exclui** anexo. Somente o **autor** (`UploadedById == userId`) pode excluir; tickets inativos são bloqueados.                                                           |

Campos principais registrados no modelo:

- `FileName`, `ContentType`, `SizeBytes`, `StorageKey`, `PublicUrl`, `UploadedAt`, `UploadedById`.

---

## 🗂️ CategoriesController (`/api/categories`)

Gerencia categorias hierárquicas de tickets (até 2 níveis).

### 🔹 Regras

- Somente `Manager` pode criar ou excluir.
- Subcategoria só é permitida se o pai não tiver outro pai.
- Nomes devem ser únicos (max. 180 caracteres).

### 🔸 Endpoints

| Método         | Descrição                                                  |
| -------------- | ---------------------------------------------------------- |
| `POST`         | Cria categoria ou subcategoria.                            |
| `GET`          | Lista categorias (filtros: nome, parentId, paginação).     |
| `GET /{id}`    | Retorna categoria pelo ID.                                 |
| `DELETE /{id}` | Exclui categoria (se não tiver filhos nem tickets ativos). |

---

## 👥 UsersController (`/api/users`)

Gerencia usuários, papéis e seus tickets associados.

### 🔹 Regras

- Somente `Manager` pode criar, editar e excluir.
- `Email` deve ser único e válido.
- `Role` deve ser um dos valores: `Requester`, `Agent`, `Manager`.

### 🔸 Endpoints

| Método         | Descrição                                               |
| -------------- | ------------------------------------------------------- |
| `POST`         | Cria usuário.                                           |
| `GET`          | Lista usuários (filtros: role, email, nome, paginação). |
| `GET /{id}`    | Retorna detalhes e tickets relacionados.                |
| `PATCH /{id}`  | Atualiza dados (nome, email, role).                     |
| `DELETE /{id}` | Remove usuário (se não possuir tickets ativos).         |

---

## 📦 Serviços Auxiliares

### 🕐 SlaBackgroundService

Executa a cada ciclo de tempo (5 minutos) verificando tickets cujo tempo decorrido  já atingiu **85% do SLA**, disparando alerta via e-mail.

### ✉️ NotificationService / EmailService

Envia e-mails automáticos para participantes de tickets quando ocorre uma ação importante (mudança de status, atribuição, etc.).

### ☁️ FileStorageService

Gerencia o upload e armazenamento de anexos no **Amazon S3**, usando `IAmazonS3`.

---

## 🧠 SLA e Prioridades

| Prioridade | Tempo de SLA |
| ---------- | ------------ |
| Crítica    | 8h           |
| Alta       | 24h          |
| Média      | 48h          |
| Baixa      | 72h          |

O **SLA** é iniciado no momento da criação (`SlaStartAt`) e pode ser recalculado em alterações de prioridade.

---

## 🧾 Health Checks

| Rota             | Descrição                           |
| ---------------- | ----------------------------------- |
| `/_db/health`    | Testa conexão com o banco de dados. |
| `/_s3/health`    | Testa conexão com o bucket AWS S3.  |
| `/_email/health` | Testa envio de e-mail SMTP.         |

---

## 🧾 Geração de Documentação Swagger

Swagger configurado automaticamente em desenvolvimento:

```
https://localhost:44314/swagger/index.html
```

Para exportar o YAML atualizado, executar o endpoint GET **SwaggerExport**:

```bash
/api/SwaggerExport/yaml
```

---

## 🧰 Tecnologias Utilizadas

### ⚙️ Backend

- **.NET 8 / C#** – estrutura principal da aplicação
- **Entity Framework Core (Pomelo MySQL Provider)** – ORM para persistência de dados
- **Swagger / Swashbuckle.AspNetCore** – geração de documentação OpenAPI
- **Amazon S3 (AWS SDK)** – armazenamento de anexos em nuvem
- **MailKit / MimeKit** – envio e composição de e-mails (alertas e notificações)
- **Hosted Services / Background Tasks** – execução agendada de rotinas (SLA e alertas)

### 🧪 Testes Automatizados

- **xUnit** – framework principal de testes unitários
- **FluentAssertions** – validações legíveis e expressivas (`result.Should().NotBeNull()`)
- **Moq** – criação de *mocks* e *stubs* para dependências externas (e-mail, S3, etc.)
- **EF Core InMemory Provider** – simulação de banco de dados em memória para testes unitários isolados
- **EF Core SQLite (in-memory)** – banco relacional leve para testes de integração mais próximos do cenário real
- **Microsoft.AspNetCore.Mvc.Testing** – uso do `WebApplicationFactory<Program>` para testes de integração da API via `HttpClient`

---

## 💡 Boas Práticas Implementadas

- Controllers documentadas com `[SwaggerOperation]`, `[ProducesResponseType]` e `[SwaggerParameter]`.
- Validações com mensagens de erro claras e detalhadas.
- Uso de transações EF (`BeginTransactionAsync`) em operações críticas.
- Padronização de respostas HTTP (200, 400, 401, 403, 404, 409).
- Cobertura de Testes Unitários e de Integração.

---
