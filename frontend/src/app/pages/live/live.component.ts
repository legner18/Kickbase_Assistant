import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LeagueContextService } from '../../core/league-context.service';
import { LiveHubService } from '../../core/live-hub.service';
import { LiveLeagueComponent } from './live-league/live-league.component';
import { LiveMatchesComponent } from './live-matches/live-matches.component';

type LiveTab = 'spiele' | 'liga';

@Component({
  selector: 'app-live',
  imports: [LiveLeagueComponent, LiveMatchesComponent],
  templateUrl: './live.component.html',
  styleUrl: './live.component.css',
})
export class LiveComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly leagueContext = inject(LeagueContextService);
  readonly liveHub = inject(LiveHubService);

  readonly activeTab = signal<LiveTab>(this.route.snapshot.queryParamMap.get('tab') === 'liga' ? 'liga' : 'spiele');

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
