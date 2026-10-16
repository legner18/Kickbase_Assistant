import { DatePipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { SellAlert } from '../../core/models';

const SIGNAL_LABELS: Record<string, string> = {
  MarketValueDrop: 'Marktwert fällt',
  LineupDowngrade: 'Startelf-Downgrade',
  InjuryNews: 'Verletzungs-News',
  TrendFlattening: 'Trend flacht ab',
};

@Component({
  selector: 'app-sell-alerts',
  imports: [DatePipe],
  templateUrl: './sell-alerts.component.html',
  styleUrl: './sell-alerts.component.css',
})
export class SellAlertsComponent {
  private readonly api = inject(ApiService);
  readonly leagueContext = inject(LeagueContextService);

  readonly alerts = signal<SellAlert[]>([]);
  readonly loading = signal(true);
  readonly signalLabels = SIGNAL_LABELS;

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
    this.api.getSellAlerts(leagueId).subscribe((alerts) => {
      this.alerts.set(alerts);
      this.loading.set(false);
    });
  }

  refresh(): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId);
    }
  }

  acknowledge(id: number): void {
    this.api.acknowledgeSellAlert(id).subscribe(() => {
      this.alerts.update((current) => current.filter((a) => a.id !== id));
    });
  }
}
