using FluentAssertions;
using HrastERP.SharedKernel.Domain;

namespace HrastERP.SharedKernel.Tests.Domain;

public class AuditableSoftDeletableEntityTests
{
    private sealed class FakeEntity(Guid id) : AuditableSoftDeletableEntity<Guid>(id);

    [Fact]
    public void Implements_IAuditable()
    {
        var entity = new FakeEntity(Guid.NewGuid());

        entity.Should().BeAssignableTo<IAuditable>();
    }

    [Fact]
    public void Implements_ISoftDeletable()
    {
        var entity = new FakeEntity(Guid.NewGuid());

        entity.Should().BeAssignableTo<ISoftDeletable>();
    }

    [Fact]
    public void Audit_properties_have_default_values()
    {
        var entity = new FakeEntity(Guid.NewGuid());

        entity.CreatedAt.Should().Be(default);
        entity.CreatedBy.Should().Be(Guid.Empty);
        entity.UpdatedAt.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void SoftDelete_properties_have_default_null_values()
    {
        var entity = new FakeEntity(Guid.NewGuid());

        entity.DeletedAt.Should().BeNull();
        entity.DeletedBy.Should().BeNull();
    }

    [Fact]
    public void Entity_id_is_set_via_constructor()
    {
        var id = Guid.NewGuid();
        var entity = new FakeEntity(id);

        entity.Id.Should().Be(id);
    }
}
