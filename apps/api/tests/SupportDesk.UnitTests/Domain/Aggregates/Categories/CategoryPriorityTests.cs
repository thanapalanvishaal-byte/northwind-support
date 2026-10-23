using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Domain.Aggregates.Categories;

public class CategoryPriorityTests
{
    private static readonly Category Plain = new("General", requiresSpecialist: false, forcesCriticalPriority: false);
    private static readonly Category Forcing = new("Outage", requiresSpecialist: true, forcesCriticalPriority: true);

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    public void ACategoryThatForcesCritical_AlwaysWins(TicketPriority requested) =>
        Assert.Equal(TicketPriority.Critical, Forcing.ResolvePriority(requested));

    [Fact]
    public void ACategoryThatForcesCritical_WinsEvenWithNoRequest() =>
        Assert.Equal(TicketPriority.Critical, Forcing.ResolvePriority(null));

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.High)]
    public void OtherCategories_UseTheRequestedPriority(TicketPriority requested) =>
        Assert.Equal(requested, Plain.ResolvePriority(requested));

    [Fact]
    public void WithoutARequest_ThePriorityDefaultsToMedium() =>
        Assert.Equal(TicketPriority.Medium, Plain.ResolvePriority(null));
}
