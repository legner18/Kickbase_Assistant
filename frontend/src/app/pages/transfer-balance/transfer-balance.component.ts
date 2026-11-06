import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { TransferBalance } from '../../core/models';
import { SignedNumberPipe } from '../../shared/signed-number.pipe';

@Component({
  selector: 'app-transfer-balance',
  imports: [DatePipe, DecimalPipe, SignedNumberPipe],
  templateUrl: './transfer-balance.component.html',
  styleUrl: './transfer-balance.component.css',
})
export class TransferBalanceComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly leagueContext = inject(LeagueContextService);

  readonly balance = signal<TransferBalance | null>(null);
  readonly loading = signal(true);

  readonly hitRatePercent = computed(() => {
    const data = this.balance();
    return data && data.salesCount > 0 ? (data.profitableSalesCount / data.salesCount) * 100 : null;
  });

  readonly missingValueNames = computed(() => {
    const data = this.balance();
    return data ? [...data.salesWithoutAcquisitionValue, ...data.squadPlayersWithoutAcquisitionValue] : [];
  });

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
    this.api.getTransferBalance(leagueId).subscribe((balance) => {
      this.balance.set(balance);
      this.loading.set(false);
    });
  }

  onRefreshClick(): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId);
    }
  }

  onPlayerClick(playerId: string): void {
    this.router.navigate(['/spieler', playerId]);
  }
}
