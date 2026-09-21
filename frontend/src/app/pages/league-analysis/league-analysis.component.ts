import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { LeagueTransfer, ManagerStat } from '../../core/models';

@Component({
  selector: 'app-league-analysis',
  imports: [DecimalPipe, DatePipe],
  templateUrl: './league-analysis.component.html',
  styleUrl: './league-analysis.component.css',
})
export class LeagueAnalysisComponent {
  private readonly api = inject(ApiService);
  readonly leagueContext = inject(LeagueContextService);

  readonly managerStats = signal<ManagerStat[]>([]);
  readonly transfers = signal<LeagueTransfer[]>([]);
  readonly loading = signal(true);

  constructor() {
    this.leagueContext.ensureLoaded();

    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (!leagueId) {
        return;
      }

      this.loading.set(true);
      this.api.getManagerStats(leagueId).subscribe((stats) => this.managerStats.set(stats));
      this.api.getTransfers(leagueId).subscribe((transfers) => {
        this.transfers.set(transfers);
        this.loading.set(false);
      });
    });
  }
}
