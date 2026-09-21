import { Injectable, computed, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { League } from './models';

@Injectable({ providedIn: 'root' })
export class LeagueContextService {
  private readonly api = inject(ApiService);

  readonly leagues = signal<League[]>([]);
  readonly selectedLeagueId = signal<string | null>(null);
  readonly selectedLeague = computed(() =>
    this.leagues().find((l) => l.id === this.selectedLeagueId()) ?? null,
  );

  private loaded = false;

  ensureLoaded(): void {
    if (this.loaded) {
      return;
    }
    this.loaded = true;

    this.api.getLeagues().subscribe((leagues) => {
      this.leagues.set(leagues);
      if (leagues.length > 0 && !this.selectedLeagueId()) {
        this.selectedLeagueId.set(leagues[0].id);
      }
    });
  }
}
