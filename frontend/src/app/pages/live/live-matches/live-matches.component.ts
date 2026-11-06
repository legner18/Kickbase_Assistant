import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, ElementRef, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/api.service';
import { LeagueContextService } from '../../../core/league-context.service';
import { createNowSignal, isLiveDataStale, onLiveRefresh } from '../../../core/live-refresh';
import { summarizeMatchEvents } from '../../../core/match-events';
import { LiveMatch, LiveMatchTeam, LiveMatchday, LiveTeamPlayer, LiveTeamPoints, POSITION_LABELS } from '../../../core/models';
import { createPointsChangeTracker } from '../../../core/points-change-tracker';
import { SignedNumberPipe } from '../../../shared/signed-number.pipe';
import { LiveTimelineComponent } from '../live-timeline/live-timeline.component';

interface SelectedTeam {
  matchId: string;
  teamId: string;
}

function teamKey(matchId: string, teamId: string): string {
  return `${matchId}:${teamId}`;
}

@Component({
  selector: 'app-live-matches',
  imports: [DatePipe, DecimalPipe, RouterLink, SignedNumberPipe, LiveTimelineComponent],
  templateUrl: './live-matches.component.html',
  styleUrl: './live-matches.component.css',
})
export class LiveMatchesComponent {
  private readonly api = inject(ApiService);
  private readonly leagueContext = inject(LeagueContextService);
  private readonly now = createNowSignal();
  private readonly teamChanges = createPointsChangeTracker();
  private readonly playerChanges = createPointsChangeTracker();

  private readonly teamPanel = viewChild<ElementRef<HTMLElement>>('teamPanel');

  readonly matchday = signal<LiveMatchday | null>(null);
  readonly matchesLoading = signal(true);
  readonly selectedTeam = signal<SelectedTeam | null>(null);
  readonly teamPoints = signal<LiveTeamPoints | null>(null);
  readonly teamLoading = signal(false);
  readonly expandedPlayerId = signal<string | null>(null);
  readonly positionLabels = POSITION_LABELS;

  readonly matches = computed(() => {
    const deltas = this.teamChanges.deltas();
    const now = this.now();
    return (this.matchday()?.matches ?? []).map((match) => ({
      ...match,
      isStale: match.status === 'Live' && isLiveDataStale(match.pointsUpdatedAt, now),
      home: { ...match.home, pointsDelta: deltas.get(teamKey(match.matchId, match.home.teamId)) ?? 0 },
      away: { ...match.away, pointsDelta: deltas.get(teamKey(match.matchId, match.away.teamId)) ?? 0 },
    }));
  });

  readonly liveMatchCount = computed(() => this.matches().filter((match) => match.status === 'Live').length);

  readonly selectedMatch = computed(() => {
    const selected = this.selectedTeam();
    return this.matches().find((match) => match.matchId === selected?.matchId) ?? null;
  });

  readonly selectedTeamDelta = computed(() => {
    const selected = this.selectedTeam();
    return selected ? (this.teamChanges.deltas().get(teamKey(selected.matchId, selected.teamId)) ?? 0) : 0;
  });

  readonly teamPlayers = computed(() => {
    const deltas = this.playerChanges.deltas();
    return (this.teamPoints()?.players ?? []).map((player) => ({
      ...player,
      events: summarizeMatchEvents(player.eventKeys),
      pointsDelta: deltas.get(player.playerId) ?? 0,
    }));
  });

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

    onLiveRefresh(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (!leagueId) {
        return;
      }

      this.loadMatches(leagueId, true);
      const selected = this.selectedTeam();
      if (selected) {
        this.loadTeam(leagueId, selected, true);
      }
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
        this.teamChanges.update(
          matchday.matches.flatMap((match) => [
            [teamKey(match.matchId, match.home.teamId), match.home.points] as const,
            [teamKey(match.matchId, match.away.teamId), match.away.points] as const,
          ]),
          silent,
        );
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
          this.playerChanges.update(
            points.players.map((player) => [player.playerId, player.points] as const),
            silent,
          );
          this.teamPoints.set(points);
        }
        this.teamLoading.set(false);
      },
      error: () => this.teamLoading.set(false),
    });
  }
}
