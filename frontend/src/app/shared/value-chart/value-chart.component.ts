import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, input, signal } from '@angular/core';
import { MarketValueHistoryPoint } from '../../core/models';

@Component({
  selector: 'app-value-chart',
  imports: [DecimalPipe, DatePipe],
  templateUrl: './value-chart.component.html',
  styleUrl: './value-chart.component.css',
})
export class ValueChartComponent {
  readonly points = input.required<MarketValueHistoryPoint[]>();

  protected readonly width = 720;
  protected readonly height = 220;
  protected readonly padLeft = 8;
  protected readonly padRight = 8;
  protected readonly padTop = 12;
  protected readonly padBottom = 24;

  protected readonly hoverIndex = signal<number | null>(null);

  protected readonly scaled = computed(() => {
    const pts = this.points();
    if (pts.length === 0) {
      return [];
    }
    const values = pts.map((p) => p.marketValue);
    const min = Math.min(...values);
    const max = Math.max(...values);
    const range = max - min || 1;
    const plotW = this.width - this.padLeft - this.padRight;
    const plotH = this.height - this.padTop - this.padBottom;

    return pts.map((p, i) => ({
      x: this.padLeft + (pts.length === 1 ? plotW / 2 : (i / (pts.length - 1)) * plotW),
      y: this.padTop + plotH - ((p.marketValue - min) / range) * plotH,
      point: p,
    }));
  });

  protected readonly linePath = computed(() =>
    this.scaled()
      .map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)},${p.y.toFixed(1)}`)
      .join(' '),
  );

  protected readonly areaPath = computed(() => {
    const s = this.scaled();
    if (s.length === 0) return '';
    const baseline = this.height - this.padBottom;
    return `M${s[0].x.toFixed(1)},${baseline} ` + s.map((p) => `L${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ') + ` L${s[s.length - 1].x.toFixed(1)},${baseline} Z`;
  });

  protected readonly overallTrendUp = computed(() => {
    const pts = this.points();
    return pts.length > 0 && pts[pts.length - 1].marketValue >= pts[0].marketValue;
  });

  protected readonly hovered = computed(() => {
    const idx = this.hoverIndex();
    return idx === null ? null : this.scaled()[idx];
  });

  onMouseMove(event: MouseEvent, svg: SVGSVGElement): void {
    const rect = svg.getBoundingClientRect();
    const scaleX = this.width / rect.width;
    const relX = (event.clientX - rect.left) * scaleX;

    const s = this.scaled();
    if (s.length === 0) return;

    let closest = 0;
    let closestDist = Infinity;
    s.forEach((p, i) => {
      const d = Math.abs(p.x - relX);
      if (d < closestDist) {
        closestDist = d;
        closest = i;
      }
    });
    this.hoverIndex.set(closest);
  }

  onMouseLeave(): void {
    this.hoverIndex.set(null);
  }
}
