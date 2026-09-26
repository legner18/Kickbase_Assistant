import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { LeagueTransfer, Player, SellAlert } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, DecimalPipe, DatePipe],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent {
  private readonly api = inject(ApiService);
  readonly leagueContext = inject(LeagueContextService);

  readonly players = signal<Player[]>([]);
  readonly openAlerts = signal<SellAlert[]>([]);
  readonly transfers = signal<LeagueTransfer[]>([]);
  readonly loading = signal(true);

  readonly squadValue = computed(() => this.players().reduce((sum, p) => sum + p.marketValue, 0));

  readonly topGainers = computed(() =>
    [...this.players()]
      .filter((p) => (p.latestMarketValueChange ?? 0) > 0)
      .sort((a, b) => (b.latestMarketValueChange ?? 0) - (a.latestMarketValueChange ?? 0))
      .slice(0, 3),
  );

  readonly topLosers = computed(() =>
    [...this.players()]
      .filter((p) => (p.latestMarketValueChange ?? 0) < 0)
      .sort((a, b) => (a.latestMarketValueChange ?? 0) - (b.latestMarketValueChange ?? 0))
      .slice(0, 3),
  );

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
    this.api.getSquad(leagueId).subscribe((players) => {
      this.players.set(players);
      this.loading.set(false);
    });
    this.api.getSellAlerts(false).subscribe((alerts) => this.openAlerts.set(alerts));
    this.api.getTransfers(leagueId).subscribe((transfers) => this.transfers.set(transfers.slice(0, 5)));
  }

  refresh(): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId);
    }
  }
}
