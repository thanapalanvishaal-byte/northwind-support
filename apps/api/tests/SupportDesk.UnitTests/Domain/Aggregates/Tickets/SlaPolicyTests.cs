using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaPolicyTests
{
    private static readonly SlaPolicy Policy = new(SlaSettings.Default);

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void StandardCustomers_GetTheBaseWindow(TicketPriority priority, double hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), Policy.WindowFor(priority, CustomerTier.Standard));

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Low, 36)]
    public void PremiumCustomers_GetHalfTheBaseWindow(TicketPriority priority, double hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), Policy.WindowFor(priority, CustomerTier.Premium));

    [Fact]
    public void PremiumWindows_NeverDropBelowTheMinimum()
    {
        // A tenth of the base window would be 24 minutes for Critical; the floor holds it at 1 hour.
        var policy = new SlaPolicy(new SlaSettings(
            SlaSettings.Default.BaseWindowHours,
            premiumCustomerMultiplier: 0.1,
            minimumWindowHours: 1,
            atRiskThresholdPercent: 25));

        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
        Assert.Equal(TimeSpan.FromHours(2.4), policy.WindowFor(TicketPriority.Medium, CustomerTier.Premium));
    }

    [Fact]
    public void TheWindowsComeFromTheSettings_NotFromTheCode()
    {
        var policy = new SlaPolicy(new SlaSettings(
            new Dictionary<TicketPriority, double>
            {
                [TicketPriority.Low] = 100,
                [TicketPriority.Medium] = 50,
                [TicketPriority.High] = 20,
                [TicketPriority.Critical] = 10
            },
            premiumCustomerMultiplier: 0.25,
            minimumWindowHours: 1,
            atRiskThresholdPercent: 10));

        Assert.Equal(TimeSpan.FromHours(10), policy.WindowFor(TicketPriority.Critical, CustomerTier.Standard));
        Assert.Equal(TimeSpan.FromHours(2.5), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void DueAt_IsTheStartPlusTheWindow() =>
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(8),
            Policy.DueAt(FixedClock.DefaultNow, TicketPriority.High, CustomerTier.Standard));

    [Fact]
    public void Settings_RequireAWindowForEveryPriority()
    {
        var incomplete = new Dictionary<TicketPriority, double> { [TicketPriority.Low] = 72 };

        Assert.Throws<ArgumentException>(() => new SlaSettings(incomplete, 0.5, 1, 25));
    }
}
