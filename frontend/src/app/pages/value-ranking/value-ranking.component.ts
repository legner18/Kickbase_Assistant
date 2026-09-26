import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { POSITION_LABELS, ValueRankingEntry } from '../../core/models';

@Component({
  selector: 'app-value-ranking',
  imports: [DecimalPipe],
  templateUrl: './value-ranking.component.html',
  styleUrl: './value-ranking.component.css',
})
export class ValueRankingComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly entries = signal<ValueRankingEntry[]>([]);
  readonly loading = signal(true);
  readonly search = signal('');
  readonly positionFilter = signal<number | null>(null);

  readonly positionLabels = POSITION_LABELS;

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) return this.entries();
    return this.entries().filter(
      (e) => e.name.toLowerCase().includes(term) || (e.teamName ?? '').toLowerCase().includes(term),
    );
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getValueRanking(this.positionFilter() ?? undefined).subscribe((entries) => {
      this.entries.set(entries);
      this.loading.set(false);
    });
  }

  onPositionChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.positionFilter.set(value ? Number(value) : null);
    this.load();
  }

  openPlayer(playerId: string): void {
    this.router.navigate(['/spieler', playerId]);
  }
}
