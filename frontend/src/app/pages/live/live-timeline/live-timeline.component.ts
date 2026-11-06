import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ApiService } from '../../../core/api.service';
import { LeagueContextService } from '../../../core/league-context.service';
import { onLiveRefresh } from '../../../core/live-refresh';
import { LivePlayerTimeline } from '../../../core/models';

const SIGNIFICANT_POINTS = 10;

@Component({
  selector: 'app-live-timeline',
  imports: [DatePipe],
  templateUrl: './live-timeline.component.html',
  styleUrl: './live-timeline.component.css',
})
export class LiveTimelineComponent {
  readonly matchId = input.required<string>();
  readonly playerId = input.required<string>();

  private readonly api = inject(ApiService);
  private readonly leagueContext = inject(LeagueContextService);

  readonly timeline = signal<LivePlayerTimeline | null>(null);
  readonly loading = signal(true);
  readonly showAll = signal(false);

  readonly events = computed(() => this.timeline()?.events ?? []);

  readonly eventSum = computed(() => this.events().reduce((sum, matchEvent) => sum + matchEvent.points, 0));

  readonly scoringEventCount = computed(() => this.events().filter((matchEvent) => matchEvent.points !== 0).length);

  readonly visibleEvents = computed(() =>
    this.showAll() ? this.events() : this.events().filter((matchEvent) => !this.isMinorEvent(matchEvent.points)),
  );

  readonly hiddenEvents = computed(() => {
    if (this.showAll()) {
      return null;
    }

    const hidden = this.events().filter((matchEvent) => this.isMinorEvent(matchEvent.points));
    return hidden.length > 0
      ? { count: hidden.length, points: hidden.reduce((sum, matchEvent) => sum + matchEvent.points, 0) }
      : null;
  });

  constructor() {
    effect(() => {
      const matchId = this.matchId();
      const playerId = this.playerId();
      untracked(() => this.load(matchId, playerId));
    });

    onLiveRefresh(() => this.load(this.matchId(), this.playerId(), true));
  }

  onShowAllClick(): void {
    this.showAll.update((showAll) => !showAll);
  }

  formatPoints(points: number): string {
    return points > 0 ? `+${points}` : `${points}`;
  }

  private isMinorEvent(points: number): boolean {
    return points !== 0 && Math.abs(points) < SIGNIFICANT_POINTS;
  }

  private load(matchId: string, playerId: string, silent = false): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (!leagueId) {
      return;
    }

    if (!silent) {
      this.loading.set(true);
    }
    this.api.getLivePlayerTimeline(leagueId, matchId, playerId).subscribe({
      next: (timeline) => {
        if (matchId === this.matchId() && playerId === this.playerId()) {
          this.timeline.set(timeline);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
