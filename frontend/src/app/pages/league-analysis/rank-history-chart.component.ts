import { DecimalPipe } from '@angular/common';
import { Component, computed, input, signal } from '@angular/core';
import { LeagueOverviewManager } from '../../core/models';

export type RankChartMode = 'rank' | 'points';

const SERIES_COLORS = [
  'var(--series-blue)',
  'var(--series-orange)',
  'var(--series-aqua)',
  'var(--series-yellow)',
  'var(--series-magenta)',
  'var(--series-green)',
  'var(--series-violet)',
  'var(--series-red)',
];
const OTHER_COLOR = 'var(--text-muted)';

const CHART_WIDTH = 720;
const CHART_HEIGHT = 260;
const PAD_LEFT = 48;
const PAD_RIGHT = 104;
const PAD_TOP = 16;
const PAD_BOTTOM = 28;
const LABEL_SPACING = 14;

interface ChartSeries {
  manager: LeagueOverviewManager;
  color: string;
  points: { x: number; y: number; matchdayNumber: number; rank: number; cumulativePoints: number; matchdayPoints: number }[];
  path: string;
  labelY: number;
}

@Component({
  selector: 'app-rank-history-chart',
  imports: [DecimalPipe],
  template: `
    <div class="legend">
      @for (series of series(); track series.manager.managerId) {
        <span class="legend-item">
          <span class="legend-swatch" [style.background]="series.color"></span>
          {{ series.manager.managerName }}{{ series.manager.isMe ? ' (ich)' : '' }}
        </span>
      }
    </div>
    <div class="chart-wrap" (mouseleave)="onChartMouseLeave()">
      <svg
        #chart
        [attr.viewBox]="'0 0 ' + width + ' ' + height"
        role="img"
        [attr.aria-label]="mode() === 'rank' ? 'Platzierung je Spieltag' : 'Gesamtpunkte je Spieltag'"
        (mousemove)="onChartMouseMove($event, chart)"
      >
        @for (tick of yTicks(); track tick.label) {
          <line class="grid-line" [attr.x1]="plotLeft" [attr.x2]="plotRight" [attr.y1]="tick.y" [attr.y2]="tick.y" />
          <text class="axis-label" [attr.x]="plotLeft - 8" [attr.y]="tick.y + 4" text-anchor="end">{{ tick.label }}</text>
        }
        @for (tick of xTicks(); track tick.matchdayNumber) {
          <text class="axis-label" [attr.x]="tick.x" [attr.y]="height - 8" text-anchor="middle">ST {{ tick.matchdayNumber }}</text>
        }
        @if (hoveredIndex() !== null && xTicks()[hoveredIndex()!]; as tick) {
          <line class="crosshair" [attr.x1]="tick.x" [attr.x2]="tick.x" [attr.y1]="padTop" [attr.y2]="height - padBottom" />
        }
        @for (series of series(); track series.manager.managerId) {
          <path class="series-line" [attr.d]="series.path" [style.stroke]="series.color" />
          @for (point of series.points; track point.matchdayNumber) {
            <circle class="series-marker" [attr.cx]="point.x" [attr.cy]="point.y" r="4" [style.fill]="series.color" />
          }
          @if (series.points.length > 0) {
            <text class="direct-label" [attr.x]="series.points[series.points.length - 1].x + 10" [attr.y]="series.labelY + 4">
              {{ series.manager.managerName }}
            </text>
          }
        }
      </svg>
      @if (tooltip(); as content) {
        <div class="chart-tooltip" [style.left.%]="content.leftPercent">
          <div class="tooltip-title">Spieltag {{ content.matchdayNumber }}</div>
          @for (entry of content.entries; track entry.managerId) {
            <div class="tooltip-row">
              <span class="legend-swatch" [style.background]="entry.color"></span>
              <span>{{ entry.rank }}. {{ entry.managerName }}</span>
              <span class="tooltip-value">
                {{ entry.cumulativePoints | number: '1.0-0' : 'de' }} ({{ entry.matchdayPoints >= 0 ? '+' : '' }}{{ entry.matchdayPoints | number: '1.0-0' : 'de' }})
              </span>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .legend {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem 1rem;
      font-size: 0.8rem;
      color: var(--text-secondary);
      margin-bottom: 0.5rem;
    }

    .legend-item {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }

    .legend-swatch {
      display: inline-block;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      flex-shrink: 0;
    }

    .chart-wrap {
      position: relative;
    }

    svg {
      width: 100%;
      height: auto;
      display: block;
    }

    .grid-line {
      stroke: var(--gridline);
      stroke-width: 1;
    }

    .axis-label {
      fill: var(--text-muted);
      font-size: 11px;
    }

    .crosshair {
      stroke: var(--text-muted);
      stroke-width: 1;
      stroke-dasharray: 3 3;
    }

    .series-line {
      fill: none;
      stroke-width: 2;
      stroke-linecap: round;
      stroke-linejoin: round;
    }

    .series-marker {
      stroke: var(--surface-card);
      stroke-width: 2;
    }

    .direct-label {
      fill: var(--text-secondary);
      font-size: 12px;
    }

    .chart-tooltip {
      position: absolute;
      top: 0;
      transform: translateX(-50%);
      background: var(--surface-card);
      border: 1px solid var(--border);
      border-radius: var(--radius-sm);
      box-shadow: var(--shadow);
      padding: 0.45rem 0.65rem;
      font-size: 0.8rem;
      white-space: nowrap;
      pointer-events: none;
    }

    .tooltip-title {
      font-weight: 600;
      margin-bottom: 0.2rem;
    }

    .tooltip-row {
      display: flex;
      align-items: center;
      gap: 0.4rem;
    }

    .tooltip-value {
      margin-left: auto;
      padding-left: 0.8rem;
      font-variant-numeric: tabular-nums;
      color: var(--text-secondary);
    }
  `,
})
export class RankHistoryChartComponent {
  readonly managers = input.required<LeagueOverviewManager[]>();
  readonly matchdays = input.required<number[]>();
  readonly mode = input<RankChartMode>('rank');

