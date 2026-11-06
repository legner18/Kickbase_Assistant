import { DecimalPipe } from '@angular/common';
import { Component, computed, input, output, signal } from '@angular/core';
import { FairValueRadar, FairValueRadarPlayer, POSITION_LABELS } from '../../core/models';
import { SignedNumberPipe } from '../../shared/signed-number.pipe';

export type RadarScope = 'all' | 'market' | 'free' | 'own';

interface ValueBin {
  key: string;
  label: string;
  color: string;
  minimumPercent: number;
}

const VALUE_BINS: ValueBin[] = [
  { key: 'strong-above', label: 'deutlich mehr (≥ +30 %)', color: 'var(--series-blue)', minimumPercent: 30 },
  { key: 'above', label: 'mehr (+10 bis +30 %)', color: 'color-mix(in srgb, var(--series-blue) 50%, var(--surface-card))', minimumPercent: 10 },
  { key: 'neutral', label: 'im Rahmen (± 10 %)', color: 'var(--text-muted)', minimumPercent: -10 },
  { key: 'below', label: 'weniger (−10 bis −30 %)', color: 'color-mix(in srgb, var(--series-red) 50%, var(--surface-card))', minimumPercent: -30 },
  { key: 'strong-below', label: 'deutlich weniger (≤ −30 %)', color: 'var(--series-red)', minimumPercent: -Infinity },
];

const CHART_WIDTH = 720;
const CHART_HEIGHT = 380;
const PAD_LEFT = 48;
const PAD_RIGHT = 16;
const PAD_TOP = 30;
const PAD_BOTTOM = 40;
const HOVER_RADIUS = 12;
const MARKET_VALUE_STEP = 10_000_000;
const POINTS_STEP = 50;

interface RadarDot {
  player: FairValueRadarPlayer;
  x: number;
  y: number;
  color: string;
  binIndex: number;
}

function binIndexFor(deltaPercent: number): number {
  return VALUE_BINS.findIndex((bin) => deltaPercent >= bin.minimumPercent);
}

