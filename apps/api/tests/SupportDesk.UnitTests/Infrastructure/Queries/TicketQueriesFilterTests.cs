using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Runs the real list query against an in-memory SQLite database, so these tests prove the
/// filtering, the count and the paging happen in SQL and not in C#.
/// </summary>
/// <remarks>
/// SQLite compares text case-sensitively where SQL Server's default collation does not, so
/// search tests use the exact casing of the data.
/// </remarks>
public sealed class TicketQueriesFilterTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;
    private readonly TicketQueries _queries;

    private int _fabrikamId;
    private int _contosoId;
    private int _billingId;
    private int _technicalId;
    private int _alexId;
    private int _saraId;

    public TicketQueriesFilterTests()
    {
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options;
        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();

        Seed();

        _queries = new TicketQueries(_db, new FixedClock());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Priority_IsAnExactMatch()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { Priority = TicketPriority.High }, default);

        Assert.Equal(new[] { "TCK-0002", "TCK-0004" }, Sorted(result));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Filters_CombineWithAnd()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { Status = TicketStatus.Open, CategoryId = _technicalId, AssignedAgentId = _alexId },
            default);

        Assert.Equal(new[] { "TCK-0003" }, Sorted(result));
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_MatchesTheCustomerName_AndIgnoresSurroundingSpaces()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { Search = "  Contoso  " }, default);

        Assert.Equal(new[] { "TCK-0003", "TCK-0004" }, Sorted(result));
    }

    [Fact]
    public async Task Search_MatchesTheReference()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { Search = "TCK-0002" }, default);

        Assert.Equal(new[] { "TCK-0002" }, Sorted(result));
    }

    [Fact]
    public async Task Search_MatchesPartOfTheTitle()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { Search = "Invoice" }, default);

        Assert.Equal(new[] { "TCK-0001" }, Sorted(result));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankSearch_IsIgnored(string? search)
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { Search = search }, default);

        Assert.Equal(4, result.TotalCount);
    }

    [Fact]
    public async Task UnassignedOnly_ReturnsOnlyTicketsWithNoAgent()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { UnassignedOnly = true }, default);

        Assert.Equal(new[] { "TCK-0001" }, Sorted(result));
    }

    [Fact]
    public async Task UnassignedOnly_CombinedWithAnAgent_MatchesNothing()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { UnassignedOnly = true, AssignedAgentId = _alexId },
            default);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task CustomerFilter_ReturnsOnlyThatCustomersTickets()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { CustomerId = _fabrikamId }, default);

        Assert.Equal(new[] { "TCK-0001", "TCK-0002" }, Sorted(result));
    }

    [Fact]
    public async Task TheCountAndPager_DescribeTheFilteredSet_NotEveryTicket()
    {
        // Two of the four tickets belong to Contoso; with a page size of 1 that is two pages.
        var result = await _queries.GetPagedAsync(
            new TicketQuery { CustomerId = _contosoId, Page = 2, PageSize = 1 },
            default);

        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Page);
    }

    private static string[] Sorted(PagedResult<TicketListItemDto> result) =>
        result.Items.Select(i => i.Reference).Order().ToArray();

    private void Seed()
    {
        var now = FixedClock.DefaultNow;

        var fabrikam = new Customer("Fabrikam Inc", "it@fabrikam.test", null, CustomerTier.Standard, now);
        var contoso = new Customer("Contoso Ltd", "it@contoso.test", null, CustomerTier.Premium, now);
        var billing = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        var technical = new Category("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
        var alex = new Agent("Alex Turner", "alex@support.test", 10, now);
        var sara = new Agent("Sara Lindqvist", "sara@support.test", 6, now);

        _db.AddRange(fabrikam, contoso, billing, technical, alex, sara);
        _db.SaveChanges();

        _fabrikamId = fabrikam.Id;
        _contosoId = contoso.Id;
        _billingId = billing.Id;
        _technicalId = technical.Id;
        _alexId = alex.Id;
        _saraId = sara.Id;

        _db.AddRange(
            NewTicket("TCK-0001", "Invoice total does not match", _fabrikamId, _billingId, TicketPriority.Low, TicketStatus.New, null),
            NewTicket("TCK-0002", "Cannot export report", _fabrikamId, _technicalId, TicketPriority.High, TicketStatus.InProgress, _saraId),
            NewTicket("TCK-0003", "Login page is slow", _contosoId, _technicalId, TicketPriority.Medium, TicketStatus.Open, _alexId),
            NewTicket("TCK-0004", "Duplicate charge on statement", _contosoId, _billingId, TicketPriority.High, TicketStatus.Resolved, _saraId));
        _db.SaveChanges();
    }

    private static Ticket NewTicket(
        string reference,
        string title,
        int customerId,
        int categoryId,
        TicketPriority priority,
        TicketStatus status,
        int? agentId)
    {
        var now = FixedClock.DefaultNow;
        var ticket = Ticket.Raise(
            reference, title, "A description long enough to be realistic.", customerId, categoryId, priority, now);

        if (agentId is not null)
        {
            ticket.AssignTo(agentId, now);
        }

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, now);
        }

        return ticket;
    }
}
