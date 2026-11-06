import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { LeagueContextService } from '../../core/league-context.service';
import { POSITION_LABELS, SimulatorData, SimulatorMarketPlayer, SimulatorSquadPlayer } from '../../core/models';
import { SignedNumberPipe } from '../../shared/signed-number.pipe';

const CREDIT_LIMIT_RATE = 0.33;

function creditLimit(teamValue: number, balance: number): number {
  return CREDIT_LIMIT_RATE * (teamValue - Math.max(0, -balance));
}

function sumBy<T>(items: T[], selector: (item: T) => number): number {
  return items.reduce((sum, item) => sum + selector(item), 0);
}

@Component({
  selector: 'app-simulator',
  imports: [DatePipe, DecimalPipe, SignedNumberPipe],
  templateUrl: './simulator.component.html',
  styleUrl: './simulator.component.css',
})
export class SimulatorComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly leagueContext = inject(LeagueContextService);

  readonly data = signal<SimulatorData | null>(null);
  readonly loading = signal(true);
  readonly plannedSales = signal<ReadonlySet<string>>(new Set());
  readonly plannedPurchases = signal<ReadonlyMap<string, number>>(new Map());
  readonly loginBonusDays = signal(0);
  readonly successBonus = signal(0);

  readonly positionLabels = POSITION_LABELS;

  readonly loginBonusOptions = computed(() => {
    const maxDays = this.data()?.loginBonusDays ?? 0;
    return [...Array(maxDays + 1).keys()];
  });

  readonly soldPlayers = computed(() => {
    const sales = this.plannedSales();
    return (this.data()?.squad ?? []).filter((squadPlayer) => sales.has(squadPlayer.playerId));
  });

  readonly remainingPlayers = computed(() => {
    const sales = this.plannedSales();
    return (this.data()?.squad ?? []).filter((squadPlayer) => !sales.has(squadPlayer.playerId));
  });

  readonly purchasedPlayers = computed(() => {
    const purchases = this.plannedPurchases();
    return (this.data()?.market ?? []).filter((listing) => purchases.has(listing.playerId));
  });

  readonly salesTotal = computed(() => sumBy(this.soldPlayers(), (squadPlayer) => squadPlayer.marketValue));
  readonly purchasesTotal = computed(() => sumBy([...this.plannedPurchases().values()], (bid) => bid));
  readonly loginBonusTotal = computed(() => this.loginBonusDays() * (this.data()?.loginBonusPerDay ?? 0));

  readonly balanceNow = computed(() => this.data()?.balance ?? 0);
  readonly balanceAfter = computed(
    () => this.balanceNow() + this.salesTotal() - this.purchasesTotal() + this.loginBonusTotal() + this.successBonus(),
  );

  readonly teamValueNow = computed(() => sumBy(this.data()?.squad ?? [], (squadPlayer) => squadPlayer.marketValue));
  readonly teamValueAfter = computed(
    () =>
      sumBy(this.remainingPlayers(), (squadPlayer) => squadPlayer.marketValue) +
      sumBy(this.purchasedPlayers(), (listing) => listing.marketValue),
  );

  readonly availableNow = computed(() => this.balanceNow() + creditLimit(this.teamValueNow(), this.balanceNow()));
  readonly availableAfter = computed(() => this.balanceAfter() + creditLimit(this.teamValueAfter(), this.balanceAfter()));

  readonly dailyTrendNow = computed(() => sumBy(this.data()?.squad ?? [], (squadPlayer) => squadPlayer.latestChange ?? 0));
  readonly dailyTrendAfter = computed(() => sumBy(this.remainingPlayers(), (squadPlayer) => squadPlayer.latestChange ?? 0));

  readonly predictedNextNow = computed(() => sumBy(this.data()?.squad ?? [], (squadPlayer) => squadPlayer.predictedNextChange));
  readonly predictedNextAfter = computed(() => sumBy(this.remainingPlayers(), (squadPlayer) => squadPlayer.predictedNextChange));

  readonly squadSizeAfter = computed(() => this.remainingPlayers().length + this.purchasedPlayers().length);

  readonly rosterGapsAfter = computed(() => {
    const positions = [
      ...this.remainingPlayers().map((squadPlayer) => squadPlayer.position),
      ...this.purchasedPlayers().map((listing) => listing.position),
    ];
    return (this.data()?.minimumPositions ?? [])
      .map((requirement) => ({
        requirement,
        count: positions.filter((position) => position === requirement.position).length,
      }))
      .filter((entry) => entry.count < entry.requirement.minCount)
      .map((entry) => `${entry.requirement.label}: ${entry.count}/${entry.requirement.minCount}`);
  });

  readonly hasPlannedChanges = computed(
    () => this.plannedSales().size > 0 || this.plannedPurchases().size > 0 || this.loginBonusDays() > 0 || this.successBonus() !== 0,
  );

  constructor() {
    this.leagueContext.ensureLoaded();
    effect(() => {
      const leagueId = this.leagueContext.selectedLeagueId();
      if (leagueId) {
        this.load(leagueId);
      }
    });
  }

  load(leagueId: string): void {
    this.loading.set(true);
    this.api.getSimulator(leagueId).subscribe({
      next: (data) => {
        this.data.set(data);
        this.resetPlan(data);
        this.loading.set(false);
      },
      error: () => {
        this.data.set(null);
        this.loading.set(false);
      },
    });
  }

  onRefreshClick(): void {
    const leagueId = this.leagueContext.selectedLeagueId();
    if (leagueId) {
      this.load(leagueId);
    }
  }

  onResetClick(): void {
    const data = this.data();
    if (data) {
      this.resetPlan(data);
    }
  }

  onSaleChange(squadPlayer: SimulatorSquadPlayer): void {
    this.plannedSales.update((current) => {
      const next = new Set(current);
      if (next.has(squadPlayer.playerId)) {
        next.delete(squadPlayer.playerId);
      } else {
        next.add(squadPlayer.playerId);
      }
      return next;
    });
  }

  onPurchaseChange(listing: SimulatorMarketPlayer): void {
    this.plannedPurchases.update((current) => {
      const next = new Map(current);
      if (next.has(listing.playerId)) {
        next.delete(listing.playerId);
      } else {
        next.set(listing.playerId, listing.ownOfferPrice ?? Math.max(listing.price, listing.marketValue));
      }
      return next;
    });
  }

  onBidInput(listing: SimulatorMarketPlayer, event: Event): void {
    const bid = Number((event.target as HTMLInputElement).value);
    if (!Number.isFinite(bid) || bid < 0) {
      return;
    }
    this.plannedPurchases.update((current) => new Map(current).set(listing.playerId, bid));
  }

  onLoginBonusChange(event: Event): void {
    this.loginBonusDays.set(Number((event.target as HTMLSelectElement).value));
  }

  onSuccessBonusInput(event: Event): void {
    const amount = Number((event.target as HTMLInputElement).value);
    this.successBonus.set(Number.isFinite(amount) ? amount : 0);
  }

  onPlayerClick(playerId: string): void {
    this.router.navigate(['/spieler', playerId]);
  }

  plannedBid(playerId: string): number | undefined {
    return this.plannedPurchases().get(playerId);
  }

  private resetPlan(data: SimulatorData): void {
    this.plannedSales.set(new Set());
    this.plannedPurchases.set(
      new Map(
        data.market
          .filter((listing) => listing.ownOfferPrice !== null)
          .map((listing) => [listing.playerId, listing.ownOfferPrice as number]),
      ),
    );
    this.loginBonusDays.set(0);
    this.successBonus.set(0);
  }
}
