using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketEscalationTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;
    private static readonly DateTime EscalatedAt = Now.AddHours(3);
    private static readonly DateTime NewDue = EscalatedAt.AddHours(8);

    private static Ticket Open(TicketPriority priority, int? agentId = 1)
    {
        var ticket = new TicketBuilder().WithPriority(priority).AssignedTo(agentId).Build();
        ticket.StartSla(Now.AddHours(24), Now);

        return ticket;
    }

    [Fact]
    public void Escalate_RaisesThePriorityOneLevel()
    {
        var ticket = Open(TicketPriority.Medium);

        ticket.Escalate("Customer is blocked", "alex@support.test", NewDue, 1, EscalatedAt);

        Assert.Equal(TicketPriority.High, ticket.Priority);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalate_MovesExactlyOneStep(TicketPriority from, TicketPriority to)
    {
        var ticket = Open(from);

        var row = ticket.Escalate("Needs attention now", "lead", NewDue, 1, EscalatedAt);

        Assert.Equal(from, row.FromPriority);
        Assert.Equal(to, row.ToPriority);
    }

    [Fact]
    public void Escalate_SetsTheNewDueDate_AndRestartsTheSlaWindowFromNow()
    {
        var ticket = Open(TicketPriority.Medium);

        ticket.Escalate("Customer is blocked", "lead", NewDue, 1, EscalatedAt);

        Assert.Equal(NewDue, ticket.DueAtUtc);
        Assert.Equal(EscalatedAt, ticket.SlaStartedAtUtc);
        Assert.Equal(EscalatedAt, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Escalate_RecordsTheBeforeAndAfter_InAHistoryRow()
    {
        var ticket = Open(TicketPriority.Medium, agentId: 1);
        var oldDue = ticket.DueAtUtc;

        var row = ticket.Escalate("  Customer is blocked  ", "  lead@support.test ", NewDue, newAgentId: 2, EscalatedAt);

        Assert.Equal(1, row.FromAgentId);
        Assert.Equal(2, row.ToAgentId);
        Assert.Equal(oldDue, row.FromDueAtUtc);
        Assert.Equal(NewDue, row.ToDueAtUtc);
        Assert.Equal("Customer is blocked", row.Reason);
        Assert.Equal("lead@support.test", row.EscalatedBy);
        Assert.Equal(EscalatedAt, row.EscalatedAtUtc);
        Assert.Same(row, Assert.Single(ticket.Escalations));
        Assert.Equal(2, ticket.AssignedAgentId);
    }

    [Fact]
    public void Escalate_CanLeaveTheTicketUnassigned()
    {
        var ticket = Open(TicketPriority.Medium, agentId: 1);

        var row = ticket.Escalate("Customer is blocked", "lead", NewDue, newAgentId: null, EscalatedAt);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(row.ToAgentId);
    }

    [Fact]
    public void ACriticalTicket_CannotBeEscalated()
    {
        var ticket = Open(TicketPriority.Critical);

        var error = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate("Customer is blocked", "lead", NewDue, 1, EscalatedAt));

        Assert.Contains("Critical", error.Message);
        Assert.False(ticket.CanEscalate);
        Assert.Empty(ticket.Escalations);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void AResolvedOrClosedTicket_CannotBeEscalated(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).WithStatus(status).Build();

        var error = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate("Customer is blocked", "lead", NewDue, 1, EscalatedAt));

        Assert.Contains(status.ToString(), error.Message);
        Assert.Equal(TicketPriority.Low, ticket.Priority);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcd")]
    public void ATooShortReason_IsRejected(string reason)
    {
        var ticket = Open(TicketPriority.Low);

        Assert.Throws<ArgumentException>(() => ticket.Escalate(reason, "lead", NewDue, 1, EscalatedAt));
        Assert.Equal(TicketPriority.Low, ticket.Priority);
    }

    [Fact]
    public void TheReasonLimits_AreInclusive()
    {
        Assert.Equal(
            TicketPriority.Medium,
            Open(TicketPriority.Low).Escalate(new string('a', 5), "lead", NewDue, 1, EscalatedAt).ToPriority);
        Assert.Equal(
            TicketPriority.Medium,
            Open(TicketPriority.Low).Escalate(new string('a', 500), "lead", NewDue, 1, EscalatedAt).ToPriority);
        Assert.Throws<ArgumentException>(
            () => Open(TicketPriority.Low).Escalate(new string('a', 501), "lead", NewDue, 1, EscalatedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingActor_IsRejected(string actor)
    {
        var ticket = Open(TicketPriority.Low);

        Assert.Throws<ArgumentException>(() => ticket.Escalate("Customer is blocked", actor, NewDue, 1, EscalatedAt));
    }
}
