using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer. Every change to a ticket goes through one of its
/// methods, so its rules cannot be bypassed.
/// </summary>
/// <remarks>
/// The customer, the category and the assigned agent are other aggregates and are referenced
/// by id only.
/// </remarks>
public sealed class Ticket : AggregateRoot
{
    private readonly List<TicketEscalation> _escalations = [];

    private Ticket()
    {
        // For EF Core.
    }

    /// <summary>Human-friendly identifier shown to customers, e.g. TCK-0042.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>Null while the ticket is waiting for an owner.</summary>
    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>When the team has committed to responding by. Null when no SLA applies.</summary>
    public DateTime? DueAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>
    /// When the current SLA window began: creation, or the latest escalation. The at-risk
    /// threshold is measured against the window from this moment to the due date. Null for
    /// tickets that predate it; use <see cref="CreatedAtUtc"/> then.
    /// </summary>
    public DateTime? SlaStartedAtUtc { get; private set; }

    public IReadOnlyCollection<TicketEscalation> Escalations => _escalations;

    /// <summary>True while the ticket still needs work from an agent.</summary>
    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>
    /// Raises a new ticket for a customer. It starts as <see cref="TicketStatus.New"/>, with no
    /// owner and no due date.
    /// </summary>
    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Moves the ticket through its lifecycle, keeping the resolution timestamp in step.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is closed.</exception>
    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Hands the ticket to an agent, or takes it back off them when <paramref name="agentId"/>
    /// is null. Whether the agent may take it is checked by the caller, which can see agents.
    /// </summary>
    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Sets the SLA due date and the moment its window started (BR-2, BR-3).</summary>
    public void StartSla(DateTime dueAtUtc, DateTime startedAtUtc)
    {
        DueAtUtc = dueAtUtc;
        SlaStartedAtUtc = startedAtUtc;
    }

    /// <summary>Why this ticket cannot be escalated right now, or null when it can.</summary>
    public string? EscalationBlockedReason =>
        Priority == TicketPriority.Critical
            ? $"Ticket {Reference} is already Critical and cannot be escalated further."
        : !IsOpen
            ? $"Ticket {Reference} is {Status} and cannot be escalated."
        : null;

    public bool CanEscalate => EscalationBlockedReason is null;

    /// <summary>
    /// Raises the priority one level and records it (BR-7). The caller works out the new due
    /// date and owner with the SLA and assignment rules, because those need data this
    /// aggregate does not hold; this method guards the ticket's own rules and keeps the
    /// history row and the ticket in step.
    /// </summary>
    public TicketEscalation Escalate(
        string reason,
        string escalatedBy,
        DateTime newDueAtUtc,
        int? newAgentId,
        DateTime nowUtc)
    {
        var trimmedReason = reason?.Trim() ?? string.Empty;
        var trimmedBy = escalatedBy?.Trim() ?? string.Empty;

        if (trimmedReason.Length is < TicketEscalation.ReasonMinLength or > TicketEscalation.ReasonMaxLength)
        {
            throw new ArgumentException(
                $"The reason must be {TicketEscalation.ReasonMinLength} to {TicketEscalation.ReasonMaxLength} characters.",
                nameof(reason));
        }

        if (trimmedBy.Length is 0 or > TicketEscalation.EscalatedByMaxLength)
        {
            throw new ArgumentException(
                $"Who escalated the ticket is required and at most {TicketEscalation.EscalatedByMaxLength} characters.",
                nameof(escalatedBy));
        }

        if (EscalationBlockedReason is { } blocked)
        {
            throw new BusinessRuleViolationException(blocked);
        }

        var escalation = new TicketEscalation(
            Priority,
            (TicketPriority)((int)Priority + 1),
            AssignedAgentId,
            newAgentId,
            DueAtUtc,
            newDueAtUtc,
            trimmedReason,
            trimmedBy,
            nowUtc);

        Priority = escalation.ToPriority;
        AssignedAgentId = newAgentId;
        DueAtUtc = newDueAtUtc;
        SlaStartedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        _escalations.Add(escalation);

        return escalation;
    }
}
