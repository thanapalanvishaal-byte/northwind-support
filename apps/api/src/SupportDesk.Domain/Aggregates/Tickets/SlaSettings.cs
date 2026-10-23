namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// The tunable numbers behind the SLA rules (BR-2, BR-3, BR-6). They are plain values so the
/// domain stays free of any configuration framework; the application layer fills them from the
/// <c>Sla</c> section of the app settings, so changing a window never needs a code change.
/// </summary>
public sealed class SlaSettings
{
    public SlaSettings(
        IReadOnlyDictionary<TicketPriority, double> baseWindowHours,
        double premiumCustomerMultiplier,
        double minimumWindowHours,
        double atRiskThresholdPercent)
    {
        ArgumentNullException.ThrowIfNull(baseWindowHours);

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!baseWindowHours.TryGetValue(priority, out var hours) || hours <= 0)
            {
                throw new ArgumentException(
                    $"A base SLA window greater than zero is required for {priority} priority.",
                    nameof(baseWindowHours));
            }
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(premiumCustomerMultiplier, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(premiumCustomerMultiplier, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumWindowHours);
        ArgumentOutOfRangeException.ThrowIfNegative(atRiskThresholdPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(atRiskThresholdPercent, 100);

        BaseWindowHours = baseWindowHours;
        PremiumCustomerMultiplier = premiumCustomerMultiplier;
        MinimumWindowHours = minimumWindowHours;
        AtRiskThresholdPercent = atRiskThresholdPercent;
    }

    public IReadOnlyDictionary<TicketPriority, double> BaseWindowHours { get; }

    /// <summary>Premium customers get this fraction of the base window (0.5 is half).</summary>
    public double PremiumCustomerMultiplier { get; }

    /// <summary>A premium window is never shorter than this many hours.</summary>
    public double MinimumWindowHours { get; }

    /// <summary>A ticket is at risk once this percentage or less of its window remains.</summary>
    public double AtRiskThresholdPercent { get; }

    /// <summary>The values from the specification, for tests and as documentation.</summary>
    public static SlaSettings Default { get; } = new(
        new Dictionary<TicketPriority, double>
        {
            [TicketPriority.Low] = 72,
            [TicketPriority.Medium] = 24,
            [TicketPriority.High] = 8,
            [TicketPriority.Critical] = 4
        },
        premiumCustomerMultiplier: 0.5,
        minimumWindowHours: 1,
        atRiskThresholdPercent: 25);
}
