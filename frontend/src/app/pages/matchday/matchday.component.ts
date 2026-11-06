import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { MatchdayMatch, MatchdayOverview, POSITION_LABELS } from '../../core/models';
import { OutcomeBarComponent } from '../../shared/outcome-bar/outcome-bar.component';

@Component({
  selector: 'app-matchday',
  imports: [DatePipe, DecimalPipe, OutcomeBarComponent],
  templateUrl: './matchday.component.html',
  styleUrl: './matchday.component.css',
})
export class MatchdayComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly leagueContext = inject(LeagueContextService);

  readonly overview = signal<MatchdayOverview | null>(null);
  readonly loading = signal(true);
  readonly positionLabels = POSITION_LABELS;

  readonly previousMatchday = computed(() => this.neighbourMatchday(-1));
  readonly nextMatchday = computed(() => this.neighbourMatchday(1));

  constructor() {
    this.leagueContext.ensureLoaded();
    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (leagueId) {
        this.load(leagueId);
      }
    });
  }

  load(leagueId: string, matchday?: number): void {
    this.loading.set(true);
    this.api.getMatchday(leagueId, matchday).subscribe((overview) => {
      this.overview.set(overview);
      this.loading.set(false);
    });
  }

  onRefreshClick(): void {
    this.loadSelected(this.overview()?.matchdayNumber ?? undefined);
  }

  onMatchdayChange(event: Event): void {
    this.loadSelected(Number((event.target as HTMLSelectElement).value));
  }

  onPreviousClick(): void {
    this.loadSelected(this.previousMatchday() ?? undefined);
  }

  onNextClick(): void {
    this.loadSelected(this.nextMatchday() ?? undefined);
  }

  onPlayerClick(playerId: string): void {
    this.router.navigate(['/spieler', playerId]);
  }

  teamLabel(name: string | null, symbol: string | null): string {
    return name ?? symbol ?? '–';
  }

  hasResult(match: MatchdayMatch): boolean {
    return match.status !== 'NotStarted' && match.homeGoals !== null && match.awayGoals !== null;
  }

  difficultyClass(tier: number): string {
    if (tier <= 2) {
      return 'badge-good';
    }
    if (tier === 3) {
      return 'badge-warning';
    }
    return 'badge-critical';
  }

  private loadSelected(matchday: number | undefined): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId, matchday);
    }
  }

  private neighbourMatchday(direction: number): number | null {
    const data = this.overview();
    if (!data || data.matchdayNumber === null) {
      return null;
    }
    const index = data.availableMatchdays.indexOf(data.matchdayNumber);
    return data.availableMatchdays[index + direction] ?? null;
  }
}