  protected readonly width = CHART_WIDTH;
  protected readonly height = CHART_HEIGHT;
  protected readonly padTop = PAD_TOP;
  protected readonly padBottom = PAD_BOTTOM;
  protected readonly plotLeft = PAD_LEFT;
  protected readonly plotRight = CHART_WIDTH - PAD_RIGHT;

  protected readonly hoveredIndex = signal<number | null>(null);

  private readonly pointsAxis = computed(() => {
    const maximum = Math.max(1, ...this.managers().flatMap((manager) => manager.standings.map((standing) => standing.cumulativePoints)));
    const roughStep = maximum / 4;
    const magnitude = Math.pow(10, Math.floor(Math.log10(roughStep)));
    const step = [1, 2, 2.5, 5, 10].map((factor) => factor * magnitude).find((candidate) => candidate >= roughStep) ?? roughStep;
    return { step, top: Math.ceil(maximum / step) * step };
  });

  private maximumPoints(): number {
    return this.pointsAxis().top;
  }

  private xFor(index: number): number {
    const count = this.matchdays().length;
    const plotWidth = this.plotRight - this.plotLeft;
    return count <= 1 ? this.plotLeft + plotWidth / 2 : this.plotLeft + (index / (count - 1)) * plotWidth;
  }

  private yFor(rank: number, cumulativePoints: number): number {
    const plotHeight = CHART_HEIGHT - PAD_TOP - PAD_BOTTOM;
    if (this.mode() === 'points') {
      return PAD_TOP + plotHeight - (cumulativePoints / this.maximumPoints()) * plotHeight;
    }
    const maximumRank = Math.max(1, this.managers().length);
    return maximumRank <= 1 ? PAD_TOP + plotHeight / 2 : PAD_TOP + ((rank - 1) / (maximumRank - 1)) * plotHeight;
  }

