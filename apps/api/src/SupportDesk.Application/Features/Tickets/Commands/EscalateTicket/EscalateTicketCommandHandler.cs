using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICategoryRepository categories,
    ICustomerRepository customers,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    SlaPolicy policy,
    GetTicketQueryHandler getTicket)
{
    public async Task<TicketDetailDto> HandleAsync(int id, EscalateTicketRequest request, string escalatedBy, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);
        if (ticket.EscalationBlockedReason is { } blocked)
            throw new ConflictException(blocked);

        var reason = request.Reason?.Trim() ?? string.Empty;
        var actor = escalatedBy?.Trim() ?? string.Empty;
        if (reason.Length is < TicketEscalation.ReasonMinLength or > TicketEscalation.ReasonMaxLength)
            throw new ValidationException(new Dictionary<string, string[]> { ["reason"] = [$"Reason must be {TicketEscalation.ReasonMinLength}-{TicketEscalation.ReasonMaxLength} characters."] });
        if (actor.Length == 0 || actor.Length > TicketEscalation.EscalatedByMaxLength)
            throw new ValidationException(new Dictionary<string, string[]> { ["escalatedBy"] = ["An actor is required."] });

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct) ?? throw new NotFoundException("Category", ticket.CategoryId);
        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct) ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var now = clock.UtcNow;
        var nextPriority = (TicketPriority)((int)ticket.Priority + 1);
        var assignment = AgentSelector.Select(category, await agents.GetCandidatesAsync(ct), ticket.AssignedAgentId);
        ticket.Escalate(reason, actor, policy.DueAt(now, nextPriority, customer.Tier), assignment.AgentId, now);
        await unitOfWork.SaveChangesAsync(ct);
        return await getTicket.HandleAsync(id, ct);
    }
}
