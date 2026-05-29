# Hrast ERP — Technical Implementation Roadmap

## Project Summary

ASP.NET Core 10 API-first, multi-tenant SaaS ERP for sawmill and wood processing companies. Organized as a **modular monolith** following **Clean Architecture** principles, with **CQRS** via MediatR. Five business modules: Administration, Procurement, Inventory, Production, Finance.

---

## Architecture Overview

### Solution Structure
```
HrastERP.slnx
├── src/
│   ├── HrastERP.API                      # Composition root only — no controllers, just wires modules
│   ├── HrastERP.SharedKernel             # Base entities, value objects, interfaces, domain events, result types
│   ├── HrastERP.Infrastructure           # Shared DbContext, interceptors, pipeline behaviors
│   ├── Modules/
│   │   ├── Administration/
│   │   │   └── HrastERP.Administration   # Single project, layers as folders
│   │   ├── Procurement/
│   │   │   └── HrastERP.Procurement
│   │   ├── Inventory/
│   │   │   └── HrastERP.Inventory
│   │   ├── Production/
│   │   │   └── HrastERP.Production
│   │   └── Finance/
│   │       └── HrastERP.Finance
└── tests/
    ├── HrastERP.SharedKernel.Tests
    ├── HrastERP.Infrastructure.Tests
    ├── Administration.Tests
    ├── Procurement.Tests
    ├── Inventory.Tests
    ├── Production.Tests
    └── Finance.Tests
```

### Per-Module Internal Structure (Vertical Slice)

Each module is a single project with Clean Architecture layers as folders:

```
HrastERP.<Module>/
├── Domain/           # Entities, aggregates, value objects, events, enumerations, repository interfaces
├── Application/      # CQRS commands/queries (MediatR), validators (FluentValidation), DTOs, event handlers
├── Infrastructure/   # EF Core configurations, repository implementations
├── Web/              # Controllers
└── <Module>Module.cs # DI registration entry point
```

### Per-Module Layer Responsibilities
- **Domain**: Entities, aggregates, value objects, domain events, repository interfaces, domain services
- **Application**: CQRS commands/queries (MediatR), validators (FluentValidation), DTOs, application service interfaces
- **Infrastructure**: EF Core entity configurations, repository implementations, external service integrations
- **Web**: Controllers that receive HTTP requests, send MediatR commands/queries, map Result to HTTP responses

### Cross-Cutting Concerns (SharedKernel + API)
- Multi-tenancy: row-level tenant isolation via `TenantId` on all entities, resolved from JWT claims
- Audit log: EF Core `SaveChanges` interceptor capturing entity state changes to `AuditLog` table
- CQRS pipeline behaviors: logging, validation, audit enrichment, caching
- Background jobs: Hangfire with PostgreSQL storage
- Caching: in-memory (`IMemoryCache`) for Phase 0–3, optionally Redis in later phases
- Email: MailKit with SMTP configuration; abstracted via `IEmailService`
- File storage: local filesystem abstracted via `IFileStorageService`; replaceable with Azure Blob Storage
- PDF generation: QuestPDF abstracted via `IReportGenerator`

---

## Technology Stack

| Concern | Technology |
|---|---|
| Runtime | ASP.NET Core 10 |
| ORM | Entity Framework Core 10 |
| Database | PostgreSQL |
| CQRS / Mediator | MediatR 12 |
| Validation | FluentValidation |
| Authentication | ASP.NET Core Identity + JWT Bearer |
| Authorization | Custom RBAC policy handlers |
| Background Jobs | Hangfire |
| Caching | IMemoryCache (Phase 0–3), Redis optional |
| Email | MailKit |
| File Storage | Local filesystem / IFileStorageService abstraction |
| PDF Reports | QuestPDF |
| API Docs | Scalar / Swagger OpenAPI |
| Testing | xUnit + Moq + FluentAssertions |
| Migrations | EF Core Migrations |

---

## Implementation Phases

---

### Phase 0: Foundation & Infrastructure

