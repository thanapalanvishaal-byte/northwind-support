# Implementation approach

Task 1 was already present as two focused commits: the React list request is derived from the complete filter state and only search is debounced, while ticket filters are applied to the EF query before count, sort and paging.

For Task 2, triage is split between pure domain rules and application orchestration. Categories resolve forced priority, `SlaPolicy` computes configurable windows, and `AgentSelector` receives candidate data and applies capacity, active-state, specialization, and deterministic tie-breaking. Creation and escalation use the same policy and selector. Escalations are immutable child rows and are read newest first. The SLA list filter is expressed as EF predicates so it executes in SQL.

The UI uses one `SlaCell` for all SLA states, adds the SLA filter to the existing filter object, and refreshes the detail and history after escalation. Client validation prevents short or overlong escalation reasons from reaching the API.

The container uses a cached restore layer and a non-root ASP.NET runtime. `RUN_MIGRATIONS=true` is intended for the local empty-volume workflow; a production deployment should run migrations as a separately controlled release step.

Authentication stores an ASP.NET Identity password hash on `Agent`, issues eight-hour JWTs from a configured signing key, and applies a fallback authorization policy. The token is kept in browser local storage for this assessment; production would prefer an HttpOnly cookie or a stronger session architecture. Escalation uses the authenticated name claim rather than accepting an actor from the request body.

Verification: `dotnet build SupportDesk.sln --no-restore`, `dotnet test SupportDesk.sln`, and `npm run typecheck`. Existing package audit warnings for `System.Security.Cryptography.Pkcs` remain and are unrelated to these changes.
