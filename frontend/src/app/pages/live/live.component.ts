import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LeagueContextService } from '../../core/league-context.service';
import { LiveHubService } from '../../core/live-hub.service';
import { LIVE_STALE_AFTER_MS, createNowSignal } from '../../core/live-refresh';
import { LiveLeagueComponent } from './live-league/live-league.component';
import { LiveMatchesComponent } from './live-matches/live-matches.component';

type LiveTab = 'spiele' | 'liga';

interface WorkerWarning {
  pausedUntil: string | null;
  lastSuccessAt: string | null;
}

@Component({
  selector: 'app-live',
  imports: [DatePipe, LiveLeagueComponent, LiveMatchesComponent],
  templateUrl: './live.component.html',
  styleUrl: './live.component.css',
})
export class LiveComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly now = createNowSignal();
  readonly leagueContext = inject(LeagueContextService);
  readonly liveHub = inject(LiveHubService);

  readonly activeTab = signal<LiveTab>(this.route.snapshot.queryParamMap.get('tab') === 'liga' ? 'liga' : 'spiele');

  readonly workerWarning = computed<WorkerWarning | null>(() => {
    if (this.liveHub.connectionState() !== 'connected') {
      return null;
    }

    const now = this.now();
    const status = this.liveHub.liveStatus();
    if (!status) {
      const connectedAt = this.liveHub.connectedAt();
      return connectedAt !== null && now - connectedAt > LIVE_STALE_AFTER_MS ? { pausedUntil: null, lastSuccessAt: null } : null;
    }

    if (status.pausedUntil && Date.parse(status.pausedUntil) > now) {
      return { pausedUntil: status.pausedUntil, lastSuccessAt: status.lastSuccessAt };
    }

    if (!status.lastSuccessAt || now - Date.parse(status.lastSuccessAt) > LIVE_STALE_AFTER_MS) {
      return { pausedUntil: null, lastSuccessAt: status.lastSuccessAt };
    }

    return null;
  });

  ngOnInit(): void {
    this.leagueContext.ensureLoaded();
    this.liveHub.connect();
  }

  ngOnDestroy(): void {
    this.liveHub.disconnect();
  }

  onTabClick(tab: LiveTab): void {
    this.activeTab.set(tab);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab: tab === 'liga' ? 'liga' : null },
      replaceUrl: true,
    });
  }
}
