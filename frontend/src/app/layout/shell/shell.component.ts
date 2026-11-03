import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { LeagueContextService } from '../../core/league-context.service';
import { ThemeService } from '../../core/theme.service';
import { PlayerSearchComponent } from '../../shared/player-search/player-search.component';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, PlayerSearchComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css',
  host: { '(document:keydown.escape)': 'onEscape()' },
})
export class ShellComponent implements OnInit {
  private readonly router = inject(Router);
  readonly leagueContext = inject(LeagueContextService);
  readonly themeService = inject(ThemeService);

  readonly menuOpen = signal(false);

  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.menuOpen.set(false));
  }

  ngOnInit(): void {
    this.leagueContext.ensureLoaded();
  }

  onLeagueChange(event: Event): void {
    const id = (event.target as HTMLSelectElement).value;
    this.leagueContext.selectedLeagueId.set(id);
    this.menuOpen.set(false);
  }

  onMenuClick(): void {
    this.menuOpen.update((open) => !open);
  }

  onBackdropClick(): void {
    this.menuOpen.set(false);
  }

  onEscape(): void {
    this.menuOpen.set(false);
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
