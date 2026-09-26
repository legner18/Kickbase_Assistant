export interface League {
  id: string;
  name: string;
  competitionId: string;
  ownUserId: string;
}

export interface LeagueManager {
  managerId: string;
  managerName: string;
  isMe: boolean;
}

export interface Player {
  id: string;
  name: string;
  teamId: string;
  teamName: string | null;
  position: number;
  marketValue: number;
  totalPoints: number;
  averagePoints: number;
  lastSeenAt: string;
  latestLineupStatus: string | null;
  latestMarketValueChange: number | null;
  sellScore: number | null;
  sellMomentumScore: number | null;
  sellLineupScore: number | null;
  sellFixtureScore: number | null;
}

export interface PlayerSearchResult {
  id: string;
  name: string;
  teamName: string | null;
  position: number;
  marketValue: number;
  totalPoints: number;
}

export interface ComparablePlayer {
  id: string;
  name: string;
  teamName: string | null;
  marketValue: number;
  totalPoints: number;
  averagePoints: number;
}

export interface PlayerComparables {
  similarByValue: ComparablePlayer[];
  similarByPoints: ComparablePlayer[];
}

export interface ValueRankingEntry {
  id: string;
  name: string;
  teamName: string | null;
  position: number;
  marketValue: number;
  totalPoints: number;
  averagePoints: number;
  pricePerPoint: number;
}

export interface MarketValueHistoryPoint {
  date: string;
  marketValue: number;
  change: number;
}

export interface SellAlert {
  id: number;
  playerId: string;
  playerName: string;
  signal: string;
  reason: string;
  createdAt: string;
  acknowledged: boolean;
}

export interface MarketScanEntry {
  playerId: string;
  playerName: string;
  teamId: string;
  teamName: string | null;
  position: number;
  price: number;
  offerCount: number;
  expiresAt: string;
  momentumScore: number;
  lineupScore: number;
  fixtureScore: number;
  overallScore: number;
  marketValueChangeAbs: number | null;
  marketValueChangePercent: number | null;
  ownerManagerName: string | null;
  scannedAt: string;
}

export interface LineupHistoryEntry {
  status: string;
  scrapedAt: string;
  source: string;
}

export interface PlayerOwner {
  leagueId: string;
  managerId: string;
  managerName: string;
}

export interface PlayerSellAlert {
  signal: string;
  reason: string;
  createdAt: string;
  acknowledged: boolean;
}

export interface PlayerDetail {
  id: string;
  name: string;
  teamId: string;
  teamName: string | null;
  position: number;
  marketValue: number;
  totalPoints: number;
  averagePoints: number;
  lastSeenAt: string;
  lineupHistory: LineupHistoryEntry[];
  sellAlerts: PlayerSellAlert[];
  owners: PlayerOwner[];
}

export interface ManagerStat {
  id: number;
  leagueId: string;
  managerId: string;
  managerName: string;
  estimatedBudget: number;
  averageOverpayPercent: number | null;
  transferCount: number;
  seasonPoints: number;
  matchdayPoints: number;
  seasonRank: number;
  matchdayRank: number;
  currentMatchday: number;
  updatedAt: string;
}

export interface LeagueTransfer {
  id: number;
  leagueId: string;
  managerId: string;
  managerName: string;
  playerId: string;
  playerName: string;
  type: 'Buy' | 'Sell';
  price: number;
  otherManagerName: string | null;
  marketValueAtTransfer: number | null;
  overpayPercent: number | null;
  occurredAt: string;
}

export interface RankingStanding {
  userId: string;
  name: string;
  seasonPoints: number;
  matchdayPoints: number;
  seasonRank: number;
  matchdayRank: number;
}

export interface LiveRankingUpdate {
  leagueId: string;
  leagueName: string;
  matchday: number;
  standings: RankingStanding[];
}

export const POSITION_LABELS: Record<number, string> = {
  1: 'TW',
  2: 'ABW',
  3: 'MF',
  4: 'ST',
};

export const LINEUP_STATUS_LABELS: Record<string, string> = {
  Starting: 'Startelf',
  Doubtful: 'Fraglich',
  Bench: 'Bank',
  Out: 'Fehlt',
};
