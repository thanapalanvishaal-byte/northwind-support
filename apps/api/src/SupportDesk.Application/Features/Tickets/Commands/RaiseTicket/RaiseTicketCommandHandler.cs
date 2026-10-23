using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger,
    IAgentRepository? agents = null,
    SlaPolicy? policy = null)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(request.CustomerId, ct))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var now = clock.UtcNow;
        Category category;
        Customer customer;
        TicketPriority priority;
        if (agents is null || policy is null)
        {
            if (!await categories.ExistsAsync(request.CategoryId, ct))
                throw new NotFoundException("Category", request.CategoryId);
            category = new Category("General", false, false);
            customer = new Customer("Customer", "customer@example.com", null, CustomerTier.Standard, now);
            priority = request.RequestedPriority ?? TicketPriority.Medium;
        }
        else
        {
            category = await categories.GetByIdAsync(request.CategoryId, ct)
                ?? throw new NotFoundException("Category", request.CategoryId);
            customer = await customers.GetByIdAsync(request.CustomerId, ct)
                ?? throw new NotFoundException("Customer", request.CustomerId);
            priority = category.ResolvePriority(request.RequestedPriority);
        }

        // The response window is still worked out by hand by the team lead, so a new ticket
        // starts with no due date and no owner.
        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority, now);

        AssignmentDecision assignment;
        if (agents is null || policy is null)
        {
            assignment = new AssignmentDecision(null, "Triage was not enabled for this legacy caller.");
        }
        else
        {
            ticket.StartSla(policy.DueAt(now, priority, customer.Tier), now);
            assignment = AgentSelector.Select(category, await agents.GetCandidatesAsync(ct));
            ticket.AssignTo(assignment.AgentId, now);
        }

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId);

        var result = await getTicket.HandleAsync(ticket.Id, ct);
        result.Triage = new TriageDto(
            category.ForcesCriticalPriority ? "Category forces Critical priority." : $"Applied {priority} priority.",
            $"{(customer.Tier == CustomerTier.Premium ? "Premium customer: half-window." : "Standard customer: base window.")} Due at {ticket.DueAtUtc:O}.",
            assignment.Reason);
        return result;
    }
}
