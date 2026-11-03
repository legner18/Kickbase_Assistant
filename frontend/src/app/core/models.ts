export interface League {
  id: string;
  name: string;
  competitionId: string;
  ownUserId: string;
  isActive: boolean;
}

export interface LeagueBalanceStatus {
  balance: number;
  creditLimit: number;
  status: 'Green' | 'Yellow' | 'Red';
  nextMatchdayNumber: number;
  nextKickoffAt: string;
  rosterGaps: string[];
  updatedAt: string;
}

export interface SellPlanCandidate {
  playerId: string;
  playerName: string;
  position: number;
  marketValue: number;
  expectedPoints: number;
  locked: boolean;
  purchasePrice: number | null;
  profitLoss: number | null;
}

export interface SellPlanCombination {
  playerIds: string[];
  playerNames: string[];
  proceeds: number;
  lostExpectedPoints: number;
  remainingSquadSize: number;
  resultingBalance: number;
}

export interface SellPlan {
  balance: number;
  candidates: SellPlanCandidate[];
  combinations: SellPlanCombination[];
}

export interface LineupPlayer {
  playerId: string;
  playerName: string;
  position: number;
  expectedPoints: number;
  startProbability: number;
  weightedPoints: number;
  injured: boolean;
}

export interface MissingPlayer {
  position: string;
  count: number;
}

