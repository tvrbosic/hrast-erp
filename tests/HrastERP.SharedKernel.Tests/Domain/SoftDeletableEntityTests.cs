using FluentAssertions;
using HrastERP.SharedKernel.Domain;

namespace HrastERP.SharedKernel.Tests.Domain;

public class SoftDeletableEntityTests
{
    private sealed class FakeSoftDeletableEntity(Guid id) : SoftDeletableEntity<Guid>(id);

    [Fact]
    public void Entity_id_is_set_via_constructor()
    {
        var id = Guid.NewGuid();
        var entity = new FakeSoftDeletableEntity(id);

        entity.Id.Should().Be(id);
    }

    [Fact]
    public void Implements_ISoftDeletable()
    {
        var entity = new FakeSoftDeletableEntity(Guid.NewGuid());

        entity.Should().BeAssignableTo<ISoftDeletable>();
    }

    [Fact]
    public void SoftDelete_properties_have_default_null_values()
    {
        var entity = new FakeSoftDeletableEntity(Guid.NewGuid());

        entity.DeletedAt.Should().BeNull();
        entity.DeletedBy.Should().BeNull();
    }

    [Fact]
    public void Entities_with_same_id_should_be_equal()
    {
        var id = Guid.NewGuid();
        var a = new FakeSoftDeletableEntity(id);
        var b = new FakeSoftDeletableEntity(id);

        a.Should().Be(b);
    }

    [Fact]
    public void Entities_with_different_ids_should_not_be_equal()
    {
        var a = new FakeSoftDeletableEntity(Guid.NewGuid());
        var b = new FakeSoftDeletableEntity(Guid.NewGuid());

        a.Should().NotBe(b);
    }
}
