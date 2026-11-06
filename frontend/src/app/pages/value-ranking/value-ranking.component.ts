import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { FairValueRadar, POSITION_LABELS, ValueRankingEntry } from '../../core/models';
import { FairValueRadarComponent } from './fair-value-radar.component';

@Component({
  selector: 'app-value-ranking',
  imports: [DecimalPipe, FairValueRadarComponent],
  templateUrl: './value-ranking.component.html',
  styleUrl: './value-ranking.component.css',
})
export class ValueRankingComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly leagueContext = inject(LeagueContextService);

  readonly radar = signal<FairValueRadar | null>(null);
  readonly entries = signal<ValueRankingEntry[]>([]);
  readonly loading = signal(true);
  readonly search = signal('');
  readonly positionFilter = signal<number | null>(null);

  readonly positionLabels = POSITION_LABELS;

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) return this.entries();
    return this.entries().filter(
      (e) => e.name.toLowerCase().includes(term) || (e.teamName ?? '').toLowerCase().includes(term),
    );
  });

  constructor() {
    this.load();
    this.leagueContext.ensureLoaded();
    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (leagueId) {
        this.api
          .getFairValueRadar(leagueId)
          .pipe(catchError(() => of(null)))
          .subscribe((radar) => this.radar.set(radar));
      }
    });
  }

  load(): void {
    this.loading.set(true);
    this.api.getValueRanking(this.positionFilter() ?? undefined).subscribe((entries) => {
      this.entries.set(entries);
      this.loading.set(false);
    });
  }

  onPositionChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.positionFilter.set(value ? Number(value) : null);
    this.load();
  }

  openPlayer(playerId: string): void {
    this.router.navigate(['/spieler', playerId]);
  }

  isDeal(entry: ValueRankingEntry): boolean {
    return entry.fairValueDeltaPercent >= 30;
  }
}
