using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>The outcome of choosing an owner: who (or nobody) and a reason a person can read.</summary>
public sealed record AssignmentDecision(int? AgentId, string Reason);

/// <summary>
/// Picks the owner of a ticket (BR-4, BR-5, and the owner re-check of BR-7). Pure: it works on
/// the candidates it is given, so it needs no database and is tested on its own.
/// </summary>
public static class AgentSelector
{
    /// <summary>
    /// Chooses among the eligible agents the one with the fewest open tickets; ties go to the
    /// lowest agent id so the result is deterministic. When <paramref name="currentAgentId"/>
    /// is given (an escalation) and that agent is still eligible, they keep the ticket.
    /// </summary>
    public static AssignmentDecision Select(
        Category category,
        IEnumerable<AgentCandidate> candidates,
        int? currentAgentId = null)
    {
        var all = candidates.ToList();

        if (currentAgentId is { } currentId
            && all.FirstOrDefault(c => c.AgentId == currentId) is { } current
            && current.IsEligibleFor(category, ownOpenTickets: 1))
        {
            return new AssignmentDecision(
                current.AgentId,
                $"Kept {current.Name}, who can still handle {category.Name} tickets.");
        }

        var chosen = all
            .Where(c => c.IsEligibleFor(category))
            .OrderBy(c => c.OpenTicketCount)
            .ThenBy(c => c.AgentId)
            .FirstOrDefault();

        if (chosen is not null)
        {
            return new AssignmentDecision(
                chosen.AgentId,
                $"Assigned to {chosen.Name}: fewest open tickets ({chosen.OpenTicketCount}) among eligible agents.");
        }

        return new AssignmentDecision(
            null,
            category.RequiresSpecialist
                ? $"No eligible agent: no active {category.Name} specialist is below their open-ticket limit."
                : "No eligible agent: every active agent is at their open-ticket limit.");
    }
}
