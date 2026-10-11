import { DecimalPipe, PercentPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { LineupOptimizerResult, MissingPlayer, POSITION_LABELS } from '../../core/models';

const POSITION_ORDER = [1, 2, 3, 4];

@Component({
  selector: 'app-lineup-optimizer',
  imports: [DecimalPipe, PercentPipe],
  templateUrl: './lineup-optimizer.component.html',
  styleUrl: './lineup-optimizer.component.css',
})
export class LineupOptimizerComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly leagueContext = inject(LeagueContextService);

  readonly result = signal<LineupOptimizerResult | null>(null);
  readonly loading = signal(true);
  readonly positionLabels = POSITION_LABELS;

  readonly playersByPosition = computed(() => {
    const players = this.result()?.players ?? [];
    return POSITION_ORDER.map((position) => ({
      position,
      label: this.positionLabels[position],
      players: players.filter((p) => p.position === position),
    })).filter((group) => group.players.length > 0);
  });

  constructor() {
    this.leagueContext.ensureLoaded();

    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (leagueId) {
        this.load(leagueId);
      }
    });
  }

  load(leagueId: string): void {
    this.loading.set(true);
    this.api.getLineupOptimizer(leagueId).subscribe((result) => {
      this.result.set(result);
      this.loading.set(false);
    });
  }

  refresh(): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId);
    }
  }

  openPlayer(playerId: string): void {
    this.router.navigate(['/spieler', playerId]);
  }

  missingPlayersLabel(missing: MissingPlayer[]): string {
    return missing.map((m) => `${m.count} ${m.position}`).join(', ');
  }
}