export interface LineupOptimizerResult {
  formation: string | null;
  players: LineupPlayer[];
  totalExpectedPoints: number;
  unfillablePositions: string[];
  closestFormation?: string;
  missingPlayers?: MissingPlayer[];
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
  statusCode: number;
  lastSeenAt: string;
  latestLineupStatus: string | null;
  latestMarketValueChange: number | null;
  sellScore: number | null;
  sellMomentumScore: number | null;
  sellLineupScore: number | null;
  sellFixtureScore: number | null;
  purchasePrice: number | null;
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
  pointsPerMillion: number;
  fairValue: number;
  fairValueDeltaPercent: number;
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

export interface UpcomingFixture {
  matchdayNumber: number;
  opponentTeamId: string;
  opponentTeamName: string | null;
  isHome: boolean;
  difficultyTier: number;
}

export interface MarketListingHistoryEntry {
  leagueId: string;
  listedAt: string;
  removedAt: string | null;
  price: number;
  outcome: 'Open' | 'Sold' | 'Expired';
}

export interface OwnershipHistoryEntry {
  managerName: string;
  type: 'Buy' | 'Sell';
  price: number;
  occurredAt: string;
  heldDays: number | null;
}

export interface OwnershipHistoryLeague {
  leagueId: string;
  leagueName: string;
  chain: OwnershipHistoryEntry[];
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
  statusCode: number;
  lastSeenAt: string;
  fairValue: number;
  fairValueDeltaPercent: number;
  pointsPerMillion: number;
  medianPoints: number | null;
  startingXiQuota: number | null;
  pointsPerMinute: number | null;
  lineupHistory: LineupHistoryEntry[];
  sellAlerts: PlayerSellAlert[];
  owners: PlayerOwner[];
  upcomingFixtures: UpcomingFixture[];
  marketListingHistory: MarketListingHistoryEntry[];
  ownershipHistory: OwnershipHistoryLeague[];
  predictedChangePercent: number;
  predictedNextValue: number;
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
  transferBalance: number;
}

export type MarkingColor = 'None' | 'Green' | 'Yellow' | 'Red' | 'Blue';
export type LineupEstimate = 'Sicher' | 'Erwartet' | 'Unsicher' | 'Unwahrscheinlich' | 'Ausgeschlossen';

export interface PlayerMarking {
  playerId: string;
  color: MarkingColor;
  labels: string[];
  ownLineupEstimate: LineupEstimate | null;
  expiresAt: string | null;
}

export interface SetMarkingRequest {
  color: MarkingColor;
  labels: string[];
  ownLineupEstimate: LineupEstimate | null;
  expiresAt: string | null;
}

export interface FairValueComparisonEntry {
  managerId: string;
  managerName: string;
  playerCount: number;
  squadMarketValue: number;
  squadFairValue: number;
  deltaPercent: number;
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

export interface MarketListingChange {
  type: 'New' | 'Removed' | 'PriceChanged' | 'OfferCountChanged';
  playerId: string;
  playerName: string;
  previousValue: number | null;
  currentValue: number | null;
}

export interface LiveTransferActivity {
  occurredAt: string;
  playerName: string;
  price: number;
  managerName: string;
  type: 'Buy' | 'Sell';
}

export interface LiveMarketListingsUpdate {
  leagueId: string;
  leagueName: string;
  kind: 'listings';
  changes: MarketListingChange[];
}

export interface LiveMarketTransfersUpdate {
  leagueId: string;
  leagueName: string;
  kind: 'transfers';
  transfers: LiveTransferActivity[];
}

export type LiveMarketUpdate = LiveMarketListingsUpdate | LiveMarketTransfersUpdate;

export type LiveMatchStatus = 'NotStarted' | 'Live' | 'Finished';

export interface LiveMatchTeam {
  teamId: string;
  name: string | null;
  symbol: string | null;
  goals: number | null;
  points: number | null;
  ownPlayerCount: number;
  ownPoints: number;
}

export interface LiveMatch {
  matchId: string;
  matchdayNumber: number;
  kickoffAt: string;
  status: LiveMatchStatus;
  minute: string | null;
  pointsUpdatedAt: string | null;
  home: LiveMatchTeam;
  away: LiveMatchTeam;
}

export interface LiveMatchday {
  matchday: number | null;
  nextKickoffAt: string | null;
  matches: LiveMatch[];
}

export interface LiveTeamPlayer {
  playerId: string;
  playerName: string;
  position: number | null;
  points: number;
  eventKeys: number[];
  ownerManagerName: string | null;
  isMine: boolean;
  hasDetail: boolean;
  hasTimeline: boolean;
}

export interface LiveTeamPoints {
  matchId: string;
  teamId: string;
  teamName: string | null;
  points: number | null;
  pointsUpdatedAt: string | null;
  players: LiveTeamPlayer[];
}

export interface LiveManagerStanding {
  managerId: string;
  managerName: string;
  isMe: boolean;
  points: number;
  pointsCounted: boolean;
  matchdayRank: number;
  seasonPoints: number;
  seasonRank: number;
  lineupCount: number;
  startedCount: number;
  liveCount: number;
}

export interface LiveManagerStandings {
  matchday: number | null;
  updatedAt: string | null;
  managers: LiveManagerStanding[];
}

export interface LiveManagerPlayerMatch {
  matchId: string;
  status: LiveMatchStatus;
  minute: string | null;
  kickoffAt: string;
  opponent: string | null;
  isHome: boolean;
}

export interface LiveManagerPlayer {
  playerId: string;
  playerName: string;
  position: number | null;
  teamName: string | null;
  inLineup: boolean;
  points: number | null;
  eventKeys: number[];
  hasTimeline: boolean;
  hasDetail: boolean;
  match: LiveManagerPlayerMatch | null;
}

export interface LiveManagerDetail {
  managerId: string;
  managerName: string;
  isMe: boolean;
  matchday: number;
  points: number;
  pointsCounted: boolean;
  updatedAt: string;
  players: LiveManagerPlayer[];
}

export interface LivePlayerEvent {
  eventId: string;
  minute: number;
  title: string | null;
  points: number;
}

export interface LivePlayerTimeline {
  matchId: string;
  playerId: string;
  playerName: string;
  points: number;
  fetchedAt: string | null;
  events: LivePlayerEvent[];
}

export const POSITION_LABELS: Record<number, string> = {
  1: 'TW',
  2: 'ABW',
  3: 'MF',
  4: 'ST',
};

export const MATCH_EVENT_LABELS: Record<number, string> = {
  1: 'Tor',
  2: 'Eigentor',
  3: 'Vorlage',
  4: 'Gelb',
  8: 'Eingewechselt',
  9: 'Ausgewechselt',
};

export const LINEUP_STATUS_LABELS: Record<string, string> = {
  Starting: 'Startelf',
  Doubtful: 'Fraglich',
  Bench: 'Bank',
  Out: 'Fehlt',
};
