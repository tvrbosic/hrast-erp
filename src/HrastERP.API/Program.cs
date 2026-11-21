using Hangfire;
using HrastERP.API.Authentication;
using HrastERP.API.Authorization;
using HrastERP.API.HealthChecks;
using HrastERP.API.Middleware;
using HrastERP.API.OpenApi;
using HrastERP.API.Responses;
using HrastERP.Administration;
using HrastERP.Finance;
using HrastERP.Infrastructure;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Hangfire;
using HrastERP.Infrastructure.Logging;
using HrastERP.Inventory;
using HrastERP.Procurement;
using HrastERP.Production;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog as the logging provider (replaces default providers).
// Called on builder directly (not through AddInfrastructure) because UseSerilog is on builder.Host.
builder.AddLoggingInfrastructure();

// Register all shared infrastructure: persistence (EF Core, interceptors), identity, and MediatR behaviors
builder.Services.AddInfrastructure();

// Validate incoming JWT access tokens and expose their claims as ICurrentUser / ICurrentTenant.
builder.Services.AddApiAuthentication(builder.Configuration);

// Register the SuperAdminTenant and Permission policies, their handlers, and the 403 ErrorResponse writer.
builder.Services.AddApiAuthorization();

// Register each business module's services (MediatR handlers, validators, EF configurations, repositories)
builder.Services
    .AddAdministrationModule(builder.Configuration)
    .AddFinanceModule(builder.Configuration)
    .AddInventoryModule(builder.Configuration)
    .AddProcurementModule(builder.Configuration)
    .AddProductionModule(builder.Configuration);

// Register MVC controllers from the API project.
// AddApplicationPart tells MVC to also scan each module assembly for discoverable types
// (e.g. controllers that may be added to modules in the future).
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(AdministrationModule).Assembly)
    .AddApplicationPart(typeof(FinanceModule).Assembly)
    .AddApplicationPart(typeof(InventoryModule).Assembly)
    .AddApplicationPart(typeof(ProcurementModule).Assembly)
    .AddApplicationPart(typeof(ProductionModule).Assembly);

// Configure the model binding error format to return consistent error message (ErrorResponse type) in case of API layer validation errors,
// and keep other framework client errors (e.g. 415) bodiless so StatusCodeErrorResponse can write their ErrorResponse.
builder.Services.ConfigureModelBindingErrorFormat();

// Register OpenAPI document generation with JWT security scheme for Scalar UI.
builder.Services.AddOpenApiServices();

// Register health checks for PostgreSQL, Redis, and SMTP connectivity.
builder.Services.AddHealthCheckServices(builder.Configuration);

var app = builder.Build();

// Requires migrations to be applied first: dotnet ef database update
// Reference seeds always run; dev fixtures run only in Development environment.
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

// Register recurring background jobs (cleanup tasks, etc.) with Hangfire. Must be run after DI container is fully build.
// For that reason it is called here and not in BackgroundJobServiceExtensions.
RecurringJobRegistrar.RegisterAll(app.Services);

// Safety net for unhandled infrastructure/framework exceptions. Must be first so it wraps the entire pipeline.
// Application-layer failures use Result.Failure — this middleware only catches unexpected exceptions (DB errors, bugs, etc.).
app.UseMiddleware<GlobalExceptionMiddleware>();

// Give the framework's bodiless 404 (no route), 405 (wrong method) and 415 (unsupported Content-Type) an
// ErrorResponse body. Only runs for error statuses with no body, so responses our code wrote are untouched.
app.UseStatusCodePages(StatusCodeErrorResponse.WriteAsync);

// Serilog HTTP request logging — one structured log line per request (method, path, status code, elapsed time).
app.UseLoggingInfrastructure();

// Hangfire dashboard — restricted to localhost connections only.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new LocalhostDashboardAuthorizationFilter()]
});

// UseAuthentication reads the Bearer token from the request, validates it, and populates HttpContext.User.
// UseAuthorization checks whether the authenticated user is allowed to access the endpoint ([Authorize] etc.).
// Order matters: authentication must run before authorization.
app.UseAuthentication();

// Enrich all subsequent log entries with UserId and TenantId from JWT claims.
// Must run after UseAuthentication so claims are populated.
app.UseLoggingEnrichment();

// Block requests from deactivated users or tenants. Checks cached IsActive status
// and returns 403 if either the user or their tenant has been deactivated.
// Must run after authentication (needs JWT claims) and before authorization.
app.UseMiddleware<BlockInactiveEntityMiddleware>();

app.UseAuthorization();

// Build the routing table by mapping HTTP routes to controller actions discovered in all application parts.
app.MapControllers();

// Health check endpoint — unauthenticated, available in all environments.
app.UseHealthCheckEndpoint();

// OpenAPI document + Scalar UI — Development only.
app.UseOpenApiInfrastructure();

app.Run();
