export interface League {
  id: string;
  name: string;
  competitionId: string;
}

export interface Player {
  id: string;
  name: string;
  teamId: string;
  teamName: string | null;
  position: number;
  marketValue: number;
  totalPoints: number;
  lastSeenAt: string;
  latestLineupStatus: string | null;
  latestMarketValueChange: number | null;
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
  price: number;
  offerCount: number;
  expiresAt: string;
  momentumScore: number;
  lineupScore: number;
  fixtureScore: number;
  overallScore: number;
  scannedAt: string;
}

export interface ManagerStat {
  id: number;
  leagueId: string;
  managerId: string;
  managerName: string;
  estimatedBudget: number;
  averageOverpayPercent: number | null;
  transferCount: number;
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
