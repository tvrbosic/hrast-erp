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
    public void Has_empty_domain_events_When_first_created()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Contains_event_When_domain_event_is_added()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());
        var @event = new TestEvent();

        aggregate.RaiseEvent(@event);

        aggregate.DomainEvents.Should().ContainSingle().Which.Should().Be(@event);
    }

    [Fact]
    public void Has_empty_domain_events_When_cleared()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());
        aggregate.RaiseEvent(new TestEvent());
        aggregate.RaiseEvent(new TestEvent());

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Has_correct_event_count_When_multiple_events_are_added()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());
        aggregate.RaiseEvent(new TestEvent());
        aggregate.RaiseEvent(new TestEvent());

        aggregate.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    public void Implements_IAuditable_When_defined()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());

        aggregate.Should().BeAssignableTo<IAuditable>();
    }

    [Fact]
    public void Implements_ISoftDeletable_When_defined()
    {
        var aggregate = new FakeAggregate(Guid.NewGuid());

        aggregate.Should().BeAssignableTo<ISoftDeletable>();
    }
}
