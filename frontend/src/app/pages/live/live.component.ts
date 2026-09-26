import { DecimalPipe } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, effect, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { LiveHubService } from '../../core/live-hub.service';
import { ManagerStat } from '../../core/models';

@Component({
  selector: 'app-live',
  imports: [DecimalPipe],
  templateUrl: './live.component.html',
  styleUrl: './live.component.css',
})
export class LiveComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  readonly leagueContext = inject(LeagueContextService);
  readonly liveHub = inject(LiveHubService);

  readonly fallbackStats = signal<ManagerStat[]>([]);

  readonly standingsForSelectedLeague = computed(() => {
    const update = this.liveHub.lastUpdate();
    const leagueId = this.leagueContext.selectedLeagueId();
    if (!update || update.leagueId !== leagueId) {
      return null;
    }
    return update;
  });

  // Fallback ("last known standings") for when no live push has arrived yet this session -
  // without it the page shows nothing at all until the Worker's next 90s poll during a live
  // match, even if the page was simply just opened. Sourced from LeagueManagerStat, which
  // LeagueAnalysisJob already refreshes daily from the same ranking endpoint.
  readonly fallbackSorted = computed(() => [...this.fallbackStats()].sort((a, b) => a.matchdayRank - b.matchdayRank));

  constructor() {
    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (leagueId) {
        this.api.getManagerStats(leagueId).subscribe((stats) => this.fallbackStats.set(stats));
      }
    });
  }

  ngOnInit(): void {
    this.leagueContext.ensureLoaded();
    this.liveHub.connect();
  }

  ngOnDestroy(): void {
    this.liveHub.disconnect();
  }
}
