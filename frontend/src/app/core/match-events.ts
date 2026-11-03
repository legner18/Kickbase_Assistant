import { MATCH_EVENT_LABELS } from './models';

export interface MatchEventChip {
  label: string;
  badgeClass: string;
}

const EVENT_BADGE_CLASSES: Record<number, string> = {
  1: 'badge-good',
  2: 'badge-critical',
  3: 'badge-good',
  4: 'badge-warning',
  8: 'badge-neutral',
  9: 'badge-neutral',
};

export function summarizeMatchEvents(eventKeys: number[]): MatchEventChip[] {
  const counts = new Map<number, number>();
  for (const eventKey of eventKeys) {
    if (MATCH_EVENT_LABELS[eventKey]) {
      counts.set(eventKey, (counts.get(eventKey) ?? 0) + 1);
    }
  }

  return [...counts.entries()]
    .sort(([firstKey], [secondKey]) => firstKey - secondKey)
    .map(([eventKey, count]) => ({
      label: (count > 1 ? `${count}× ` : '') + MATCH_EVENT_LABELS[eventKey],
      badgeClass: EVENT_BADGE_CLASSES[eventKey] ?? 'badge-neutral',
    }));
}