@Component({
  selector: 'app-fair-value-radar',
  imports: [DecimalPipe, SignedNumberPipe],
  template: `
    <div class="radar-toolbar">
      <select [value]="scope()" (change)="onScopeChange($event)" aria-label="Spielerauswahl">
        <option value="all">Alle Spieler</option>
        <option value="market">Nur Transfermarkt</option>
        <option value="free">Nur freie Spieler</option>
        <option value="own">Nur mein Kader</option>
      </select>
      <span class="muted">{{ dots().length }} Spieler im Graphen</span>
    </div>
    <div class="chart-wrap" (mouseleave)="onChartMouseLeave()">
      <svg
        #chart
        [attr.viewBox]="'0 0 ' + width + ' ' + height"
        role="img"
        aria-label="Marktwert gegen Durchschnittspunkte"
        (mousemove)="onChartMouseMove($event, chart)"
        (click)="onChartClick()"
        [class.has-hover]="hovered() !== null"
      >
        @for (tick of yTicks(); track tick.value) {
          <line class="grid-line" [attr.x1]="plotLeft" [attr.x2]="plotRight" [attr.y1]="tick.y" [attr.y2]="tick.y" />
          <text class="axis-label" [attr.x]="plotLeft - 8" [attr.y]="tick.y + 4" text-anchor="end">{{ tick.value }}</text>
        }
        @for (tick of xTicks(); track tick.value) {
          <text class="axis-label" [attr.x]="tick.x" [attr.y]="plotBottom + 16" text-anchor="middle">{{ tick.value / 1000000 }} M</text>
        }
        <text class="axis-title" [attr.x]="plotRight" [attr.y]="height - 4" text-anchor="end">Marktwert (Mio €)</text>
        <text class="axis-title" x="4" y="12">Ø Punkte</text>
        @if (fairValueLine(); as line) {
          <line class="fair-line" [attr.x1]="line.x1" [attr.y1]="line.y1" [attr.x2]="line.x2" [attr.y2]="line.y2" />
        }
        @for (dot of dots(); track dot.player.id) {
          <circle
            class="dot"
            [class.dot-own]="dot.player.isOwn"
            [attr.cx]="dot.x"
            [attr.cy]="dot.y"
            [attr.r]="dot.player.isOnMarket ? 5.5 : 4"
            [style.fill]="dot.color"
          />
        }
        @if (hovered(); as dot) {
          <circle class="dot-highlight" [attr.cx]="dot.x" [attr.cy]="dot.y" r="8" />
        }
      </svg>
      @if (hovered(); as dot) {
        <div class="chart-tooltip" [style.left.%]="(dot.x / width) * 100" [style.top.%]="(dot.y / height) * 100">
          <div class="tooltip-title">{{ dot.player.name }}</div>
          <div class="muted">{{ positionLabels[dot.player.position] }} · {{ dot.player.teamName ?? '–' }}</div>
          <div>Marktwert {{ dot.player.marketValue | number: '1.0-0' : 'de' }} €</div>
          <div>Ø {{ dot.player.averagePoints }} Punkte · Fair Value {{ dot.player.fairValue | number: '1.0-0' : 'de' }} €</div>
          <div>{{ dot.player.fairValueDeltaPercent | signedNumber }} % gegenüber Marktwert</div>
          <div class="muted">
            {{ dot.player.isOwn ? 'In deinem Kader' : (dot.player.ownerManagerName ?? 'frei') }}{{ dot.player.isOnMarket ? ' · auf dem Transfermarkt' : '' }}
          </div>
        </div>
      }
    </div>
    <div class="radar-legend">
      @for (bin of bins; track bin.key) {
        <span class="legend-item"><span class="legend-dot" [style.background]="bin.color"></span>{{ bin.label }}</span>
      }
      <span class="legend-item"><span class="legend-line"></span>typisch für den Preis</span>
      <span class="legend-item"><span class="legend-dot legend-own"></span>dein Kader</span>
      <span class="legend-item"><span class="legend-dot legend-market"></span>auf dem Markt</span>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .radar-toolbar {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 0.6rem;
      font-size: 0.82rem;
    }

    .chart-wrap {
      position: relative;
    }

    svg {
      width: 100%;
      height: auto;
      display: block;
    }

    svg.has-hover {
      cursor: pointer;
    }

    .grid-line {
      stroke: var(--gridline);
      stroke-width: 1;
    }

    .axis-label {
      fill: var(--text-muted);
      font-size: 11px;
    }

    .axis-title {
      fill: var(--text-secondary);
      font-size: 11px;
    }

    .fair-line {
      stroke: var(--text-secondary);
      stroke-width: 2;
      stroke-dasharray: 6 4;
    }

    .dot {
      stroke: var(--surface-card);
      stroke-width: 1.5;
    }

    .dot-own {
      stroke: var(--text-primary);
      stroke-width: 2;
    }

    .dot-highlight {
      fill: none;
      stroke: var(--text-primary);
      stroke-width: 2;
      pointer-events: none;
    }

    .chart-tooltip {
      position: absolute;
      transform: translate(-50%, calc(-100% - 12px));
      background: var(--surface-card);
      border: 1px solid var(--border);
      border-radius: var(--radius-sm);
      box-shadow: var(--shadow);
      padding: 0.45rem 0.65rem;
      font-size: 0.8rem;
      white-space: nowrap;
      pointer-events: none;
      z-index: 1;
    }

    .tooltip-title {
      font-weight: 600;
    }

    .radar-legend {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem 1rem;
      margin-top: 0.6rem;
      font-size: 0.76rem;
      color: var(--text-secondary);
    }

    .legend-item {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }

    .legend-dot {
      display: inline-block;
      width: 9px;
      height: 9px;
      border-radius: 50%;
    }

    .legend-own {
      background: transparent;
      border: 2px solid var(--text-primary);
    }

    .legend-market {
      width: 11px;
      height: 11px;
      background: var(--text-muted);
    }

    .legend-line {
      display: inline-block;
      width: 18px;
      border-top: 2px dashed var(--text-secondary);
    }
  `,
})
export class FairValueRadarComponent {
  readonly radar = input.required<FairValueRadar>();
  readonly positionFilter = input<number | null>(null);
  readonly playerClick = output<string>();

  protected readonly width = CHART_WIDTH;
  protected readonly height = CHART_HEIGHT;
  protected readonly plotLeft = PAD_LEFT;
  protected readonly plotRight = CHART_WIDTH - PAD_RIGHT;
  protected readonly plotTop = PAD_TOP;
  protected readonly plotBottom = CHART_HEIGHT - PAD_BOTTOM;
  protected readonly bins = VALUE_BINS;
  protected readonly positionLabels = POSITION_LABELS;

  protected readonly scope = signal<RadarScope>('all');
  protected readonly hovered = signal<RadarDot | null>(null);

