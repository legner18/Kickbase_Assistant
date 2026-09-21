import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { Player, SellAlert } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly leagueContext = inject(LeagueContextService);

  readonly players = signal<Player[]>([]);
  readonly openAlerts = signal<SellAlert[]>([]);

  ngOnInit(): void {
    this.leagueContext.ensureLoaded();
    this.api.getPlayers().subscribe((players) => this.players.set(players));
    this.api.getSellAlerts(false).subscribe((alerts) => this.openAlerts.set(alerts));
  }
}
