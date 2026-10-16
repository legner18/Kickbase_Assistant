import { DecimalPipe } from '@angular/common';
import { Component, OnDestroy, OnInit, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { LiveHubService } from '../../core/live-hub.service';
import { POSITION_LABELS, SellPlan } from '../../core/models';

@Component({
  selector: 'app-sell-plan',
  imports: [DecimalPipe],
  templateUrl: './sell-plan.component.html',
  styleUrl: './sell-plan.component.css',
})
export class SellPlanComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly liveHub = inject(LiveHubService);
  readonly leagueContext = inject(LeagueContextService);

  readonly plan = signal<SellPlan | null>(null);
  readonly loading = signal(true);
  readonly positionLabels = POSITION_LABELS;

  constructor() {
    this.leagueContext.ensureLoaded();

    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (leagueId) {
        this.load(leagueId);
      }
    });

    // OwnSquadSyncJob (alle 2 Min) pusht nur, wenn sich Kader oder Kontostand wirklich geändert
    // haben - dann still neu laden, ohne Skeleton, damit die Seite nicht bei jedem Kauf flackert.
    effect(() => {
      const update = this.liveHub.lastSquadUpdate();
      const leagueId = this.leagueContext.selectedLeagueId();
      if (update && update.leagueId === leagueId) {
        this.load(leagueId, true);
      }
    });
  }

  ngOnInit(): void {
    this.liveHub.connect();
  }

  ngOnDestroy(): void {
    this.liveHub.disconnect();
  }

  load(leagueId: string, silent = false): void {
    if (!silent) {
      this.loading.set(true);
    }
    this.api.getSellPlan(leagueId).subscribe((plan) => {
      this.plan.set(plan);
      this.loading.set(false);
    });
  }

  refresh(): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId);
    }
  }

  toggleLock(playerId: string): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (!leagueId) {
      return;
    }
    this.api.toggleSellLock(leagueId, playerId).subscribe(() => this.load(leagueId));
  }

  openPlayer(playerId: string): void {
    this.router.navigate(['/spieler', playerId]);
  }
}
