import { Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LeagueContextService } from '../../core/league-context.service';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css',
})
export class ShellComponent implements OnInit {
  readonly leagueContext = inject(LeagueContextService);

  ngOnInit(): void {
    this.leagueContext.ensureLoaded();
  }

  onLeagueChange(event: Event): void {
    const id = (event.target as HTMLSelectElement).value;
    this.leagueContext.selectedLeagueId.set(id);
  }
}
