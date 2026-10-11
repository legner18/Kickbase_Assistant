import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
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
export class SellAlertsComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly alerts = signal<SellAlert[]>([]);
  readonly loading = signal(true);
  readonly signalLabels = SIGNAL_LABELS;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getSellAlerts(false).subscribe((alerts) => {
      this.alerts.set(alerts);
      this.loading.set(false);
    });
  }

  acknowledge(id: number): void {
    this.api.acknowledgeSellAlert(id).subscribe(() => {
      this.alerts.update((current) => current.filter((a) => a.id !== id));
    });
  }
}
