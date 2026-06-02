using FluentAssertions;
using HrastERP.SharedKernel.Domain;

namespace HrastERP.SharedKernel.Tests.Domain;

public class AggregateRootTests
{
    private sealed class TestEvent : IDomainEvent;

    private sealed class FakeAggregate(Guid id) : AggregateRoot<Guid>(id)
    {
        public void RaiseEvent(IDomainEvent @event) => AddDomainEvent(@event);
    }

    [Fact]
    public void DomainEvents_is_empty_on_new_aggregate()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddDomainEvent_adds_event_to_collection()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());
        var @event = new TestEvent();

        aggregate.RaiseEvent(@event);

        aggregate.DomainEvents.Should().ContainSingle().Which.Should().Be(@event);
    }

    [Fact]
    public void ClearDomainEvents_removes_all_events()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());
        aggregate.RaiseEvent(new TestEvent());
        aggregate.RaiseEvent(new TestEvent());

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Multiple_events_can_be_added()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());
        aggregate.RaiseEvent(new TestEvent());
        aggregate.RaiseEvent(new TestEvent());

        aggregate.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    public void Implements_IAuditable()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());

        aggregate.Should().BeAssignableTo<IAuditable>();
    }

    [Fact]
    public void Implements_ISoftDeletable()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());

        aggregate.Should().BeAssignableTo<ISoftDeletable>();
    }
}
