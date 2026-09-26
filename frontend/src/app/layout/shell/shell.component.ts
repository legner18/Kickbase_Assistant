import { Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LeagueContextService } from '../../core/league-context.service';
import { ThemeService } from '../../core/theme.service';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css',
})
export class ShellComponent implements OnInit {
  readonly leagueContext = inject(LeagueContextService);
  readonly themeService = inject(ThemeService);

  ngOnInit(): void {
    this.leagueContext.ensureLoaded();
  }

  onLeagueChange(event: Event): void {
    const id = (event.target as HTMLSelectElement).value;
    this.leagueContext.selectedLeagueId.set(id);
  }

  themeIcon(): string {
    const t = this.themeService.theme();
    return t === 'dark' ? 'moon' : t === 'light' ? 'sun' : 'auto';
  }

  themeLabel(): string {
    const t = this.themeService.theme();
    return t === 'dark' ? 'Dunkel' : t === 'light' ? 'Hell' : 'System';
  }
}
