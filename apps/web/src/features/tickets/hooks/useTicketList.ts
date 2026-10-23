import { useCallback, useMemo, useState } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';
import { useDebouncedValue } from '../../../hooks/useDebouncedValue';
import type { TicketFilters } from '../../../types/api';

const SEARCH_DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  slaStatus: undefined,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

/**
 * Owns the ticket list's filters and the data they produce.
 *
 * The request is derived from the whole filter object, so any filter added to `TicketFilters`
 * (such as the Task 2 SLA status) is part of the request with no extra wiring. Only the search
 * text is debounced; every other change requests immediately.
 */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const debouncedSearch = useDebouncedValue(filters.search, SEARCH_DEBOUNCE_MS);

  // Everything except the search text, as a string, so the request below changes only when a
  // filter's value changes (not on every keystroke, and not by object identity). Any filter
  // added to TicketFilters is included automatically.
  const otherFilters: Partial<TicketFilters> = { ...filters };
  delete otherFilters.search;
  const otherFiltersKey = JSON.stringify(otherFilters);

  const request = useMemo<TicketFilters>(
    () => ({
      ...(JSON.parse(otherFiltersKey) as Omit<TicketFilters, 'search'>),
      search: debouncedSearch,
    }),
    [otherFiltersKey, debouncedSearch],
  );

  const load = useCallback(() => ticketsApi.getTickets(request), [request]);
  const { isLoading, data, error, reload } = useAsyncData(load, 'Unable to load tickets.');

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));

  const resetFilters = () => setFilters(emptyFilters);

  return { filters, updateFilters, resetFilters, reload, isLoading, data, error };
}
