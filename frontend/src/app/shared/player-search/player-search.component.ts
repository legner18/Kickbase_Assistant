import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { POSITION_LABELS, PlayerSearchResult } from '../../core/models';

@Component({
  selector: 'app-player-search',
  imports: [],
  templateUrl: './player-search.component.html',
  styleUrl: './player-search.component.css',
})
export class PlayerSearchComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private debounceHandle: ReturnType<typeof setTimeout> | null = null;

  readonly query = signal('');
  readonly results = signal<PlayerSearchResult[]>([]);
  readonly open = signal(false);
  readonly positionLabels = POSITION_LABELS;

  onInput(value: string): void {
    this.query.set(value);
    if (this.debounceHandle) {
      clearTimeout(this.debounceHandle);
    }

    const term = value.trim();
    if (term.length < 2) {
      this.results.set([]);
      this.open.set(false);
      return;
    }

    this.debounceHandle = setTimeout(() => {
      this.api.searchPlayers(term).subscribe({
        next: (results) => {
          this.results.set(results);
          this.open.set(true);
        },
        error: () => this.results.set([]),
      });
    }, 250);
  }

  select(playerId: string): void {
    this.open.set(false);
    this.query.set('');
    this.results.set([]);
    this.router.navigate(['/spieler', playerId]);
  }

  onBlur(): void {
    setTimeout(() => this.open.set(false), 150);
  }
}
