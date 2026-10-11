import { DecimalPipe } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { LiveHubService } from '../../core/live-hub.service';
import { LiveTransferActivity, MarketScanEntry, POSITION_LABELS } from '../../core/models';

type SortKey = 'playerName' | 'price' | 'marketValueChangePercent' | 'overallScore';
const MAX_RECENT_ACTIVITY = 10;

@Component({
  selector: 'app-market-scan',
  imports: [DecimalPipe],
  templateUrl: './market-scan.component.html',
  styleUrl: './market-scan.component.css',
})
export class MarketScanComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly leagueContext = inject(LeagueContextService);
  readonly liveHub = inject(LiveHubService);

  readonly entries = signal<MarketScanEntry[]>([]);
  readonly loading = signal(true);
  readonly sortKey = signal<SortKey>('overallScore');
  readonly sortDesc = signal(true);
  readonly positionLabels = POSITION_LABELS;
  readonly recentActivity = signal<LiveTransferActivity[]>([]);

  readonly sorted = computed(() => {
    const key = this.sortKey();
    const dir = this.sortDesc() ? -1 : 1;
    return [...this.entries()].sort((a, b) => {
      const av = a[key] ?? -Infinity;
      const bv = b[key] ?? -Infinity;
      if (typeof av === 'string' || typeof bv === 'string') {
        return dir * String(av).localeCompare(String(bv));
      }
      return dir * ((av as number) - (bv as number));
    });
  });

  constructor() {
    this.leagueContext.ensureLoaded();

    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (leagueId) {
        this.load(leagueId);
      }
    });

    // Live-Markt-Updates (LiveMarketJob, alle 60s, nur bei echten Änderungen): Angebots-Änderungen
    // lösen einen schlichten Re-Fetch aus (die Server-Diff-Logik verhindert bereits unnötige
    // Pushes, ein erneutes GET ist hier einfacher als serverseitige Deltas im Frontend nachzubauen).
    // Transfer-Aktivität wird direkt aus dem Push in eine kleine rollierende Liste übernommen, da es
    // dafür keinen REST-Endpoint gibt (rein transient, wie die Live-Punkte-Seite).
    effect(() => {
      const update = this.liveHub.lastMarketUpdate();
      const leagueId = this.leagueContext.selectedLeagueId();
      if (!update || update.leagueId !== leagueId) {
        return;
      }

      if (update.kind === 'listings') {
        this.load(leagueId);
      } else {
        this.recentActivity.update((current) => [...update.transfers, ...current].slice(0, MAX_RECENT_ACTIVITY));
      }
    });
  }

  ngOnInit(): void {
    this.liveHub.connect();
  }

  ngOnDestroy(): void {
    this.liveHub.disconnect();
  }

  load(leagueId: string): void {
    this.loading.set(true);
    this.api.getMarketScan(leagueId).subscribe((entries) => {
      this.entries.set(entries);
      this.loading.set(false);
    });
  }

  refresh(): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId);
    }
  }

  openPlayer(playerId: string): void {
    this.router.navigate(['/spieler', playerId]);
  }

  setSort(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortDesc.update((d) => !d);
    } else {
      this.sortKey.set(key);
      this.sortDesc.set(true);
    }
  }

  // Fixed status hexes (same light/dark per the palette) - returned as real hex, not var(),
  // so callers can append an alpha suffix for badge backgrounds.
  scoreColor(score: number): string {
    if (score >= 65) return '#0ca30c';
    if (score >= 40) return '#fab219';
    return '#d03b3b';
  }

  expiresInLabel(iso: string): string {
    const diffMs = new Date(iso).getTime() - Date.now();
    if (diffMs <= 0) return 'abgelaufen';
    const hours = Math.round(diffMs / 3_600_000);
    if (hours < 1) return '< 1 Std.';
    if (hours < 24) return `${hours} Std.`;
    return `${Math.round(hours / 24)} Tage`;
  }
}
