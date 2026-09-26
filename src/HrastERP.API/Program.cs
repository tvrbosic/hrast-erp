using System.Text;
using Hangfire;
using HrastERP.Administration;
using HrastERP.API.Authentication;
using HrastERP.API.Authorization;
using HrastERP.API.Extensions;
using HrastERP.API.Middleware;
using HrastERP.Finance;
using HrastERP.Infrastructure;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Logging;
using HrastERP.Infrastructure.Hangfire.Filters;
using HrastERP.Infrastructure.Hangfire.Services;
using HrastERP.Infrastructure.Database;
using HrastERP.Inventory;
using HrastERP.Procurement;
using HrastERP.Production;
using HrastERP.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog as the logging provider (replaces default providers).
// Called on builder directly (not through AddInfrastructure) because UseSerilog is on builder.Host.
builder.AddLoggingInfrastructure();

// Register all shared infrastructure: persistence (EF Core, interceptors), identity, and MediatR behaviors
builder.Services.AddInfrastructure();

// AddHttpContextAccessor registers IHttpContextAccessor, which gives DI-injected services (like CurrentUser)
// access to HttpContext (and thus JWT claims) outside of controllers and middleware.
builder.Services.AddHttpContextAccessor();

// Register ICurrentUser and ICurrentTenant — resolved from JWT claims per-request by application handlers.
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()!;

// Configure JWT Bearer as the default authentication scheme.
// AddAuthentication sets the default scheme so [Authorize] uses it without naming it explicitly.
// AddJwtBearer configures how incoming tokens are validated (issuer, audience, signing key, expiry).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

// Register authorization services that evaluate [Authorize] attributes and policies against the authenticated user.
builder.Services.AddAuthorization();

// Replace the default policy provider with one that builds Permission policies dynamically.
// Singleton is safe — policy construction uses only static enum values.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

// Scoped because it depends on scoped ICurrentUser (one per HTTP request).
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Register each business module's services (MediatR handlers, validators, EF configurations, repositories)
builder.Services
    .AddAdministrationModule(builder.Configuration)
    .AddFinanceModule(builder.Configuration)
    .AddInventoryModule(builder.Configuration)
    .AddProcurementModule(builder.Configuration)
    .AddProductionModule(builder.Configuration);

// Register MVC controllers from the API project.
// AddApplicationPart tells MVC to also scan each module assembly for controllers,
// since controllers live in module projects rather than in HrastERP.API.
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(AdministrationModule).Assembly)
    .AddApplicationPart(typeof(FinanceModule).Assembly)
    .AddApplicationPart(typeof(InventoryModule).Assembly)
    .AddApplicationPart(typeof(ProcurementModule).Assembly)
    .AddApplicationPart(typeof(ProductionModule).Assembly);

// Configure the model binding error format to return consistent error message (ErrorResponse type) in case of API layer validation errors.
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

app.UseAuthorization();

// Build the routing table by mapping HTTP routes to controller actions discovered in all application parts.
app.MapControllers();

// Health check endpoint — unauthenticated, available in all environments.
app.UseHealthCheckEndpoint();

// OpenAPI document + Scalar UI — Development only.
app.UseOpenApiInfrastructure();

app.Run();
