import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { API_BASE_URL } from './api-config';
import { LiveMarketUpdate, LiveRankingUpdate } from './models';

export type LiveConnectionState = 'disconnected' | 'connecting' | 'connected';

@Injectable({ providedIn: 'root' })
export class LiveHubService {
  private connection: signalR.HubConnection | null = null;

  readonly lastUpdate = signal<LiveRankingUpdate | null>(null);
  readonly lastMarketUpdate = signal<LiveMarketUpdate | null>(null);
  // Nur ein Signal "Kader/Kontostand dieser Liga hat sich geändert" (OwnSquadSyncJob) - das Objekt
  // ist pro Push neu, damit zwei Pushes für dieselbe Liga trotzdem jeweils einen Effekt auslösen.
  readonly lastSquadUpdate = signal<{ leagueId: string } | null>(null);
  readonly connectionState = signal<LiveConnectionState>('disconnected');

  connect(): void {
    if (this.connection) {
      return;
    }

    this.connectionState.set('connecting');
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/live`)
      .withAutomaticReconnect()
      .build();

    connection.on('RankingUpdated', (_leagueId: string, payload: LiveRankingUpdate) => {
      this.lastUpdate.set(payload);
    });
    connection.on('MarketUpdated', (_leagueId: string, payload: LiveMarketUpdate) => {
      this.lastMarketUpdate.set(payload);
    });
    connection.on('SquadUpdated', (leagueId: string) => {
      this.lastSquadUpdate.set({ leagueId });
    });
    connection.onreconnected(() => this.connectionState.set('connected'));
    connection.onreconnecting(() => this.connectionState.set('connecting'));
    connection.onclose(() => this.connectionState.set('disconnected'));

    connection
      .start()
      .then(() => this.connectionState.set('connected'))
      .catch(() => this.connectionState.set('disconnected'));

    this.connection = connection;
  }

  disconnect(): void {
    this.connection?.stop();
    this.connection = null;
    this.connectionState.set('disconnected');
  }
}
