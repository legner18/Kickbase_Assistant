import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnDestroy, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { LeagueBalanceStatus, LeagueTransfer, Player, SellAlert } from '../../core/models';

const STATUS_LABELS: Record<LeagueBalanceStatus['status'], string> = {
  Green: 'Im Plus',
  Yellow: 'Knapp, aber im Rahmen',
  Red: 'Kritisch',
};

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, DecimalPipe, DatePipe],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnDestroy {
  private readonly api = inject(ApiService);
  readonly leagueContext = inject(LeagueContextService);

  readonly players = signal<Player[]>([]);
  readonly openAlerts = signal<SellAlert[]>([]);
  readonly transfers = signal<LeagueTransfer[]>([]);
  readonly balanceStatus = signal<LeagueBalanceStatus | null>(null);
  readonly loading = signal(true);
  readonly now = signal(Date.now());
  private readonly countdownTimer = setInterval(() => this.now.set(Date.now()), 1000);

  readonly squadValue = computed(() => this.players().reduce((sum, p) => sum + p.marketValue, 0));

  readonly statusLabel = computed(() => {
    const bs = this.balanceStatus();
    return bs ? STATUS_LABELS[bs.status] : '';
  });

  readonly countdownLabel = computed(() => {
    const bs = this.balanceStatus();
    if (!bs) {
      return '';
    }
    const diffMs = new Date(bs.nextKickoffAt).getTime() - this.now();
    if (diffMs <= 0) {
      return 'Anpfiff läuft';
    }
    const totalSeconds = Math.floor(diffMs / 1000);
    const days = Math.floor(totalSeconds / 86400);
    const hours = Math.floor((totalSeconds % 86400) / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    if (days > 0) {
      return `${days}T ${hours}Std`;
    }
    if (hours > 0) {
      return `${hours}Std ${minutes}Min`;
    }
    const seconds = totalSeconds % 60;
    return `${minutes}Min ${seconds}Sek`;
  });

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
    this.api.getSellAlerts(leagueId).subscribe((alerts) => this.openAlerts.set(alerts));
    this.api.getTransfers(leagueId).subscribe((transfers) => this.transfers.set(transfers.slice(0, 5)));
    this.api
      .getBalanceStatus(leagueId)
      .pipe(catchError(() => of(null)))
      .subscribe((status) => this.balanceStatus.set(status));
  }

  ngOnDestroy(): void {
    clearInterval(this.countdownTimer);
  }

  refresh(): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId);
    }
  }
}
