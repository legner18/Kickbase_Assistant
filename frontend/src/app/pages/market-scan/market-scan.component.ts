import { DecimalPipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { MarketScanEntry } from '../../core/models';

@Component({
  selector: 'app-market-scan',
  imports: [DecimalPipe],
  templateUrl: './market-scan.component.html',
  styleUrl: './market-scan.component.css',
})
export class MarketScanComponent {
  private readonly api = inject(ApiService);
  readonly leagueContext = inject(LeagueContextService);

  readonly entries = signal<MarketScanEntry[]>([]);
  readonly loading = signal(true);

  constructor() {
    this.leagueContext.ensureLoaded();

    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (!leagueId) {
        return;
      }

      this.loading.set(true);
      this.api.getMarketScan(leagueId).subscribe((entries) => {
        this.entries.set(entries);
        this.loading.set(false);
      });
    });
  }

  scoreClass(score: number): string {
    if (score >= 65) return 'badge-ok';
    if (score >= 40) return 'badge-warning';
    return 'badge-danger';
  }
}
