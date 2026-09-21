import { DecimalPipe } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject } from '@angular/core';
import { LeagueContextService } from '../../core/league-context.service';
import { LiveHubService } from '../../core/live-hub.service';

@Component({
  selector: 'app-live',
  imports: [DecimalPipe],
  templateUrl: './live.component.html',
  styleUrl: './live.component.css',
})
export class LiveComponent implements OnInit, OnDestroy {
  readonly leagueContext = inject(LeagueContextService);
  readonly liveHub = inject(LiveHubService);

  readonly standingsForSelectedLeague = computed(() => {
    const update = this.liveHub.lastUpdate();
    const leagueId = this.leagueContext.selectedLeagueId();
    if (!update || update.leagueId !== leagueId) {
      return null;
    }
    return update;
  });

  ngOnInit(): void {
    this.leagueContext.ensureLoaded();
    this.liveHub.connect();
  }

  ngOnDestroy(): void {
    this.liveHub.disconnect();
  }
}
