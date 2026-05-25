using FluentAssertions;
using HrastERP.SharedKernel.Domain;

namespace HrastERP.SharedKernel.Tests.Domain;

public class AuditableAggregateRootTests
{
    private sealed class TestEvent : IDomainEvent;

    private sealed class FakeAuditableAggregate(Guid id) : AuditableAggregateRoot<Guid>(id)
    {
        public void RaiseEvent(IDomainEvent @event) => AddDomainEvent(@event);
    }

    [Fact]
    public void Implements_IAuditable()
    {
        var aggregate = new FakeAuditableAggregate(Guid.NewGuid());

        aggregate.Should().BeAssignableTo<IAuditable>();
    }

    [Fact]
    public void DomainEvents_is_empty_on_new_aggregate()
    {
        var aggregate = new FakeAuditableAggregate(Guid.NewGuid());

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddDomainEvent_adds_event_to_collection()
    {
        var aggregate = new FakeAuditableAggregate(Guid.NewGuid());
        var @event = new TestEvent();

        aggregate.RaiseEvent(@event);

        aggregate.DomainEvents.Should().ContainSingle().Which.Should().Be(@event);
    }

    [Fact]
    public void Audit_properties_have_default_values()
    {
        var aggregate = new FakeAuditableAggregate(Guid.NewGuid());

        aggregate.CreatedAt.Should().Be(default);
        aggregate.CreatedBy.Should().Be(Guid.Empty);
        aggregate.UpdatedAt.Should().BeNull();
        aggregate.UpdatedBy.Should().BeNull();
    }
}
