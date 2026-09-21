import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { LINEUP_STATUS_LABELS, POSITION_LABELS, Player } from '../../core/models';

@Component({
  selector: 'app-squad',
  imports: [DecimalPipe],
  templateUrl: './squad.component.html',
  styleUrl: './squad.component.css',
})
export class SquadComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly players = signal<Player[]>([]);
  readonly loading = signal(true);

  readonly positionLabels = POSITION_LABELS;
  readonly lineupLabels = LINEUP_STATUS_LABELS;

  ngOnInit(): void {
    this.api.getPlayers().subscribe((players) => {
      this.players.set(players);
      this.loading.set(false);
    });
  }

  lineupBadgeClass(status: string | null): string {
    switch (status) {
      case 'Starting':
        return 'badge-ok';
      case 'Doubtful':
        return 'badge-warning';
      case 'Bench':
      case 'Out':
        return 'badge-danger';
      default:
        return 'badge-neutral';
    }
  }
}
