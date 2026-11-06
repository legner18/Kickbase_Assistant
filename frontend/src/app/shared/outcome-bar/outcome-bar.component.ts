import { DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-outcome-bar',
  imports: [DecimalPipe],
  template: `
    <div class="outcome-bar" role="img" [attr.aria-label]="ariaLabel()">
      <span class="segment segment-home" [style.flex-grow]="homeProbability()"></span>
      <span class="segment segment-draw" [style.flex-grow]="drawProbability()"></span>
      <span class="segment segment-away" [style.flex-grow]="awayProbability()"></span>
    </div>
    <div class="outcome-labels">
      <span><span class="swatch swatch-home"></span>{{ homeLabel() }} {{ homeProbability() * 100 | number: '1.0-0' : 'de' }} %</span>
      <span><span class="swatch swatch-draw"></span>Remis {{ drawProbability() * 100 | number: '1.0-0' : 'de' }} %</span>
      <span><span class="swatch swatch-away"></span>{{ awayLabel() }} {{ awayProbability() * 100 | number: '1.0-0' : 'de' }} %</span>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .outcome-bar {
      display: flex;
      gap: 2px;
      height: 8px;
    }

    .segment {
      flex-basis: 0;
      min-width: 2px;
    }

    .segment-home {
      background: var(--series-blue);
      border-radius: 4px 0 0 4px;
    }

    .segment-draw {
      background: var(--text-muted);
    }

    .segment-away {
      background: var(--series-orange);
      border-radius: 0 4px 4px 0;
    }

    .outcome-labels {
      display: flex;
      justify-content: space-between;
      gap: 0.5rem;
      margin-top: 0.3rem;
      font-size: 0.74rem;
      color: var(--text-secondary);
      font-variant-numeric: tabular-nums;
    }

    .swatch {
      display: inline-block;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      margin-right: 0.3rem;
    }

    .swatch-home {
      background: var(--series-blue);
    }

    .swatch-draw {
      background: var(--text-muted);
    }

    .swatch-away {
      background: var(--series-orange);
    }
  `,
})
export class OutcomeBarComponent {
  readonly homeProbability = input.required<number>();
  readonly drawProbability = input.required<number>();
  readonly awayProbability = input.required<number>();
  readonly homeLabel = input('Heim');
  readonly awayLabel = input('Auswärts');

  protected readonly ariaLabel = computed(
    () =>
      `${this.homeLabel()} ${Math.round(this.homeProbability() * 100)} %, Remis ${Math.round(this.drawProbability() * 100)} %, ` +
      `${this.awayLabel()} ${Math.round(this.awayProbability() * 100)} %`,
  );
}
