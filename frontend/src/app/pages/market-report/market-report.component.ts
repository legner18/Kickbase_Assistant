import { DatePipe, DecimalPipe, formatNumber } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { MarketMovementDay, MarketReport, POSITION_LABELS, TopElevenPlayer } from '../../core/models';
import { SignedNumberPipe } from '../../shared/signed-number.pipe';

interface ChartBar {
  day: MarketMovementDay;
  x: number;
  width: number;
  bandX: number;
  bandWidth: number;
  path: string;
  isPositive: boolean;
  labelX: number;
}

const CHART_WIDTH = 720;
const CHART_HEIGHT = 220;
const PAD_LEFT = 64;
const PAD_RIGHT = 8;
const PAD_TOP = 12;
const PAD_BOTTOM = 26;
const BAR_GAP = 2;
const MAX_BAR_WIDTH = 36;
const CORNER_RADIUS = 4;

function roundedBarPath(x: number, width: number, baselineY: number, endY: number): string {
  const height = Math.abs(endY - baselineY);
  const radius = Math.min(CORNER_RADIUS, width / 2, height);
  const direction = endY < baselineY ? 1 : -1;
  const cornerY = endY + direction * radius;
  return [
    `M${x},${baselineY}`,
    `L${x},${cornerY}`,
    `Q${x},${endY} ${x + radius},${endY}`,
    `L${x + width - radius},${endY}`,
    `Q${x + width},${endY} ${x + width},${cornerY}`,
    `L${x + width},${baselineY}`,
    'Z',
  ].join(' ');
}

export function formatMillions(value: number): string {
  const formatted = formatNumber(value / 1_000_000, 'de', '1.2-2');
  return `${value > 0 ? '+' : ''}${formatted} Mio €`;
}

@Component({
  selector: 'app-market-report',
  imports: [DatePipe, DecimalPipe, SignedNumberPipe],
  templateUrl: './market-report.component.html',
  styleUrl: './market-report.component.css',
})
export class MarketReportComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly leagueContext = inject(LeagueContextService);

  readonly report = signal<MarketReport | null>(null);
  readonly loading = signal(true);
  readonly hoverIndex = signal<number | null>(null);

  readonly chartWidth = CHART_WIDTH;
  readonly chartHeight = CHART_HEIGHT;
  readonly plotLeft = PAD_LEFT;
  readonly plotRight = CHART_WIDTH - PAD_RIGHT;
  readonly positionLabels = POSITION_LABELS;
  readonly formatMillions = formatMillions;

  private readonly valueRange = computed(() => {
    const changes = (this.report()?.history ?? []).map((day) => day.totalChange);
    const minimum = Math.min(0, ...changes);
    const maximum = Math.max(0, ...changes);
    return { minimum, maximum, span: maximum - minimum || 1 };
  });

  private scaleY(value: number): number {
    const { maximum, span } = this.valueRange();
    const plotHeight = CHART_HEIGHT - PAD_TOP - PAD_BOTTOM;
    return PAD_TOP + ((maximum - value) / span) * plotHeight;
  }

  readonly baselineY = computed(() => this.scaleY(0));

  readonly yTicks = computed(() => {
    const { minimum, maximum } = this.valueRange();
    const ticks = [maximum, 0];
    if (minimum < 0) {
      ticks.push(minimum);
    }
    return ticks.filter((tick, index) => index === 1 || tick !== 0).map((tick) => ({ value: tick, y: this.scaleY(tick) }));
  });

  readonly bars = computed<ChartBar[]>(() => {
    const history = this.report()?.history ?? [];
    if (history.length === 0) {
      return [];
    }
    const bandWidth = (CHART_WIDTH - PAD_LEFT - PAD_RIGHT) / history.length;
    const barWidth = Math.min(MAX_BAR_WIDTH, bandWidth - BAR_GAP);
    const baselineY = this.baselineY();
    return history.map((day, index) => {
      const bandX = PAD_LEFT + index * bandWidth;
      const x = bandX + (bandWidth - barWidth) / 2;
      return {
        day,
        x,
        width: barWidth,
        bandX,
        bandWidth,
        path: roundedBarPath(x, barWidth, baselineY, this.scaleY(day.totalChange)),
        isPositive: day.totalChange >= 0,
        labelX: x + barWidth / 2,
      };
    });
  });

  readonly hoveredBar = computed(() => {
    const index = this.hoverIndex();
    return index === null ? null : (this.bars()[index] ?? null);
  });

  readonly topElevenRows = computed(() => {
    const players = this.report()?.topEleven?.players ?? [];
    return [4, 3, 2, 1]
      .map((position) => ({
        position,
        players: players.filter((player: TopElevenPlayer) => player.position === position),
      }))
      .filter((row) => row.players.length > 0);
  });

  constructor() {
    this.leagueContext.ensureLoaded();
    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (leagueId) {
        this.load(leagueId);
      }
    });
  }

  load(leagueId: string, matchday?: number): void {
    this.loading.set(true);
    this.api.getMarketReport(leagueId, matchday).subscribe((report) => {
      this.report.set(report);
      this.loading.set(false);
    });
  }

  onRefreshClick(): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId, this.report()?.topEleven?.matchdayNumber);
    }
  }

  onMatchdayChange(event: Event): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId, Number((event.target as HTMLSelectElement).value));
    }
  }

  onBarMouseEnter(index: number): void {
    this.hoverIndex.set(index);
  }

  onChartMouseLeave(): void {
    this.hoverIndex.set(null);
  }

  onPlayerClick(playerId: string): void {
    this.router.navigate(['/spieler', playerId]);
  }

  tooltipLeftPercent(bar: ChartBar): number {
    return ((bar.x + bar.width / 2) / CHART_WIDTH) * 100;
  }
}
