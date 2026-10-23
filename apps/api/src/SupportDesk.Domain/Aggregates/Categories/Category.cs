using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Categories;

/// <summary>
/// The kind of problem a ticket is about (Billing, Outage, ...). Categories carry the
/// handling rules for the tickets filed against them, so that those rules live in data
/// rather than in code.
/// </summary>
public sealed class Category : AggregateRoot
{
    private Category()
    {
        // For EF Core.
    }

    public Category(string name, bool requiresSpecialist, bool forcesCriticalPriority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        IsActive = true;
        RequiresSpecialist = requiresSpecialist;
        ForcesCriticalPriority = forcesCriticalPriority;
    }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    /// <summary>
    /// Tickets in this category may only be handled by an agent who lists it as a
    /// specialization.
    /// </summary>
    public bool RequiresSpecialist { get; private set; }

    /// <summary>
    /// Tickets in this category are always treated as the most urgent, whatever the
    /// reporter asked for.
    /// </summary>
    public bool ForcesCriticalPriority { get; private set; }

    /// <summary>
    /// BR-1: a category that forces Critical always wins; otherwise the requested priority, or
    /// Medium when none was given.
    /// </summary>
    public TicketPriority ResolvePriority(TicketPriority? requested) =>
        ForcesCriticalPriority ? TicketPriority.Critical : requested ?? TicketPriority.Medium;
}
