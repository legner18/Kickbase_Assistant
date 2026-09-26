import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import {
  LINEUP_STATUS_LABELS,
  MarketValueHistoryPoint,
  PlayerComparables,
  POSITION_LABELS,
  PlayerDetail,
} from '../../core/models';
import { ValueChartComponent } from '../../shared/value-chart/value-chart.component';

const SIGNAL_LABELS: Record<string, string> = {
  MarketValueDrop: 'Marktwert fällt',
  LineupDowngrade: 'Startelf-Downgrade',
  InjuryNews: 'Verletzungs-News',
};

@Component({
  selector: 'app-player-detail',
  imports: [DecimalPipe, DatePipe, RouterLink, ValueChartComponent],
  templateUrl: './player-detail.component.html',
  styleUrl: './player-detail.component.css',
})
export class PlayerDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ApiService);

  readonly player = signal<PlayerDetail | null>(null);
  readonly history = signal<MarketValueHistoryPoint[]>([]);
  readonly comparables = signal<PlayerComparables | null>(null);
  readonly loading = signal(true);

  readonly positionLabels = POSITION_LABELS;
  readonly lineupLabels = LINEUP_STATUS_LABELS;
  readonly signalLabels = SIGNAL_LABELS;

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      if (id) {
        this.load(id);
      }
    });
  }

  load(id: string): void {
    this.loading.set(true);
    this.player.set(null);
    this.comparables.set(null);
    this.api.getPlayerDetail(id).subscribe((detail) => {
      this.player.set(detail);
      this.loading.set(false);
    });
    this.api.getMarketValueHistory(id).subscribe((history) => this.history.set(history));
    this.api.getPlayerComparables(id).subscribe((comparables) => this.comparables.set(comparables));
  }

  lineupBadgeClass(status: string): string {
    switch (status) {
      case 'Starting':
        return 'badge-good';
      case 'Doubtful':
        return 'badge-warning';
      case 'Bench':
        return 'badge-neutral';
      case 'Out':
        return 'badge-critical';
      default:
        return 'badge-neutral';
    }
  }

  latestChange(): number | null {
    const h = this.history();
    return h.length > 0 ? h[h.length - 1].change : null;
  }

  firstValue(): number | null {
    const h = this.history();
    return h.length > 0 ? h[0].marketValue : null;
  }

  totalChangePercent(): number | null {
    const h = this.history();
    if (h.length < 2 || h[0].marketValue === 0) return null;
    return ((h[h.length - 1].marketValue - h[0].marketValue) / h[0].marketValue) * 100;
  }
}
