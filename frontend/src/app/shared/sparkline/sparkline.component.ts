import { Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-sparkline',
  template: `
    @if (points().length >= 2) {
      <svg [attr.viewBox]="'0 0 ' + width() + ' ' + height()" [class.positive]="trendUp()" [class.negative]="!trendUp()">
        <title>{{ tooltip() }}</title>
        <path [attr.d]="areaPath()" class="spark-area" />
        <path [attr.d]="linePath()" class="spark-line" />
        <circle [attr.cx]="lastPoint().x" [attr.cy]="lastPoint().y" r="2.5" class="spark-dot" />
      </svg>
    } @else {
      <span class="spark-empty">–</span>
    }
  `,
  styles: `
    :host {
      display: inline-block;
      width: 84px;
      height: 28px;
    }
    svg {
      width: 100%;
      height: 100%;
      overflow: visible;
    }
    .spark-line {
      fill: none;
      stroke-width: 1.75;
      stroke-linecap: round;
      stroke-linejoin: round;
    }
    .spark-area {
      stroke: none;
      opacity: 0.12;
    }
    .spark-dot {
      stroke: none;
    }
    svg.positive .spark-line,
    svg.positive .spark-dot {
      stroke: var(--delta-up);
      fill: var(--delta-up);
    }
    svg.positive .spark-area {
      fill: var(--delta-up);
    }
    svg.negative .spark-line,
    svg.negative .spark-dot {
      stroke: var(--delta-down);
      fill: var(--delta-down);
    }
    svg.negative .spark-area {
      fill: var(--delta-down);
    }
    .spark-empty {
      color: var(--text-muted);
    }
  `,
})
export class SparklineComponent {
  readonly values = input.required<number[]>();
  readonly labels = input<string[]>([]);

  protected readonly width = () => 84;
  protected readonly height = () => 28;

  protected readonly points = computed(() => {
    const values = this.values();
    if (values.length < 2) {
      return [];
    }
    const min = Math.min(...values);
    const max = Math.max(...values);
    const range = max - min || 1;
    const w = this.width();
    const h = this.height();
    const pad = 3;
    return values.map((v, i) => ({
      x: (i / (values.length - 1)) * w,
      y: h - pad - ((v - min) / range) * (h - pad * 2),
    }));
  });

  protected readonly trendUp = computed(() => {
    const v = this.values();
    return v.length > 0 && v[v.length - 1] >= v[0];
  });

  protected readonly lastPoint = computed(() => {
    const p = this.points();
    return p[p.length - 1] ?? { x: 0, y: 0 };
  });

  protected readonly linePath = computed(() =>
    this.points()
      .map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)},${p.y.toFixed(1)}`)
      .join(' '),
  );

  protected readonly areaPath = computed(() => {
    const p = this.points();
    if (p.length === 0) {
      return '';
    }
    const h = this.height();
    return `M${p[0].x.toFixed(1)},${h} ` + p.map((pt) => `L${pt.x.toFixed(1)},${pt.y.toFixed(1)}`).join(' ') + ` L${p[p.length - 1].x.toFixed(1)},${h} Z`;
  });

  protected readonly tooltip = computed(() => {
    const values = this.values();
    if (values.length === 0) {
      return '';
    }
    const first = values[0];
    const last = values[values.length - 1];
    const change = last - first;
    const pct = first !== 0 ? (change / first) * 100 : 0;
    const sign = change >= 0 ? '+' : '';
    return `${sign}${change.toLocaleString('de-DE')} € (${sign}${pct.toFixed(1)}%)`;
  });
}
