import { DatePipe, DecimalPipe, NgStyle } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { LeagueTransfer, ManagerStat } from '../../core/models';

@Component({
  selector: 'app-league-analysis',
  imports: [DecimalPipe, DatePipe, NgStyle],
  templateUrl: './league-analysis.component.html',
  styleUrl: './league-analysis.component.css',
})
export class LeagueAnalysisComponent {
  private readonly api = inject(ApiService);
  readonly leagueContext = inject(LeagueContextService);

  readonly managerStats = signal<ManagerStat[]>([]);
  readonly transfers = signal<LeagueTransfer[]>([]);
  readonly loading = signal(true);

  readonly sortedStats = computed(() => [...this.managerStats()].sort((a, b) => b.estimatedBudget - a.estimatedBudget));

  // Diverging bar anchored at zero - budgets can legitimately go negative in some leagues
  // (confirmed against a real account: Kickbase itself reported a negative budget here), so a
  // plain value/max bar would render a negative budget as a full-width bar. zeroPct is where the
  // shared zero-line sits as a % of the whole track; each bar starts there and grows toward its
  // own sign.
  private readonly budgetRange = computed(() => {
    const values = this.managerStats().map((s) => s.estimatedBudget);
    const min = Math.min(0, ...values);
    const max = Math.max(0, ...values, 1);
    return { min, max, span: max - min };
  });

  readonly zeroPct = computed(() => {
    const { min, span } = this.budgetRange();
    return (-min / span) * 100;
  });

  barStyle(value: number): { left: string; width: string; background: string } {
    const { span } = this.budgetRange();
    const zero = this.zeroPct();
    const widthPct = (Math.abs(value) / span) * 100;
    return {
      left: value >= 0 ? `${zero}%` : `${zero - widthPct}%`,
      width: `${widthPct}%`,
      background: value >= 0 ? 'var(--primary)' : 'var(--status-critical)',
    };
  }

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
    this.api.getManagerStats(leagueId).subscribe((stats) => this.managerStats.set(stats));
    this.api.getTransfers(leagueId).subscribe((transfers) => {
      this.transfers.set(transfers);
      this.loading.set(false);
    });
  }

  refresh(): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId);
    }
  }

  overpayClass(pct: number | null): string {
    if (pct === null) return 'badge-neutral';
    if (pct <= 3) return 'badge-good';
    if (pct <= 10) return 'badge-warning';
    return 'badge-critical';
  }
}
