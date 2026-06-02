using FluentAssertions;
using HrastERP.SharedKernel.Domain;

namespace HrastERP.SharedKernel.Tests.Domain;

public class BaseEntityTests
{
    private sealed class FakeEntity(Guid id) : BaseEntity<Guid>(id);

    [Fact]
    public void Entities_with_same_id_should_be_equal()
    {
        var id = Guid.NewGuid();
        var a = new FakeEntity(id);
        var b = new FakeEntity(id);

        a.Should().Be(b);
    }

    [Fact]
    public void Entities_with_different_ids_should_not_be_equal()
    {
        var a = new FakeEntity(Guid.NewGuid());
        var b = new FakeEntity(Guid.NewGuid());

        a.Should().NotBe(b);
    }

    [Fact]
    public void Entity_equality_operator_returns_true_for_same_id()
    {
        var id = Guid.NewGuid();
        var a = new FakeEntity(id);
        var b = new FakeEntity(id);

        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Entity_inequality_operator_returns_true_for_different_ids()
    {
        var a = new FakeEntity(Guid.NewGuid());
        var b = new FakeEntity(Guid.NewGuid());

        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Entity_id_is_set_via_constructor()
    {
        var id = Guid.NewGuid();
        var entity = new FakeEntity(id);

        entity.Id.Should().Be(id);
    }

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
}
