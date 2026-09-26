import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { LINEUP_STATUS_LABELS, LeagueManager, POSITION_LABELS, Player } from '../../core/models';
import { SparklineComponent } from '../../shared/sparkline/sparkline.component';

type SortKey = 'name' | 'position' | 'marketValue' | 'change' | 'totalPoints';

@Component({
  selector: 'app-squad',
  imports: [DecimalPipe, SparklineComponent],
  templateUrl: './squad.component.html',
  styleUrl: './squad.component.css',
})
export class SquadComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly leagueContext = inject(LeagueContextService);

  readonly players = signal<Player[]>([]);
  readonly history = signal<Record<string, number[]>>({});
  readonly loading = signal(true);
  readonly search = signal('');
  readonly sortKey = signal<SortKey>('marketValue');
  readonly sortDesc = signal(true);

  readonly managers = signal<LeagueManager[]>([]);
  readonly selectedManagerId = signal<string | null>(null);

  readonly positionLabels = POSITION_LABELS;
  readonly lineupLabels = LINEUP_STATUS_LABELS;

  readonly filteredSorted = computed(() => {
    const term = this.search().trim().toLowerCase();
    let list = this.players();
    if (term) {
      list = list.filter((p) => p.name.toLowerCase().includes(term) || (p.teamName ?? '').toLowerCase().includes(term));
    }

    const key = this.sortKey();
    const dir = this.sortDesc() ? -1 : 1;
    return [...list].sort((a, b) => {
      const av = key === 'change' ? (a.latestMarketValueChange ?? 0) : key === 'name' ? a.name : a[key];
      const bv = key === 'change' ? (b.latestMarketValueChange ?? 0) : key === 'name' ? b.name : b[key];
      if (typeof av === 'string' || typeof bv === 'string') {
        return dir * String(av).localeCompare(String(bv));
      }
      return dir * ((av as number) - (bv as number));
    });
  });

  readonly totalValue = computed(() => this.players().reduce((sum, p) => sum + p.marketValue, 0));

  constructor() {
    this.leagueContext.ensureLoaded();
    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (!leagueId) {
        return;
      }
      this.selectedManagerId.set(null);
      this.api.getManagers(leagueId).subscribe((managers) => this.managers.set(managers));
      this.load(leagueId, undefined);
    });
  }

  onManagerChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    const leagueId = this.leagueContext.selectedLeagueId();
    if (!leagueId) return;
    this.selectedManagerId.set(value || null);
    this.load(leagueId, value || undefined);
  }

  load(leagueId: string, managerId: string | undefined): void {
    this.loading.set(true);
    this.api.getSquad(leagueId, managerId).subscribe((players) => {
      this.players.set(players);
      this.loading.set(false);

      if (players.length === 0) {
        this.history.set({});
        return;
      }

      forkJoin(
        Object.fromEntries(
          players.map((p) => [p.id, this.api.getMarketValueHistory(p.id).pipe(catchError(() => of([])))]),
        ),
      ).subscribe((result) => {
        const map: Record<string, number[]> = {};
        for (const [playerId, points] of Object.entries(result)) {
          map[playerId] = points.slice(-21).map((pt) => pt.marketValue);
        }
        this.history.set(map);
      });
    });
  }

  refresh(): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId, this.selectedManagerId() ?? undefined);
    }
  }

  setSort(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortDesc.update((d) => !d);
    } else {
      this.sortKey.set(key);
      this.sortDesc.set(true);
    }
  }

  openPlayer(playerId: string): void {
    this.router.navigate(['/spieler', playerId]);
  }

  lineupBadgeClass(status: string | null): string {
    switch (status) {
      case 'Starting':
        return 'badge-good';
      case 'Doubtful':
        return 'badge-warning';
      case 'Bench':
        return 'badge-neutral';
      case 'Out':
        return 'badge-critical';
      default:
        return 'badge-neutral';
    }
  }
}
