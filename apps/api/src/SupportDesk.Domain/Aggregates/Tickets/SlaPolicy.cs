using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// How long a ticket may stay unresolved (BR-2, BR-3). Used at creation and again when a
/// ticket is escalated, so both paths always agree.
/// </summary>
public sealed class SlaPolicy(SlaSettings settings)
{
    public TimeSpan WindowFor(TicketPriority priority, CustomerTier tier)
    {
        var hours = settings.BaseWindowHours[priority];

        if (tier == CustomerTier.Premium)
        {
            hours = Math.Max(hours * settings.PremiumCustomerMultiplier, settings.MinimumWindowHours);
        }

        return TimeSpan.FromHours(hours);
    }

    public DateTime DueAt(DateTime startUtc, TicketPriority priority, CustomerTier tier) =>
        startUtc + WindowFor(priority, tier);
}
