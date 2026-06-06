using System.Text;
using HrastERP.Administration;
using HrastERP.API.Authentication;
using HrastERP.API.Middleware;
using HrastERP.Finance;
using HrastERP.Infrastructure.Configuration;
using HrastERP.Infrastructure.Extensions;
using HrastERP.Inventory;
using HrastERP.Procurement;
using HrastERP.Production;
using HrastERP.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

// Safety net for unhandled infrastructure/framework exceptions. Must be first so it wraps the entire pipeline.
// Application-layer failures use Result.Failure — this middleware only catches unexpected exceptions (DB errors, bugs, etc.).
app.UseMiddleware<GlobalExceptionMiddleware>();

// UseAuthentication reads the Bearer token from the request, validates it, and populates HttpContext.User.
// UseAuthorization checks whether the authenticated user is allowed to access the endpoint ([Authorize] etc.).
// Order matters: authentication must run before authorization.
app.UseAuthentication();
app.UseAuthorization();

// Build the routing table by mapping HTTP routes to controller actions discovered in all application parts.
app.MapControllers();
app.Run();
