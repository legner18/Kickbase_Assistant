import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import {
  League,
  LeagueManager,
  LeagueTransfer,
  ManagerStat,
  MarketScanEntry,
  MarketValueHistoryPoint,
  Player,
  PlayerComparables,
  PlayerDetail,
  PlayerSearchResult,
  SellAlert,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = API_BASE_URL;

  getLeagues(): Observable<League[]> {
    return this.http.get<League[]>(`${this.base}/api/leagues`);
  }

  getManagers(leagueId: string): Observable<LeagueManager[]> {
    return this.http.get<LeagueManager[]>(`${this.base}/api/leagues/${leagueId}/managers`);
  }

  getSquad(leagueId: string, managerId?: string): Observable<Player[]> {
    const query = managerId ? `?managerId=${managerId}` : '';
    return this.http.get<Player[]>(`${this.base}/api/leagues/${leagueId}/squad${query}`);
  }

  getPlayerDetail(playerId: string): Observable<PlayerDetail> {
    return this.http.get<PlayerDetail>(`${this.base}/api/players/${playerId}`);
  }

  searchPlayers(query: string): Observable<PlayerSearchResult[]> {
    return this.http.get<PlayerSearchResult[]>(`${this.base}/api/players/search?q=${encodeURIComponent(query)}`);
  }

  getPlayerComparables(playerId: string): Observable<PlayerComparables> {
    return this.http.get<PlayerComparables>(`${this.base}/api/players/${playerId}/comparables`);
  }

  getMarketValueHistory(playerId: string): Observable<MarketValueHistoryPoint[]> {
    return this.http.get<MarketValueHistoryPoint[]>(`${this.base}/api/players/${playerId}/market-value-history`);
  }

  getSellAlerts(includeAcknowledged = false): Observable<SellAlert[]> {
    return this.http.get<SellAlert[]>(`${this.base}/api/sell-alerts?includeAcknowledged=${includeAcknowledged}`);
  }

  acknowledgeSellAlert(id: number): Observable<void> {
    return this.http.post<void>(`${this.base}/api/sell-alerts/${id}/acknowledge`, {});
  }

  getMarketScan(leagueId: string): Observable<MarketScanEntry[]> {
    return this.http.get<MarketScanEntry[]>(`${this.base}/api/leagues/${leagueId}/market-scan`);
  }

  getManagerStats(leagueId: string): Observable<ManagerStat[]> {
    return this.http.get<ManagerStat[]>(`${this.base}/api/leagues/${leagueId}/manager-stats`);
  }

  getTransfers(leagueId: string): Observable<LeagueTransfer[]> {
    return this.http.get<LeagueTransfer[]>(`${this.base}/api/leagues/${leagueId}/transfers`);
  }
}
