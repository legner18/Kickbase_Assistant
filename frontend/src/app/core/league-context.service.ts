import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { League } from './models';

@Injectable({ providedIn: 'root' })
export class LeagueContextService {
  private readonly api = inject(ApiService);

  readonly leagues = signal<League[]>([]);
  readonly archivedLeagues = signal<League[]>([]);
  readonly selectedLeagueId = signal<string | null>(null);
  readonly selectedLeague = computed(() =>
    this.leagues().find((l) => l.id === this.selectedLeagueId()) ?? null,
  );

  private loaded = false;
  private archivedLoaded = false;

  constructor() {
    effect(() => {
      const leagues = this.leagues();
      const currentId = this.selectedLeagueId();
      // Fällt die gerade gewählte Liga aus der aktiven Liste (z.B. gerade archiviert), auf die
      // erste verbleibende aktive Liga zurückfallen, statt auf einer toten Auswahl stehenzubleiben.
      if (currentId && leagues.length > 0 && !leagues.some((l) => l.id === currentId)) {
        this.selectedLeagueId.set(leagues[0].id);
      }
    });
  }

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

  ensureArchivedLoaded(): void {
    if (this.archivedLoaded) {
      return;
    }
    this.archivedLoaded = true;

    this.api.getLeagues(true).subscribe((leagues) => {
      this.archivedLeagues.set(leagues.filter((l) => !l.isActive));
    });
  }
}
