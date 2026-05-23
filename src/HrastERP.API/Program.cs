using HrastERP.Administration.Infrastructure.Extensions;
using HrastERP.Finance.Infrastructure.Extensions;
using HrastERP.Infrastructure.Extensions;
using HrastERP.Inventory.Infrastructure.Extensions;
using HrastERP.Procurement.Infrastructure.Extensions;
using HrastERP.Production.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAdministrationInfrastructure();
builder.Services.AddFinanceInfrastructure();
builder.Services.AddInventoryInfrastructure();
builder.Services.AddProcurementInfrastructure();
builder.Services.AddProductionInfrastructure();

var app = builder.Build();

app.Run();
