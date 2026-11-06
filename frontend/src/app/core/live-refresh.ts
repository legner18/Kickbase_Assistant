import { DestroyRef, Signal, effect, inject, signal, untracked } from '@angular/core';
import { LeagueContextService } from './league-context.service';
import { LiveHubService } from './live-hub.service';

export const LIVE_STALE_AFTER_MS = 2 * 60_000;

export function isLiveDataStale(timestamp: string | null | undefined, now: number): boolean {
  return !!timestamp && now - Date.parse(timestamp) > LIVE_STALE_AFTER_MS;
}

export function onLiveRefresh(refresh: () => void): void {
  const liveHub = inject(LiveHubService);
  const leagueContext = inject(LeagueContextService);
  const initialUpdate = liveHub.lastLiveMatchesUpdate();
  const initialResync = liveHub.lastResync();

  effect(() => {
    const update = liveHub.lastLiveMatchesUpdate();
    if (!update || update === initialUpdate) {
      return;
    }

    untracked(() => {
      if (leagueContext.selectedLeague()?.competitionId === update.competitionId) {
        refresh();
      }
    });
  });

  effect(() => {
    const resync = liveHub.lastResync();
    if (resync && resync !== initialResync) {
      untracked(refresh);
    }
  });
}

export function createNowSignal(intervalMs = 15_000): Signal<number> {
  const now = signal(Date.now());
  const timer = setInterval(() => now.set(Date.now()), intervalMs);
  inject(DestroyRef).onDestroy(() => clearInterval(timer));
  return now.asReadonly();
}
