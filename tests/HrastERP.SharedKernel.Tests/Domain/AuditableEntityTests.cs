using FluentAssertions;
using HrastERP.SharedKernel.Domain;

namespace HrastERP.SharedKernel.Tests.Domain;

public class AuditableEntityTests
{
    private sealed class FakeAuditableEntity(Guid id) : AuditableEntity<Guid>(id);

    [Fact]
    public void Entity_id_is_set_via_constructor()
    {
        var id = Guid.NewGuid();
        var entity = new FakeAuditableEntity(id);

        entity.Id.Should().Be(id);
    }

    [Fact]
    public void Entities_with_same_id_should_be_equal()
    {
        var id = Guid.NewGuid();
        var a = new FakeAuditableEntity(id);
        var b = new FakeAuditableEntity(id);

        a.Should().Be(b);
    }

    [Fact]
    public void Entities_with_different_ids_should_not_be_equal()
    {
        var a = new FakeAuditableEntity(Guid.NewGuid());
        var b = new FakeAuditableEntity(Guid.NewGuid());

        a.Should().NotBe(b);
    }

    [Fact]
    public void Implements_IAuditable()
    {
        var entity = new FakeAuditableEntity(Guid.NewGuid());

        entity.Should().BeAssignableTo<IAuditable>();
    }

    [Fact]
    public void Audit_properties_have_default_values()
    {
        var entity = new FakeAuditableEntity(Guid.NewGuid());

        entity.CreatedAt.Should().Be(default);
        entity.CreatedBy.Should().Be(Guid.Empty);
        entity.UpdatedAt.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }
}
