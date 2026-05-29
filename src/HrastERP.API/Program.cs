using HrastERP.Administration;
using HrastERP.Finance;
using HrastERP.Infrastructure.Extensions;
using HrastERP.Inventory;
using HrastERP.Procurement;
using HrastERP.Production;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediatRPipelineBehaviors();

builder.Services
    .AddAdministrationModule(builder.Configuration)
    .AddFinanceModule(builder.Configuration)
    .AddInventoryModule(builder.Configuration)
    .AddProcurementModule(builder.Configuration)
    .AddProductionModule(builder.Configuration);

builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(AdministrationModule).Assembly)
    .AddApplicationPart(typeof(FinanceModule).Assembly)
    .AddApplicationPart(typeof(InventoryModule).Assembly)
    .AddApplicationPart(typeof(ProcurementModule).Assembly)
    .AddApplicationPart(typeof(ProductionModule).Assembly);

var app = builder.Build();

app.MapControllers();
app.Run();
