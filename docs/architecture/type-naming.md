# Type Naming

How request, response and data types are named, and where they live. A type's suffix tells you what role it plays; its folder follows from that.

---

## Suffixes

| Suffix | What it is | Where it lives |
|---|---|---|
| `*Command` / `*Query` | MediatR request. Its handler (`*CommandHandler` / `*QueryHandler`) and validator (`*CommandValidator` / `*QueryValidator`) sit next to it. | Its slice: `Application/<Feature>/Commands/<Name>/` or `Application/<Feature>/Queries/<Name>/` |
| `*Response` | What a query handler returns. It is also the API contract, serialized inside the `{ "data": … }` envelope. | `Application/<Feature>/Models/`, always — even when only one query uses it, so it never has to move when a second one does |
| `*Request` | API-only request body. Use it only when no command exists (e.g. `LoginRequest`, since auth endpoints call `IAuthService`, not MediatR), or when the body differs from the command. Otherwise the controller binds the command directly. | `HrastERP.API/Requests/` |

Infrastructure service types (what a service takes or returns, e.g. `AuthTokens`, `AuthenticatedUser`, `StoredFileInfo`, `FileDownload`, `GeneratedReport`, `EmailMessage`) are **plain nouns** with no `Dto`, `Response` or `Result` suffix. `Result` is reserved for `Result` / `Result<T>`, so `Result<GeneratedReport>` reads cleanly where `Result<ReportResult>` did not.

---

## Folders

- Data-type folders are named `Models/`, never `DTOs/`.
- A module's error catalog, `<Module>Errors.cs`, lives at the root of `Application/` — it is module-wide and shared by every feature.
- `Application/<Feature>/Models/` is created together with the feature's first response type; it is not pre-created.

```
HrastERP.<Module>/
  Application/
    <Feature>/
      Commands/<Name>/   # <Name>Command, <Name>CommandHandler, <Name>CommandValidator
      Queries/<Name>/    # <Name>Query, <Name>QueryHandler, <Name>QueryValidator
      Models/            # *Response
    <Module>Errors.cs
```

---

## Namespaces

- **Modules:** the namespace follows the folder, e.g. `HrastERP.Administration.Application.Roles.Models`.
- **Infrastructure:** each service has one namespace, `HrastERP.Infrastructure.<Service>`. Its subfolders (`Models/`, `Services/`, `Entities/`, `Persistence/`, `Configurations/`, `Jobs/`, `Filters/`, `Audit/` …) never appear in the namespace, so one `using HrastERP.Infrastructure.FileStorage;` gives a caller the service and every type it uses. The one exception is `HrastERP.Infrastructure.Database.Migrations`, which EF Core generates.
