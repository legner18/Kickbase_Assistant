import { Component, OnInit, inject } from '@angular/core';
import { LeagueContextService } from '../../core/league-context.service';

@Component({
  selector: 'app-archived-leagues',
  imports: [],
  templateUrl: './archived-leagues.component.html',
  styleUrl: './archived-leagues.component.css',
})
export class ArchivedLeaguesComponent implements OnInit {
  readonly leagueContext = inject(LeagueContextService);

  ngOnInit(): void {
    this.leagueContext.ensureArchivedLoaded();
  }
}
