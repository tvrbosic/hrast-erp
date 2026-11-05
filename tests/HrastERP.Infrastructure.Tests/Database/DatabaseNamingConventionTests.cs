using System.Text.RegularExpressions;
using FluentAssertions;
using HrastERP.Administration;
using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Database.Audit;
using HrastERP.Infrastructure.FileStorage;
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

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<HrastDbContext>().UseNpgsql("Host=unused").Options;
        var tenant = Substitute.For<ICurrentTenant>();
        using var context = new HrastDbContext(
            options,
            [new EntityConfigurationAssembly(typeof(AdministrationModule).Assembly)],
            tenant);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Theory]
    [InlineData(typeof(Tenant), "administration", "tenants")]
    [InlineData(typeof(RefreshToken), null, "refresh_tokens")]
    [InlineData(typeof(Role), null, "roles")]
    [InlineData(typeof(AuditLogEntry), null, "audit_logs")]
    [InlineData(typeof(StoredFile), null, "stored_files")]
    [InlineData(typeof(ApplicationUser), null, "asp_net_users")]
    public void Maps_entity_to_plural_snake_case_table_When_model_is_built(Type clrType, string? schema, string table)
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
    public void Maps_identity_helper_tables_to_snake_case_When_model_is_built()
    {
        var model = BuildModel();

        model.FindEntityType(typeof(IdentityUserClaim<Guid>))!.GetTableName().Should().Be("asp_net_user_claims");
        model.FindEntityType(typeof(IdentityUserLogin<Guid>))!.GetTableName().Should().Be("asp_net_user_logins");
        model.FindEntityType(typeof(IdentityUserToken<Guid>))!.GetTableName().Should().Be("asp_net_user_tokens");
    }
}
