using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    // The window is 24 hours, so the 25% threshold is the last 6 hours.
    private static readonly DateTime Due = Now.AddHours(24);

    [Fact]
    public void AtRisk_BeginsExactlyWhenAQuarterOfTheWindowRemains() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Due, null, Due.AddHours(-6), Now, 25));

    [Fact]
    public void JustBeforeTheThreshold_TheTicketIsStillWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Due, null, Due.AddHours(-6).AddMinutes(-1), Now, 25));

    [Fact]
    public void AtTheDueInstant_TheTicketIsAtRiskNotYetBreached() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Due, null, Due, Now, 25));

    [Fact]
    public void OneMinutePastDue_TheTicketIsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Due, null, Due.AddMinutes(1), Now, 25));

    [Fact]
    public void AResolvedTicket_IsNeverAtRisk() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Due, Due.AddHours(-1), Due.AddHours(-1), Now, 25));

    [Fact]
    public void WithoutADueDate_TheThresholdDoesNotMatter() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now, Now, 25));

    [Fact]
    public void TheThreshold_IsMeasuredFromTheWindowStart_NotFromCreation()
    {
        // Escalated 20 hours in: the new 8 hour window runs from there, so 2 hours left is 25%.
        var escalatedAt = Now.AddHours(20);
        var newDue = escalatedAt.AddHours(8);

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(newDue, null, newDue.AddHours(-2), escalatedAt, 25));
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(newDue, null, newDue.AddHours(-3), escalatedAt, 25));
    }
}
