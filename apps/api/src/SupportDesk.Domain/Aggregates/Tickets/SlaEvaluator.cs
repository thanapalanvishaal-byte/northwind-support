namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc) =>
        dueAtUtc is null ? SlaStatus.NotApplicable
        : resolvedAtUtc is not null
            ? (resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
        : nowUtc > dueAtUtc ? SlaStatus.Breached
        : SlaStatus.WithinSla;

    /// <summary>
    /// As above, but also reports <see cref="SlaStatus.AtRisk"/>: still open and the share of
    /// the window left (from <paramref name="windowStartUtc"/> to the due date) is at or below
    /// the threshold.
    /// </summary>
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime windowStartUtc,
        double atRiskThresholdPercent)
    {
        var status = Evaluate(dueAtUtc, resolvedAtUtc, nowUtc);

        if (status != SlaStatus.WithinSla)
        {
            return status;
        }

        var window = dueAtUtc!.Value - windowStartUtc;
        var remaining = dueAtUtc.Value - nowUtc;

        return remaining <= window * (atRiskThresholdPercent / 100.0)
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