  private readonly visiblePlayers = computed(() => {
    const position = this.positionFilter();
    const scope = this.scope();
    return this.radar().players.filter((player) => {
      if (position && player.position !== position) {
        return false;
      }
      switch (scope) {
        case 'market':
          return player.isOnMarket;
        case 'free':
          return player.ownerManagerName === null;
        case 'own':
          return player.isOwn;
        default:
          return true;
      }
    });
  });

  private readonly domain = computed(() => {
    const players = this.radar().players;
    const maximumValue = Math.max(MARKET_VALUE_STEP, ...players.map((player) => player.marketValue));
    const maximumPoints = Math.max(POINTS_STEP, ...players.map((player) => player.averagePoints));
    const minimumPoints = Math.min(0, ...players.map((player) => player.averagePoints));
    return {
      maximumValue: Math.ceil(maximumValue / MARKET_VALUE_STEP) * MARKET_VALUE_STEP,
      maximumPoints: Math.ceil(maximumPoints / POINTS_STEP) * POINTS_STEP,
      minimumPoints: Math.floor(minimumPoints / POINTS_STEP) * POINTS_STEP,
    };
  });

  private scaleX(marketValue: number): number {
    return PAD_LEFT + (marketValue / this.domain().maximumValue) * (this.plotRight - PAD_LEFT);
  }

  private scaleY(points: number): number {
    const { maximumPoints, minimumPoints } = this.domain();
    return this.plotBottom - ((points - minimumPoints) / (maximumPoints - minimumPoints)) * (this.plotBottom - PAD_TOP);
  }

  protected readonly xTicks = computed(() => {
    const ticks: { value: number; x: number }[] = [];
    for (let value = 0; value <= this.domain().maximumValue; value += MARKET_VALUE_STEP) {
      ticks.push({ value, x: this.scaleX(value) });
    }
    return ticks;
  });

  protected readonly yTicks = computed(() => {
    const { maximumPoints, minimumPoints } = this.domain();
    const ticks: { value: number; y: number }[] = [];
    for (let value = minimumPoints; value <= maximumPoints; value += POINTS_STEP) {
      ticks.push({ value, y: this.scaleY(value) });
    }
    return ticks;
  });

  protected readonly fairValueLine = computed(() => {
    const { slope, intercept } = this.radar();
    if (slope <= 0) {
      return null;
    }
    const { maximumValue, maximumPoints, minimumPoints } = this.domain();
    const pointsAt = (marketValue: number) => (marketValue - intercept) / slope;
    const valueAt = (points: number) => slope * points + intercept;
    const startValue = Math.max(0, valueAt(minimumPoints));
    const endValue = Math.min(maximumValue, valueAt(maximumPoints));
    if (endValue <= startValue) {
      return null;
    }
    return {
      x1: this.scaleX(startValue),
      y1: this.scaleY(pointsAt(startValue)),
      x2: this.scaleX(endValue),
      y2: this.scaleY(pointsAt(endValue)),
    };
  });

  protected readonly dots = computed<RadarDot[]>(() =>
    this.visiblePlayers()
      .map((player) => {
        const binIndex = binIndexFor(player.fairValueDeltaPercent);
        return {
          player,
          x: this.scaleX(player.marketValue),
          y: this.scaleY(player.averagePoints),
          color: VALUE_BINS[binIndex].color,
          binIndex,
        };
      })
      .sort((first, second) => {
        const firstWeight = first.player.isOwn ? 3 : Math.abs(first.binIndex - 2);
        const secondWeight = second.player.isOwn ? 3 : Math.abs(second.binIndex - 2);
        return firstWeight - secondWeight;
      }),
  );

  onScopeChange(event: Event): void {
    this.scope.set((event.target as HTMLSelectElement).value as RadarScope);
    this.hovered.set(null);
  }

  onChartMouseMove(event: MouseEvent, chart: Element): void {
    const bounds = chart.getBoundingClientRect();
    const chartX = ((event.clientX - bounds.left) / bounds.width) * CHART_WIDTH;
    const chartY = ((event.clientY - bounds.top) / bounds.height) * CHART_HEIGHT;
    let closest: RadarDot | null = null;
    let closestDistance = HOVER_RADIUS;
    for (const dot of this.dots()) {
      const distance = Math.hypot(dot.x - chartX, dot.y - chartY);
      if (distance <= closestDistance) {
        closest = dot;
        closestDistance = distance;
      }
    }
    this.hovered.set(closest);
  }

  onChartMouseLeave(): void {
    this.hovered.set(null);
  }

  onChartClick(): void {
    const dot = this.hovered();
    if (dot) {
      this.playerClick.emit(dot.player.id);
    }
  }
}