  protected readonly xTicks = computed(() =>
    this.matchdays().map((matchdayNumber, index) => ({ matchdayNumber, x: this.xFor(index) })),
  );

  protected readonly yTicks = computed(() => {
    if (this.mode() === 'points') {
      const { step, top } = this.pointsAxis();
      const ticks: { label: string; y: number }[] = [];
      for (let value = 0; value <= top; value += step) {
        ticks.push({ label: value.toLocaleString('de-DE'), y: this.yFor(0, value) });
      }
      return ticks;
    }
    return this.managers().map((manager, index) => ({ label: `${index + 1}.`, y: this.yFor(index + 1, 0) }));
  });

  protected readonly series = computed<ChartSeries[]>(() => {
    const matchdayIndex = new Map(this.matchdays().map((matchdayNumber, index) => [matchdayNumber, index]));
    const orderedManagers = [...this.managers()].sort((first, second) => first.managerId.localeCompare(second.managerId));
    const series = orderedManagers.map((manager, index) => {
      const points = manager.standings
        .filter((standing) => matchdayIndex.has(standing.matchdayNumber))
        .map((standing) => ({
          x: this.xFor(matchdayIndex.get(standing.matchdayNumber)!),
          y: this.yFor(standing.rank, standing.cumulativePoints),
          matchdayNumber: standing.matchdayNumber,
          rank: standing.rank,
          cumulativePoints: standing.cumulativePoints,
          matchdayPoints: standing.matchdayPoints,
        }));
      return {
        manager,
        color: index < SERIES_COLORS.length ? SERIES_COLORS[index] : OTHER_COLOR,
        points,
        path: points.map((point, pointIndex) => `${pointIndex === 0 ? 'M' : 'L'}${point.x.toFixed(1)},${point.y.toFixed(1)}`).join(' '),
        labelY: points.length > 0 ? points[points.length - 1].y : 0,
      };
    });

    const byLabelY = [...series].sort((first, second) => first.labelY - second.labelY);
    for (let index = 1; index < byLabelY.length; index++) {
      const minimumY = byLabelY[index - 1].labelY + LABEL_SPACING;
      if (byLabelY[index].labelY < minimumY) {
        byLabelY[index].labelY = minimumY;
      }
    }
    return series;
  });

  protected readonly tooltip = computed(() => {
    const index = this.hoveredIndex();
    if (index === null) {
      return null;
    }
    const matchdayNumber = this.matchdays()[index];
    const entries = this.series()
      .map((series) => {
        const point = series.points.find((candidate) => candidate.matchdayNumber === matchdayNumber);
        return point
          ? {
              managerId: series.manager.managerId,
              managerName: series.manager.managerName,
              color: series.color,
              rank: point.rank,
              cumulativePoints: point.cumulativePoints,
              matchdayPoints: point.matchdayPoints,
            }
          : null;
      })
      .filter((entry): entry is NonNullable<typeof entry> => entry !== null)
      .sort((first, second) => first.rank - second.rank);
    return { matchdayNumber, entries, leftPercent: (this.xFor(index) / CHART_WIDTH) * 100 };
  });

  onChartMouseMove(event: MouseEvent, chart: Element): void {
    const count = this.matchdays().length;
    if (count === 0) {
      return;
    }
    const bounds = chart.getBoundingClientRect();
    const chartX = ((event.clientX - bounds.left) / bounds.width) * CHART_WIDTH;
    let closestIndex = 0;
    for (let index = 1; index < count; index++) {
      if (Math.abs(this.xFor(index) - chartX) < Math.abs(this.xFor(closestIndex) - chartX)) {
        closestIndex = index;
      }
    }
    this.hoveredIndex.set(closestIndex);
  }

  onChartMouseLeave(): void {
    this.hoveredIndex.set(null);
  }
}
