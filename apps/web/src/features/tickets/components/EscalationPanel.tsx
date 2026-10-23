import { useEffect, useState } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { formatDateTime } from '../../../lib/format';
import type { TicketDetail, TicketEscalation } from '../../../types/api';

export function EscalationPanel({ ticket, onUpdated }: { ticket: TicketDetail; onUpdated: (ticket: TicketDetail) => void }) {
  const [reason, setReason] = useState('');
  const [history, setHistory] = useState<TicketEscalation[]>([]);
  const [error, setError] = useState<string>();
  const [saving, setSaving] = useState(false);
  useEffect(() => { void ticketsApi.getEscalations(ticket.id).then(setHistory).catch(() => setHistory([])); }, [ticket.id]);
  const submit = async () => {
    if (reason.trim().length < 5 || reason.trim().length > 500) { setError('Reason must be between 5 and 500 characters.'); return; }
    setSaving(true); setError(undefined);
    try { const updated = await ticketsApi.escalate(ticket.id, reason); onUpdated(updated); setReason(''); setHistory(await ticketsApi.getEscalations(ticket.id)); }
    catch (caught) { setError(toErrorMessage(caught, 'Could not escalate the ticket.')); }
    finally { setSaving(false); }
  };
  return <div className="card"><h2>Escalation</h2>
    {ticket.priority !== 'Critical' && !['Resolved', 'Closed'].includes(ticket.status) && <>
      <label className="field__label" htmlFor="escalation-reason">Reason</label>
      <textarea id="escalation-reason" rows={3} value={reason} onChange={(event) => setReason(event.target.value)} />
      {error && <p className="field__error" role="alert">{error}</p>}
      <button className="button button--primary" type="button" disabled={saving} onClick={() => void submit()}>{saving ? 'Escalating...' : 'Escalate'}</button>
    </>}
    {history.length > 0 && <><h3>History</h3><ol>{history.map((entry) => <li key={entry.id}>{entry.fromPriority} → {entry.toPriority}: {entry.reason} ({formatDateTime(entry.escalatedAtUtc)})</li>)}</ol></>}
  </div>;
}