**Goal:** Establish solution skeleton, shared infrastructure, and all cross-cutting technical capabilities before any business module work begins.

#### 0.1 Solution & Project Scaffolding
- Create solution with all project stubs (API, SharedKernel, five module triplets, test projects)
- Configure project references following Clean Architecture dependency rules
- Set up `.editorconfig`, global usings, nullable reference types

#### 0.2 SharedKernel
- `BaseEntity<TId>`, `AggregateRoot<TId>` with domain event support
- `ValueObject` base class
- `IDomainEvent` interface
- `Result<T>` / `Error` types for explicit error handling
- `ICurrentTenant` and `ICurrentUser` interfaces
- `PagedResult<T>` for list responses
- Common exception types (`NotFoundException`, `ForbiddenException`, `ValidationException`)

#### 0.3 Database & EF Core Setup
- PostgreSQL connection with EF Core (Docker compose)
- Shared `HrastDbContext` within HrastERP.Infrastructure
- Timestamped entities (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`)
- Soft delete support (`IsDeleted`, `DeletedAt`, `DeletedBy`)
- Global query filter for `TenantId` on all tenant-scoped entities
- Migration pipeline

#### 0.4 Authentication Infrastructure
- ASP.NET Core Identity with custom `ApplicationUser` entity
- JWT Bearer token issuance and validation
- Refresh token support (stored in DB)
- Login, logout, refresh-token endpoints
- Password hashing, lockout configuration

#### 0.5 Multi-Tenancy
- Tenant resolution middleware: extract `TenantId` from JWT claims
- `TenantId` global query filter applied via EF Core
- `ICurrentTenant` service scoped to request
- Tenant isolation validated at application layer

#### 0.6 Authorization Infrastructure
- Custom RBAC permission system: `Permission` enum flags per module operation
- `Role` entity with many-to-many `RolePermissions`
- `ICurrentUser` exposes current user's effective permissions
- `[RequirePermission]` attribute + `IAuthorizationHandler` for policy-based checks
- Predefined role seeds: Administrator, ProcurementOperator, ProductionWorker, WarehouseEmployee, FinanceEmployee

#### 0.7 CQRS Pipeline (MediatR)
- `ValidationBehavior<TRequest, TResponse>`: runs FluentValidation, returns validation errors
- `LoggingBehavior<TRequest, TResponse>`: structured request/response logging
- `AuditBehavior<TRequest, TResponse>`: attaches current user/tenant context to audit enrichment
- `CachingBehavior<TRequest, TResponse>`: optional cache-aside for queries implementing `ICacheable`

#### 0.8 Audit Log
- `AuditLogEntry` entity: `EntityName`, `EntityId`, `Action` (Created/Updated/Deleted), `OldValues` (JSON), `NewValues` (JSON), `UserId`, `TenantId`, `Timestamp`
- EF Core `SaveChangesInterceptor` populates audit entries automatically
- `AuditLog` table is append-only (no update/delete)
- Query API: filter by entity type, entity ID, user, date range

#### 0.9 Background Job Infrastructure
- Hangfire with PostgreSQL storage
- Hangfire Dashboard secured behind admin role
- `IBackgroundJobService` abstraction for enqueue/schedule operations
- Recurring job registration pattern
- Apply to clean-up soft deleted entites

#### 0.10 Email Infrastructure
- `IEmailService` interface with `SendAsync(EmailMessage)` method
- MailKit SMTP implementation
- Email template abstraction (plain string templates initially)
- Configuration via `appsettings.json` (`SmtpSettings`)

#### 0.11 File Storage Infrastructure
- `IFileStorageService` interface: `UploadAsync`, `DownloadAsync`, `DeleteAsync`, `GetUrlAsync`
- Local filesystem implementation with configurable root path
- File metadata entity: `FileId`, `FileName`, `ContentType`, `StoragePath`, `UploadedBy`, `UploadedAt`, `TenantId`
- API endpoints: `POST /files/upload`, `GET /files/{id}`
- Max file size, allowed MIME types validation

#### 0.12 PDF Generation Infrastructure
- `IReportGenerator<TData>` interface
- QuestPDF integration with shared document styles (logo, colors, fonts)
- Base report template (header, footer, page numbering)

#### 0.13 Caching Infrastructure
- `ICacheService` abstraction wrapping `IMemoryCache`
- Cache key conventions per module
- Cache invalidation via MediatR notifications on write operations
- TTL configuration per cache key category

#### 0.14 API Project Setup
- Controller-based routing
- Global exception handler middleware returning `ProblemDetails`
- Request correlation ID middleware
- Swagger/Scalar OpenAPI with JWT auth support
- Health check endpoint
- `appsettings.json` / `appsettings.Development.json` configuration structure

---

### Phase 1: Administration Module

**Goal:** Full user, role, and permission management. Establishes RBAC foundation used by all subsequent modules.

#### 1.1 User Management
- `User` aggregate: personal info, contact details, account status, tenant assignment
- Commands: `CreateUser`, `UpdateUser`, `ActivateUser`, `DeactivateUser`, `ChangePassword`
- Queries: `GetUserById`, `GetUsersList` (paged, filterable by status/role)
- `POST /admin/users`, `PUT /admin/users/{id}`, `PATCH /admin/users/{id}/status`
- `GET /admin/users`, `GET /admin/users/{id}`

#### 1.2 Role Management
- `Role` entity with `RolePermission` join table
- Predefined role seeds with default permissions
- Commands: `CreateRole`, `UpdateRole`, `DeleteRole`, `AssignPermissionsToRole`
- Queries: `GetRoles`, `GetRoleById`, `GetPermissions`
- `POST /admin/roles`, `PUT /admin/roles/{id}`, `DELETE /admin/roles/{id}`
- `GET /admin/roles`, `GET /admin/permissions`

#### 1.3 User-Role Assignment
- `AssignRolesToUser` command, `RemoveRoleFromUser` command
- `GET /admin/users/{id}/roles`
- `PUT /admin/users/{id}/roles` (replaces role set)

#### 1.4 Auth Endpoints
- `POST /auth/login` — returns JWT + refresh token
- `POST /auth/refresh` — exchanges refresh token for new JWT
- `POST /auth/logout` — invalidates refresh token
- `GET /auth/me` — current user profile with permissions

#### 1.5 Tenant Management (Super-Admin)
- `Tenant` entity: name, status, created date
- Seed initial tenant for development
- Basic tenant CRUD accessible only to super-admin role

---

### Phase 2: Procurement Module

**Goal:** Supplier management and full purchase order lifecycle with email notifications and PDF report.

#### 2.1 Supplier Management
- `Supplier` aggregate: company name, contact details, address, tax ID, notes, status
- Commands: `CreateSupplier`, `UpdateSupplier`, `DeactivateSupplier`
- Queries: `GetSuppliers` (paged, filterable), `GetSupplierById`, `GetSupplierPurchaseHistory`
- `POST /procurement/suppliers`, `PUT /procurement/suppliers/{id}`
- `GET /procurement/suppliers`, `GET /procurement/suppliers/{id}`

#### 2.2 Purchase Order Core
- `PurchaseOrder` aggregate with `PurchaseOrderLine` child entities
- Fields: supplier, ordered materials, quantities, agreed pricing, expected delivery date, notes, payment status
- `PurchaseOrderStatusHistory` entity (append-only status change log)
- Workflow statuses: `Draft → Approved → Ordered → Received → Closed | Cancelled`
- Commands: `CreatePurchaseOrder`, `UpdatePurchaseOrder`, `AddOrderLine`, `RemoveOrderLine`
- `POST /procurement/purchase-orders`, `PUT /procurement/purchase-orders/{id}`

#### 2.3 Purchase Order Workflow
- `ApprovePurchaseOrder` command (requires Procurement permission) — transitions Draft → Approved; triggers email to supplier
- `MarkAsOrdered` command — Approved → Ordered
- `MarkAsReceived` command (requires Warehouse permission) — Ordered → Received; triggers inventory goods receiving event
- `ClosePurchaseOrder` command — Received → Closed
- `CancelPurchaseOrder` command — any non-final state → Cancelled
- `GET /procurement/purchase-orders/{id}/history` — status change audit trail
- `PATCH /procurement/purchase-orders/{id}/status`

#### 2.4 Supplier Email Notification
- On `ApprovePurchaseOrder`: send order confirmation email to supplier contact email
- Email contains order details (materials, quantities, prices, delivery date)
- Uses `IEmailService` + email template

#### 2.5 Procurement Report (PDF)
- `GenerateProcurementReport` query: filtered by supplier, date range
- Report content: list of purchase orders with lines, quantities, unit prices, totals
- PDF generated via `IReportGenerator`, returned as file download
- `GET /procurement/reports/purchases?supplierId=&from=&to=`

---

### Phase 3: Inventory Module

**Goal:** Material and product type catalog, goods receiving from POs, stock tracking, immutable transaction log, low stock background monitoring, and inventory report.

#### 3.1 Material Types
- `MaterialType` entity: tree type, log length, log diameter, measurement unit, classification
- Commands: `CreateMaterialType`, `UpdateMaterialType`, `DeactivateMaterialType`
- Queries: `GetMaterialTypes`, `GetMaterialTypeById`
- `POST /inventory/material-types`, `PUT /inventory/material-types/{id}`
- `GET /inventory/material-types`

#### 3.2 Product Types
- `ProductType` entity: name, dimensions, wood type, measurement unit, product category
- Commands: `CreateProductType`, `UpdateProductType`, `DeactivateProductType`
- Queries: `GetProductTypes`, `GetProductTypeById`
- `POST /inventory/product-types`, `PUT /inventory/product-types/{id}`
- `GET /inventory/product-types`

#### 3.3 Inventory Stock
- `InventoryItem` aggregate: `ItemId`, `ItemType` (Material/Product), `MaterialTypeId` or `ProductTypeId`, `TotalQuantity`, `AvailableQuantity`, `ReservedQuantity`, `MinimumThreshold`, `TenantId`
- Auto-created on first stock movement
- `GetInventoryItems` query (filterable by type, wood species)
- `GET /inventory/items`, `GET /inventory/items/{id}`

#### 3.4 Goods Receiving
- `GoodsReceipt` aggregate: related purchase order, actual delivered quantities, wood type, supplier batch reference, delivery date, quality notes
- `ReceiveGoods` command: validates against open PO, creates `InventoryTransaction`, updates `InventoryItem` stock
- Publishes `GoodsReceivedDomainEvent` → Procurement module handles PO status update to Received
- `POST /inventory/goods-receipts`
- `GET /inventory/goods-receipts`, `GET /inventory/goods-receipts/{id}`

#### 3.5 Inventory Transactions
- `InventoryTransaction` entity: type, item, quantity change (positive/negative), timestamp, user, related document reference
- Transaction types: `InboundDelivery`, `MaterialConsumption`, `ProductionOutput`, `ManualAdjustment`, `WasteDisposal`
- Append-only (no update/delete in repository)
- `GET /inventory/transactions` (filterable by type, item, date range)
- `POST /inventory/transactions/adjustment` (manual adjustments only)

#### 3.6 Low Stock Background Job
- Recurring Hangfire job: `LowStockMonitoringJob` (configurable interval, default 1 hour)
- Queries all `InventoryItem` records where `AvailableQuantity <= MinimumThreshold`
- For each low stock item: creates `LowStockAlert` record, sends email notification to warehouse employees
- `LowStockAlert` entity: item, current quantity, threshold, detected at, acknowledged flag
- `GET /inventory/alerts` (unacknowledged alerts)
- `PATCH /inventory/alerts/{id}/acknowledge`

#### 3.7 Inventory Status Report (PDF)
- `GenerateInventoryReport` query: filtered by product type, material type, wood species, date range
- Report content: current stock quantities, available quantities, grouped by item type
- `GET /inventory/reports/status?type=&woodSpecies=&from=&to=`

---

### Phase 4: Production Module

**Goal:** Machine registry, production batch and job lifecycle with multi-phase workflow, inventory integration on job completion, and production reports.

#### 4.1 Machine Management
- `Machine` aggregate: identifier, machine type, operational status (Active/Maintenance/Inactive), availability flag, assigned operator IDs
- Commands: `CreateMachine`, `UpdateMachine`, `SetMachineStatus`, `AssignOperator`
- Queries: `GetMachines` (filterable by type/status), `GetMachineById`
- `POST /production/machines`, `PUT /production/machines/{id}`
- `GET /production/machines`

#### 4.2 Production Batch Management
- `ProductionBatch` aggregate: batch identifier, description, status, created date, target output
- Commands: `CreateProductionBatch`, `UpdateProductionBatch`, `CloseProductionBatch`
- `GET /production/batches`, `GET /production/batches/{id}`

#### 4.3 Production Job Management
- `ProductionJob` entity (child of batch): unique job ID, batch ID, assigned machine, assigned operator, start/end time, status (`Pending → Executing → Done`), production phase, input materials, output products
- Production phases: `Debarking`, `PrimaryBreakdown`, `SecondaryCutting`, `Drying`, `Finishing`
- Commands: `CreateProductionJob`, `StartProductionJob`, `CompleteProductionJob`
- `CompleteProductionJob` triggers inventory integration (see 4.4)
- `POST /production/jobs`, `PATCH /production/jobs/{id}/start`, `PATCH /production/jobs/{id}/complete`
- `GET /production/batches/{batchId}/jobs`, `GET /production/jobs/{id}`

#### 4.4 Inventory Integration on Job Completion
- `CompleteProductionJob` command handler publishes `ProductionJobCompletedDomainEvent`
- Inventory module handler processes event:
  - Creates `MaterialConsumption` inventory transactions for each input material
  - Creates `ProductionOutput` inventory transactions for each output product
  - Updates `InventoryItem` quantities accordingly
- Intermediate products tracked as inventory items available for next production phase

#### 4.5 Job Scheduling
- `ScheduleProductionJob` command: assign job to machine queue with priority order
- `GetMachineQueue` query: ordered list of pending jobs per machine
- `ReorderJobQueue` command: adjust execution order for pending jobs
- `GET /production/machines/{machineId}/queue`

#### 4.6 Production Reports (PDF)
- `GenerateProductionReport` query: filterable by machine, batch, operator, date range, facility-wide
- Report content: consumed materials, produced outputs, machine utilization, job/batch completion summary
- `GET /production/reports?machineId=&batchId=&operatorId=&from=&to=`

---

### Phase 5: Finance Module

**Goal:** Supplier invoices from POs, sales invoice CRUD, unified payment transactions table, and financial reports.

#### 5.1 Supplier Invoices
- `SupplierInvoice` aggregate: invoice ID, related PO, supplier reference, issue date, total amount, status (`Unpaid → Paid | Cancelled`)
- `CreateSupplierInvoice` command: manual or triggered from PO closure
- `MarkSupplierInvoicePaid` command: creates outgoing `PaymentTransaction`
- `CancelSupplierInvoice` command
- `POST /finance/supplier-invoices`, `PATCH /finance/supplier-invoices/{id}/status`
- `GET /finance/supplier-invoices`, `GET /finance/supplier-invoices/{id}`

#### 5.2 Sales Invoices
- `SalesInvoice` aggregate: invoice ID, customer reference, issue date, total amount, status (`Unpaid → Paid | Cancelled`)
- Full CRUD: `CreateSalesInvoice`, `UpdateSalesInvoice`, `CancelSalesInvoice`
- `MarkSalesInvoicePaid` command: creates incoming `PaymentTransaction`, optionally publishes `SalesInvoicePaidEvent` for inventory deduction
- `POST /finance/sales-invoices`, `PUT /finance/sales-invoices/{id}`, `PATCH /finance/sales-invoices/{id}/status`
- `GET /finance/sales-invoices`, `GET /finance/sales-invoices/{id}`

#### 5.3 Payment Transactions
- `PaymentTransaction` entity: payment ID, direction (`Incoming/Outgoing`), related invoice ID, amount, payment date, payment method (`BankTransfer/ManualEntry`), external reference, status (`Pending/Succeeded/Failed`)
- Created by invoice payment commands — not directly via API
- `GET /finance/transactions` (filterable by direction, status, date range)
- `GET /finance/transactions/{id}`

#### 5.4 Financial Reports (PDF)
- `GenerateFinancialReport` query: filterable by date range, supplier, customer
- Report content: total expenses (outgoing payments, supplier costs), total income (incoming payments), net balance
- `GET /finance/reports?from=&to=&supplierId=&customerId=`

---

### Phase 6: Hardening, Integration & Developer Experience

**Goal:** Complete the system with audit log API, file upload usage in modules, advanced caching, integration testing, and API polish.

#### 6.1 Audit Log API
- `GET /admin/audit-log` — filterable by entity type, entity ID, user, date range
- Returns paged list of `AuditLogEntry` records
- Accessible only to Administrator role

#### 6.2 File Uploads in Modules
- Attach file uploads to Procurement: purchase order attachments, delivery documents
- Attach file uploads to Production: quality inspection photos
- `POST /files/upload` with module context tag
- `FileReference` entity linked to parent aggregate

#### 6.3 Caching Enhancements
- Apply `ICacheable` to high-read, low-write queries: `GetMaterialTypes`, `GetProductTypes`, `GetRoles`, `GetPermissions`, `GetMachines`
- Cache invalidation on corresponding write commands via MediatR notifications
- Cache warm-up on application startup for reference data

#### 6.4 Integration & Health
- Module integration tests covering end-to-end scenarios (PO → GoodsReceiving → InventoryUpdate)
- `GET /health` with DB connectivity and Hangfire status checks
- API versioning setup (`/api/v1/`)

#### 6.5 Seed Data & Development Setup
- `DbInitializer`: seeds default tenant, admin user, all roles with default permissions, sample material types, product types, machines
- `docker-compose.yml`: PostgreSQL + Hangfire Dashboard
- README with setup instructions

---

## Module Dependency Map

```
Administration  ──────────────────────────────────────────► All Modules (Auth/Authz)
Procurement     ──── GoodsReceivedEvent ──────────────────► Inventory
Inventory       ──── InventoryReservedEvent ──────────────► Production
Production      ──── ProductionJobCompletedEvent ──────────► Inventory
Finance         ──── SalesInvoicePaidEvent ────────────────► Inventory (optional)
Procurement     ──── PurchaseOrderClosedEvent ─────────────► Finance (invoice creation)
```

Inter-module communication is exclusively via **domain events** (MediatR notifications). Modules do not reference each other's domain or application layers directly.

---

## Phase Summary

| Phase | Module / Concern | Key Deliverables |
|---|---|---|
| 0 | Foundation | Solution structure, SharedKernel, Auth, CQRS, Audit, Jobs, Email, Files, PDF, Cache |
| 1 | Administration | Users, Roles, Permissions, RBAC, Auth endpoints |
| 2 | Procurement | Suppliers, Purchase orders, Order workflow, Email notification, Procurement PDF report |
| 3 | Inventory | Material/Product types, Goods receiving, Stock tracking, Transactions, Low stock job, Inventory PDF report |
| 4 | Production | Machines, Batches, Jobs, Phase workflow, Inventory integration, Job scheduling, Production PDF report |
| 5 | Finance | Supplier invoices, Sales invoices, Payment transactions, Financial PDF report |
| 6 | Hardening | Audit log API, File attachments, Cache optimization, Integration tests, Seed data, Docker setup |
