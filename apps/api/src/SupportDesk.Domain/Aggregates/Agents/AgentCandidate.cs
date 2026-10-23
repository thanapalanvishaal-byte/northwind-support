using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>An agent as the assignment rules see them: capacity, status and specializations.</summary>
public sealed record AgentCandidate(
    int AgentId,
    string Name,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlySet<int> SpecializationCategoryIds)
{
    /// <summary>
    /// Active, strictly below their limit, and specialised when the category requires it
    /// (BR-4, BR-5). <paramref name="ownOpenTickets"/> is 1 when the ticket being placed is
    /// already counted in <see cref="OpenTicketCount"/>, so an agent is not blocked by the very
    /// ticket we are deciding about.
    /// </summary>
    public bool IsEligibleFor(Category category, int ownOpenTickets = 0) =>
        IsActive
        && OpenTicketCount - ownOpenTickets < MaxOpenTickets
        && (!category.RequiresSpecialist || SpecializationCategoryIds.Contains(category.Id));
}
