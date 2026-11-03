import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, ElementRef, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/api.service';
import { LeagueContextService } from '../../../core/league-context.service';
import { LiveHubService } from '../../../core/live-hub.service';
import { summarizeMatchEvents } from '../../../core/match-events';
import {
  LiveManagerDetail,
  LiveManagerPlayer,
  LiveManagerStanding,
  LiveManagerStandings,
  POSITION_LABELS,
} from '../../../core/models';
import { LiveTimelineComponent } from '../live-timeline/live-timeline.component';

@Component({
  selector: 'app-live-league',
  imports: [DatePipe, DecimalPipe, NgTemplateOutlet, RouterLink, LiveTimelineComponent],
  templateUrl: './live-league.component.html',
  styleUrl: './live-league.component.css',
})
export class LiveLeagueComponent {
  private readonly api = inject(ApiService);
  private readonly leagueContext = inject(LeagueContextService);
  private readonly liveHub = inject(LiveHubService);
  private readonly initialLiveUpdate = this.liveHub.lastLiveMatchesUpdate();

  private readonly managerPanel = viewChild<ElementRef<HTMLElement>>('managerPanel');

  readonly standings = signal<LiveManagerStandings | null>(null);
  readonly standingsLoading = signal(true);
  readonly selectedManagerId = signal<string | null>(null);
  readonly manager = signal<LiveManagerDetail | null>(null);
  readonly managerLoading = signal(false);
  readonly expandedPlayerId = signal<string | null>(null);
  readonly positionLabels = POSITION_LABELS;

  readonly managers = computed(() => this.standings()?.managers ?? []);

  readonly players = computed(() =>
    (this.manager()?.players ?? []).map((player) => ({ ...player, events: summarizeMatchEvents(player.eventKeys) })),
  );

  readonly lineupPlayers = computed(() => this.players().filter((player) => player.inLineup));

  readonly benchPlayers = computed(() => this.players().filter((player) => !player.inLineup));

  constructor() {
    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (leagueId) {
        untracked(() => {
          this.selectedManagerId.set(null);
          this.manager.set(null);
          this.expandedPlayerId.set(null);
          this.loadStandings(leagueId);
        });
      }
    });

    effect(() => {
      const update = this.liveHub.lastLiveMatchesUpdate();
      if (!update || update === this.initialLiveUpdate) {
        return;
      }

      untracked(() => {
        const leagueId = this.leagueContext.selectedLeagueId();
        if (!leagueId || this.leagueContext.selectedLeague()?.competitionId !== update.competitionId) {
          return;
        }

        this.loadStandings(leagueId, true);
        const managerId = this.selectedManagerId();
        if (managerId) {
          this.loadManager(leagueId, managerId, true);
        }
      });
    });
  }

  onManagerClick(standing: LiveManagerStanding): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (!leagueId || standing.managerId === this.selectedManagerId()) {
      return;
    }

    this.selectManager(leagueId, standing.managerId);

    if (window.matchMedia('(max-width: 900px)').matches) {
      setTimeout(() => this.managerPanel()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }));
    }
  }

  onPlayerClick(player: LiveManagerPlayer): void {
    if (!player.hasTimeline || !player.match) {
      return;
    }

    this.expandedPlayerId.set(this.isExpanded(player) ? null : player.playerId);
  }

  isExpanded(player: LiveManagerPlayer): boolean {
    return this.expandedPlayerId() === player.playerId;
  }

  private selectManager(leagueId: string, managerId: string): void {
    this.selectedManagerId.set(managerId);
    this.manager.set(null);
    this.expandedPlayerId.set(null);
    this.loadManager(leagueId, managerId);
  }

  private loadStandings(leagueId: string, silent = false): void {
    if (!silent) {
      this.standingsLoading.set(true);
    }
    this.api.getLiveManagers(leagueId).subscribe({
      next: (standings) => {
        this.standings.set(standings);
        this.standingsLoading.set(false);

        if (!this.selectedManagerId()) {
          const preselected = standings.managers.find((standing) => standing.isMe) ?? standings.managers[0];
          if (preselected) {
            this.selectManager(leagueId, preselected.managerId);
          }
        }
      },
      error: () => this.standingsLoading.set(false),
    });
  }

  private loadManager(leagueId: string, managerId: string, silent = false): void {
    if (!silent) {
      this.managerLoading.set(true);
    }
    this.api.getLiveManager(leagueId, managerId).subscribe({
      next: (manager) => {
        if (this.selectedManagerId() === managerId) {
          this.manager.set(manager);
        }
        this.managerLoading.set(false);
      },
      error: () => this.managerLoading.set(false),
    });
  }
}
