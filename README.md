# `Domain-Driven Design, Clean Architecture e Hexagonal Architecture: um estudo de caso sobre refatoração de arquiteturas em camadas.`

# `ETAPA 1/2:`

# `Projeto sem padrões`

# 🧭 HelpDesk API — Sistema de Gerenciamento de Chamados

## 📘 Visão Geral

O **HelpDesk** é um sistema completo de **gerenciamento de tickets de suporte técnico**, projetado em **.NET** com **Entity Framework Core**, **Swagger** e **integração AWS S3** para armazenamento de anexos.  
Inclui autenticação simplificada via header `userId`, controle de acesso por papéis (Requester, Agent, Manager), **notificações automáticas por e-mail** e **monitoramento de SLA**.

## 🧩 Estrutura Geral do Projeto

```
HelpDesk/
├─ Properties/
│  └─ launchSettings.json                     # Perfis de execução (IIS Express/Kestrel)
│
├─ Controllers/
│  ├─ AttachmentsController.cs                # Upload/List/Get/Delete anexos (S3 via FileStorageService)
│  ├─ CategoriesController.cs                 # CRUD básico de categorias + validações (2 níveis)
│  ├─ CommentsController.cs                   # Comentários (público/interno), ACL por participante
│  ├─ SwaggerExportController.cs              # (opcional) endpoint utilitário p/ export Swagger
│  ├─ TicketsController.cs                    # Tickets: Create/Update/PATCH + assign/requester/status/reopen/cancel
│  └─ UsersController.cs                      # Gestão de usuários (GET/PATCH/DELETE)
│
├─ Data/
│  └─ AppDbContext.cs                         # DbContext EF Core (Pomelo MySQL) + DbSets e mapeamentos
│
├─ HostedServices/
│  └─ SlaBackgroundService.cs                 # Worker que dispara alertas de SLA (≥85%) por e-mail
│
├─ Migrations/                                # Migrations do EF Core (schema e seeds)
│
├─ Models/
│  ├─ AttachmentModel.cs                      # Anexos (chave S3, URL pública, uploader)
│  ├─ CategoryModel.cs                        # Categorias com ParentId (máx. 2 níveis)
│  ├─ DTOs.cs                                 # Request/Response DTOs usados nos controllers
│  ├─ Enums.cs                                # Status, Priority, CommentVisibility + helpers (ToSla etc.)
│  ├─ TicketActionModel.cs                    # Log das ações do ticket (descricao, createdAt)
│  ├─ TicketCommentModel.cs                   # Comentários (autor opcional, visibilidade)
│  ├─ TicketModel.cs                          # Entidade principal; SLA (CreatedAt, SlaDueAt, SlaStartAt)
│  └─ UserModel.cs                            # Usuário (Name, Email, Role: Requester/Agent/Manager)
│
├─ Options/
│  ├─ S3Options.cs                            # { Bucket, Region, BaseUrl, ... }
│  └─ SmtpOptions.cs                          # { Host, Port, User, Password, FromName, FromEmail }
│
├─ Services/
│  ├─ EmailService.cs                         # Envio via MailKit (SMTP Google/Gmail)
│  ├─ FileStorageService.cs                   # Persistência de arquivo no S3 (upload/delete)
│  └─ NotificationService.cs                  # Orquestra e-mail: SLA + TicketActions (com “paper card”)
│
├─ appsettings.json                           # ConnString MySQL, S3, SMTP, etc.
├─ appsettings.Development.json               # Overrides locais
├─ HelpDesk.http                              # Coleções de chamadas HTTP p/ testar endpoints
├─ Program.cs                                 # DI, Swagger, HealthChecks (_db/_s3/_email), HostedService
└─ HelpDesk.csproj
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

Executa a cada ciclo de tempo (5 minutos) verificando tickets cujo tempo decorrido ultrapassou **85% do SLA**, disparando alerta via e-mail.

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

- **.NET 8 / C#**
- **Entity Framework Core (Pomelo MySQL Provider)**
- **Swagger / Swashbuckle.AspNetCore**
- **Amazon S3 (AWS SDK)**
- **MailKit / MimeKit**
- **Hosted Services / Background Tasks**

---

## 💡 Boas Práticas Implementadas

- Controllers documentadas com `[SwaggerOperation]`, `[ProducesResponseType]` e `[SwaggerParameter]`.
- Validações com mensagens de erro claras e detalhadas.
- Uso de transações EF (`BeginTransactionAsync`) em operações críticas.
- Padronização de respostas HTTP (200, 400, 401, 403, 404, 409).
- Cobertura de Testes Unitários.

---
