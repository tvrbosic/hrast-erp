using FluentAssertions;
using HrastERP.Infrastructure.Identity;

namespace HrastERP.Infrastructure.Tests.Identity;

public class TenantTests
{
    [Fact]
    public void Trims_name_and_sets_normalized_name_When_created()
    {
        var tenant = new Tenant(Guid.NewGuid(), "  Acme d.o.o. ");

        tenant.Name.Should().Be("Acme d.o.o.");
        tenant.NormalizedName.Should().Be("ACME D.O.O.");
    }

    [Fact]
    public void Updates_normalized_name_When_renamed()
    {
        var tenant = new Tenant(Guid.NewGuid(), "Acme");

        tenant.UpdateName(" čelik ");

        tenant.Name.Should().Be("čelik");
        tenant.NormalizedName.Should().Be("ČELIK");
    }
}
