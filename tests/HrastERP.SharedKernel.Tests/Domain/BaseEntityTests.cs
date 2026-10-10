using FluentAssertions;
using HrastERP.SharedKernel.Domain;

namespace HrastERP.SharedKernel.Tests.Domain;

public class BaseEntityTests
{
    private sealed class FakeEntity(Guid id) : BaseEntity<Guid>(id);

    [Fact]
    public void Is_equal_When_ids_match()
    {
        var id = Guid.NewGuid();
        var a = new FakeEntity(id);
        var b = new FakeEntity(id);

        a.Should().Be(b);
    }

    [Fact]
    public void Is_not_equal_When_ids_differ()
    {
        var a = new FakeEntity(Guid.NewGuid());
        var b = new FakeEntity(Guid.NewGuid());

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_operator_returns_true_When_ids_match()
    {
        var id = Guid.NewGuid();
        var a = new FakeEntity(id);
        var b = new FakeEntity(id);

        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Inequality_operator_returns_true_When_ids_differ()
    {
        var a = new FakeEntity(Guid.NewGuid());
        var b = new FakeEntity(Guid.NewGuid());

        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Exposes_id_When_set_via_constructor()
    {
        var id = Guid.NewGuid();
        var entity = new FakeEntity(id);

        entity.Id.Should().Be(id);
    }

    [Fact]
    public void Implements_IAuditable_When_defined()
    {
        var entity = new FakeEntity(Guid.NewGuid());

        entity.Should().BeAssignableTo<IAuditable>();
    }

    [Fact]
    public void Implements_ISoftDeletable_When_defined()
    {
        var entity = new FakeEntity(Guid.NewGuid());

        entity.Should().BeAssignableTo<ISoftDeletable>();
    }

    [Fact]
    public void Has_default_audit_property_values_When_created()
    {
        var entity = new FakeEntity(Guid.NewGuid());

        entity.CreatedAt.Should().Be(default);
        entity.CreatedBy.Should().Be(Guid.Empty);
        entity.UpdatedAt.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void Has_null_soft_delete_properties_When_created()
    {
        var entity = new FakeEntity(Guid.NewGuid());

        entity.DeletedAt.Should().BeNull();
        entity.DeletedBy.Should().BeNull();
    }
}
