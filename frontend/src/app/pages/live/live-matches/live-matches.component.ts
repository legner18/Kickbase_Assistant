import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, ElementRef, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/api.service';
import { LeagueContextService } from '../../../core/league-context.service';
import { LiveHubService } from '../../../core/live-hub.service';
import { summarizeMatchEvents } from '../../../core/match-events';
import { LiveMatch, LiveMatchTeam, LiveMatchday, LiveTeamPlayer, LiveTeamPoints, POSITION_LABELS } from '../../../core/models';
import { LiveTimelineComponent } from '../live-timeline/live-timeline.component';

interface SelectedTeam {
  matchId: string;
  teamId: string;
}

@Component({
  selector: 'app-live-matches',
  imports: [DatePipe, DecimalPipe, RouterLink, LiveTimelineComponent],
  templateUrl: './live-matches.component.html',
  styleUrl: './live-matches.component.css',
})
export class LiveMatchesComponent {
  private readonly api = inject(ApiService);
  private readonly leagueContext = inject(LeagueContextService);
  private readonly liveHub = inject(LiveHubService);
  private readonly initialLiveUpdate = this.liveHub.lastLiveMatchesUpdate();

  private readonly teamPanel = viewChild<ElementRef<HTMLElement>>('teamPanel');

  readonly matchday = signal<LiveMatchday | null>(null);
  readonly matchesLoading = signal(true);
  readonly selectedTeam = signal<SelectedTeam | null>(null);
  readonly teamPoints = signal<LiveTeamPoints | null>(null);
  readonly teamLoading = signal(false);
  readonly expandedPlayerId = signal<string | null>(null);
  readonly positionLabels = POSITION_LABELS;

  readonly matches = computed(() => this.matchday()?.matches ?? []);

  readonly liveMatchCount = computed(() => this.matches().filter((match) => match.status === 'Live').length);

  readonly selectedMatch = computed(() => {
    const selected = this.selectedTeam();
    return this.matches().find((match) => match.matchId === selected?.matchId) ?? null;
  });

  readonly teamPlayers = computed(() =>
    (this.teamPoints()?.players ?? []).map((player) => ({ ...player, events: summarizeMatchEvents(player.eventKeys) })),
  );

  constructor() {
    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (leagueId) {
        untracked(() => {
          this.selectedTeam.set(null);
          this.teamPoints.set(null);
          this.expandedPlayerId.set(null);
          this.loadMatches(leagueId);
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

        this.loadMatches(leagueId, true);
        const selected = this.selectedTeam();
        if (selected) {
          this.loadTeam(leagueId, selected, true);
        }
      });
    });
  }

  isSelected(match: LiveMatch, team: LiveMatchTeam): boolean {
    const selected = this.selectedTeam();
    return selected?.matchId === match.matchId && selected.teamId === team.teamId;
  }

  canOpen(match: LiveMatch): boolean {
    return match.status !== 'NotStarted' || match.pointsUpdatedAt !== null;
  }

  teamLabel(team: LiveMatchTeam): string {
    return team.name ?? team.symbol ?? team.teamId;
  }

  onTeamClick(match: LiveMatch, team: LiveMatchTeam): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (!leagueId) {
      return;
    }

    this.expandedPlayerId.set(null);
    if (this.isSelected(match, team)) {
      this.selectedTeam.set(null);
      this.teamPoints.set(null);
      return;
    }

    const selected = { matchId: match.matchId, teamId: team.teamId };
    this.selectedTeam.set(selected);
    this.teamPoints.set(null);
    this.loadTeam(leagueId, selected);

    if (window.matchMedia('(max-width: 900px)').matches) {
      setTimeout(() => this.teamPanel()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }));
    }
  }

  onPlayerClick(player: LiveTeamPlayer): void {
    if (player.hasTimeline) {
      this.expandedPlayerId.set(this.isExpanded(player) ? null : player.playerId);
    }
  }

  isExpanded(player: LiveTeamPlayer): boolean {
    return this.expandedPlayerId() === player.playerId;
  }

  private loadMatches(leagueId: string, silent = false): void {
    if (!silent) {
      this.matchesLoading.set(true);
    }
    this.api.getLiveMatches(leagueId).subscribe({
      next: (matchday) => {
        this.matchday.set(matchday);
        this.matchesLoading.set(false);
      },
      error: () => this.matchesLoading.set(false),
    });
  }

  private loadTeam(leagueId: string, team: SelectedTeam, silent = false): void {
    if (!silent) {
      this.teamLoading.set(true);
    }
    this.api.getLiveTeamPoints(leagueId, team.matchId, team.teamId).subscribe({
      next: (points) => {
        const selected = this.selectedTeam();
        if (selected?.matchId === team.matchId && selected.teamId === team.teamId) {
          this.teamPoints.set(points);
        }
        this.teamLoading.set(false);
      },
      error: () => this.teamLoading.set(false),
    });
  }
}
