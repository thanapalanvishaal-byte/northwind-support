using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public class AgentSelectorTests
{
    private const int GeneralId = 1;
    private const int BillingId = 2;

    private static readonly Category General =
        new Category("General", requiresSpecialist: false, forcesCriticalPriority: false).WithId(GeneralId);

    private static readonly Category Billing =
        new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false).WithId(BillingId);

    private static AgentCandidate Candidate(
        int id,
        int open,
        int max = 10,
        bool active = true,
        int[]? specializations = null) =>
        new(id, $"Agent {id}", active, max, open, (specializations ?? []).ToHashSet());

    [Fact]
    public void ChoosesTheEligibleAgentWithTheFewestOpenTickets()
    {
        var decision = AgentSelector.Select(General, [Candidate(1, open: 5), Candidate(2, open: 2), Candidate(3, open: 4)]);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Ties_GoToTheLowestAgentId()
    {
        var decision = AgentSelector.Select(General, [Candidate(7, open: 3), Candidate(4, open: 3), Candidate(9, open: 3)]);

        Assert.Equal(4, decision.AgentId);
    }

    [Fact]
    public void InactiveAgents_AreNeverChosen()
    {
        var decision = AgentSelector.Select(General, [Candidate(1, open: 0, active: false), Candidate(2, open: 6)]);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void AnAgentAtTheirLimit_IsSkipped_ButOneBelowItIsNot()
    {
        var atLimit = AgentSelector.Select(General, [Candidate(1, open: 6, max: 6)]);
        var belowLimit = AgentSelector.Select(General, [Candidate(1, open: 5, max: 6)]);

        Assert.Null(atLimit.AgentId);
        Assert.Equal(1, belowLimit.AgentId);
    }

    [Fact]
    public void WhenTheCategoryRequiresASpecialist_OnlySpecialistsAreEligible()
    {
        var decision = AgentSelector.Select(
            Billing,
            [Candidate(1, open: 0), Candidate(2, open: 7, specializations: [BillingId])]);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void WhenNoSpecialistIsRequired_AnyEligibleAgentWillDo()
    {
        var decision = AgentSelector.Select(
            General,
            [Candidate(1, open: 1), Candidate(2, open: 3, specializations: [GeneralId])]);

        Assert.Equal(1, decision.AgentId);
    }

    [Fact]
    public void WhenNobodyIsEligible_TheTicketIsLeftUnassigned_WithAReason()
    {
        var decision = AgentSelector.Select(
            Billing,
            [Candidate(1, open: 0), Candidate(2, open: 6, max: 6, specializations: [BillingId])]);

        Assert.Null(decision.AgentId);
        Assert.Contains("Billing", decision.Reason);
    }

    [Fact]
    public void NoCandidatesAtAll_IsAlsoUnassigned()
    {
        var decision = AgentSelector.Select(General, []);

        Assert.Null(decision.AgentId);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void OnEscalation_TheCurrentAgentIsKept_WhileStillEligible()
    {
        var decision = AgentSelector.Select(
            General,
            [Candidate(1, open: 5), Candidate(2, open: 0)],
            currentAgentId: 1);

        Assert.Equal(1, decision.AgentId);
    }

    [Fact]
    public void OnEscalation_TheCurrentAgentIsNotBlockedByTheTicketBeingEscalated()
    {
        // 6 open of 6 includes this very ticket, so without it they are at 5 of 6.
        var decision = AgentSelector.Select(
            General,
            [Candidate(1, open: 6, max: 6), Candidate(2, open: 0)],
            currentAgentId: 1);

        Assert.Equal(1, decision.AgentId);
    }

    [Fact]
    public void OnEscalation_ACurrentAgentWhoIsNoLongerEligible_IsReplaced()
    {
        var decision = AgentSelector.Select(
            General,
            [Candidate(1, open: 2, active: false), Candidate(2, open: 4), Candidate(3, open: 3)],
            currentAgentId: 1);

        Assert.Equal(3, decision.AgentId);
    }

    [Fact]
    public void OnEscalation_ACurrentAgentWithoutTheSpecializationIsReplaced()
    {
        var decision = AgentSelector.Select(
            Billing,
            [Candidate(1, open: 1), Candidate(2, open: 4, specializations: [BillingId])],
            currentAgentId: 1);

        Assert.Equal(2, decision.AgentId);
    }
}
