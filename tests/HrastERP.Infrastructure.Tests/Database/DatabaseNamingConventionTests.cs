using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using HrastERP.Administration;
using HrastERP.Finance;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.FileStorage;
using HrastERP.Infrastructure.Identity;
using HrastERP.Inventory;
using HrastERP.Procurement;
using HrastERP.Production;
using HrastERP.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Database;

public class DatabaseNamingConventionTests
{
    private static readonly Regex SnakeCase = new("^[a-z][a-z0-9_]*$");

    // Every module assembly, so entities added to any module are covered by the schema guard
    private static readonly Assembly[] ModuleAssemblies =
    [
        typeof(AdministrationModule).Assembly,
        typeof(FinanceModule).Assembly,
        typeof(InventoryModule).Assembly,
        typeof(ProcurementModule).Assembly,
        typeof(ProductionModule).Assembly,
    ];

    // Entities configured outside a module (Infrastructure or Identity) whose data belongs to a module.
    // Anything else not defined in a module assembly must live in the infrastructure schema.
    private static readonly Dictionary<Type, string> ModuleOwnedInfrastructureEntities = new()
    {
        [typeof(ApplicationUser)] = DatabaseSchemas.Administration,
        [typeof(IdentityUserClaim<Guid>)] = DatabaseSchemas.Administration,
        [typeof(IdentityUserLogin<Guid>)] = DatabaseSchemas.Administration,
        [typeof(IdentityUserToken<Guid>)] = DatabaseSchemas.Administration,
        [typeof(Role)] = DatabaseSchemas.Administration,
        [typeof(RefreshToken)] = DatabaseSchemas.Administration,
        [typeof(Tenant)] = DatabaseSchemas.Administration,
    };

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<HrastDbContext>().UseNpgsql("Host=unused").Options;
        var tenant = Substitute.For<ICurrentTenant>();
        using var context = new HrastDbContext(
            options,
            ModuleAssemblies.Select(a => new EntityConfigurationAssembly(a)),
            tenant);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Theory]
    [InlineData(typeof(Tenant), "administration", "tenants")]
    [InlineData(typeof(RefreshToken), "administration", "refresh_tokens")]
    [InlineData(typeof(Role), "administration", "roles")]
    [InlineData(typeof(ApplicationUser), "administration", "users")]
    [InlineData(typeof(IdentityUserClaim<Guid>), "administration", "user_claims")]
    [InlineData(typeof(IdentityUserLogin<Guid>), "administration", "user_logins")]
    [InlineData(typeof(IdentityUserToken<Guid>), "administration", "user_tokens")]
    [InlineData(typeof(AuditLogEntry), "infrastructure", "audit_logs")]
    [InlineData(typeof(StoredFile), "infrastructure", "stored_files")]
    public void Maps_entity_to_plural_snake_case_table_When_model_is_built(Type clrType, string schema, string table)
    {
        var entityType = BuildModel().FindEntityType(clrType)!;

        entityType.GetTableName().Should().Be(table);
        entityType.GetSchema().Should().Be(schema);
    }

    [Fact]
    public void Uses_snake_case_for_all_tables_columns_keys_and_indexes_When_model_is_built()
    {
        var violations = new List<string>();

        void Check(string description, string? name)
        {
            if (name is null || !SnakeCase.IsMatch(name))
                violations.Add($"{description}: {name}");
        }

        foreach (var entityType in BuildModel().GetEntityTypes())
        {
            var table = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
            Check($"table of {entityType.ClrType.Name}", entityType.GetTableName());

            foreach (var property in entityType.GetProperties())
                Check($"column {entityType.ClrType.Name}.{property.Name}", table is null ? null : property.GetColumnName(table.Value));

            foreach (var key in entityType.GetKeys())
                Check($"key of {entityType.ClrType.Name}", key.GetName());

            foreach (var fk in entityType.GetForeignKeys())
                Check($"fk of {entityType.ClrType.Name}", fk.GetConstraintName());

            foreach (var index in entityType.GetIndexes())
                Check($"index of {entityType.ClrType.Name}", index.GetDatabaseName());
        }

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Places_every_table_in_the_schema_of_the_module_owning_its_data_When_model_is_built()
    {
        var violations = new List<string>();

        foreach (var entityType in BuildModel().GetEntityTypes())
        {
            // Owned types share their owner's table, which is checked through the owner
            if (entityType.IsOwned())
                continue;

            // Module entities use the module's schema ("HrastERP.Administration" -> "administration");
            // everything else uses infrastructure unless its data belongs to a module
            var clrType = entityType.ClrType;
            var expected = ModuleAssemblies.Contains(clrType.Assembly)
                ? clrType.Assembly.GetName().Name!.Split('.')[^1].ToLowerInvariant()
                : ModuleOwnedInfrastructureEntities.GetValueOrDefault(clrType, DatabaseSchemas.Infrastructure);

            var schema = entityType.GetSchema();
            if (schema != expected)
                violations.Add($"{clrType.Name}: expected schema '{expected}', got '{schema ?? "<none>"}'");
        }

        violations.Should().BeEmpty();
    }
}
