using System.Reflection;
using FluentAssertions;
using HrastERP.Administration;
using HrastERP.Administration.Application;
using HrastERP.API.Responses;
using HrastERP.Finance;
using HrastERP.Infrastructure.Identity;
using HrastERP.Inventory;
using HrastERP.Procurement;
using HrastERP.Production;
using HrastERP.SharedKernel.Results;

namespace HrastERP.API.Tests.Conventions;

// Every error code is defined once, in the catalog (a static *Errors class) of the code that owns it.
// Other catalogs may reuse a GeneralErrors error (e.g. ApiErrors.RouteIdMismatch is a General.Validation).
public class ErrorCatalogTests
{
    private static readonly Assembly[] Assemblies =
    [
        typeof(GeneralErrors).Assembly, typeof(AuthErrors).Assembly, typeof(ApiErrors).Assembly,
        typeof(AdministrationModule).Assembly, typeof(FinanceModule).Assembly, typeof(InventoryModule).Assembly,
        typeof(ProcurementModule).Assembly, typeof(ProductionModule).Assembly
    ];

    private sealed record CatalogError(Type Catalog, string Member, Error Error);

    // Reads every static Error field, property and factory method of every *Errors class. Factories are called
    // with default arguments (0, null), which is enough to read their code, message and type.
    private static List<CatalogError> CatalogErrors()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var catalogs = Assemblies.Distinct().SelectMany(a => a.GetTypes())
            .Where(t => t.IsAbstract && t.IsSealed && t.Name.EndsWith("Errors", StringComparison.Ordinal));

        var errors = new List<CatalogError>();
        foreach (var catalog in catalogs)
        {
            foreach (var field in catalog.GetFields(flags).Where(f => f.FieldType == typeof(Error)))
                errors.Add(new CatalogError(catalog, field.Name, (Error)field.GetValue(null)!));

            foreach (var method in catalog.GetMethods(flags).Where(m => m.ReturnType == typeof(Error) && !m.IsGenericMethod))
            {
                var args = method.GetParameters()
                    .Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
                    .ToArray();
                errors.Add(new CatalogError(catalog, method.Name.Replace("get_", ""), (Error)method.Invoke(null, args)!));
            }
        }

        return errors;
    }

    [Fact]
    public void Finds_errors_in_module_infrastructure_and_api_catalogs_When_scanning()
    {
        var catalogs = CatalogErrors().Select(e => e.Catalog).ToHashSet();

        catalogs.Should().Contain([typeof(GeneralErrors), typeof(AuthErrors), typeof(ApiErrors), typeof(AdministrationErrors)]);
    }

    [Fact]
    public void Defines_each_error_code_once_When_catalogs_are_scanned()
    {
        // Arrange: collect all catalog errors and the GeneralErrors other catalogs may reuse
        var errors = CatalogErrors();
        var general = errors
            .Where(e => e.Catalog == typeof(GeneralErrors))
            .Select(e => (e.Error.Code, e.Error.Message, e.Error.Type))
            .ToHashSet();

        // Act: group the definitions by code, ignoring reuses of a GeneralErrors error
        var duplicates = errors
            .Where(e => e.Catalog == typeof(GeneralErrors) || !general.Contains((e.Error.Code, e.Error.Message, e.Error.Type)))
            .GroupBy(e => e.Error.Code)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(e => $"{e.Catalog.Name}.{e.Member}"))}");

        // Assert
        duplicates.Should().BeEmpty();
    }
}
