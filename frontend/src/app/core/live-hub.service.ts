import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { API_BASE_URL } from './api-config';
import { LiveMarketUpdate, LiveWorkerStatus } from './models';

export type LiveConnectionState = 'disconnected' | 'connecting' | 'connected';

const RECONNECT_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];
const RESTART_DELAY_MS = 10_000;
const RESYNC_AFTER_HIDDEN_MS = 30_000;

@Injectable({ providedIn: 'root' })
export class LiveHubService {
  private connection: signalR.HubConnection | null = null;
  private restartTimer: ReturnType<typeof setTimeout> | null = null;
  private hiddenAt: number | null = null;
  private hasConnected = false;

  readonly lastMarketUpdate = signal<LiveMarketUpdate | null>(null);
  // Nur ein Signal "Kader/Kontostand dieser Liga hat sich geändert" (OwnSquadSyncJob) - das Objekt
  // ist pro Push neu, damit zwei Pushes für dieselbe Liga trotzdem jeweils einen Effekt auslösen.
  readonly lastSquadUpdate = signal<{ leagueId: string } | null>(null);
  readonly lastLiveMatchesUpdate = signal<{ competitionId: string } | null>(null);
  readonly lastResync = signal<{ requestedAt: number } | null>(null);
  readonly liveStatus = signal<LiveWorkerStatus | null>(null);
  readonly connectionState = signal<LiveConnectionState>('disconnected');
  readonly connectedAt = signal<number | null>(null);

  private readonly onVisibilityChange = (): void => {
    if (document.visibilityState === 'hidden') {
      this.hiddenAt = Date.now();
      return;
    }

    const hiddenFor = this.hiddenAt === null ? 0 : Date.now() - this.hiddenAt;
    this.hiddenAt = null;
    const state = this.connection?.state;
    if (state === signalR.HubConnectionState.Disconnected) {
      this.start();
    } else if (state === signalR.HubConnectionState.Connected && hiddenFor >= RESYNC_AFTER_HIDDEN_MS) {
      this.requestResync();
    }
  };

  connect(): void {
    if (this.connection) {
      return;
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/live`)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) =>
          RECONNECT_DELAYS_MS[Math.min(retryContext.previousRetryCount, RECONNECT_DELAYS_MS.length - 1)],
      })
      .build();

    connection.on('MarketUpdated', (_leagueId: string, payload: LiveMarketUpdate) => {
      this.lastMarketUpdate.set(payload);
    });
    connection.on('SquadUpdated', (leagueId: string) => {
      this.lastSquadUpdate.set({ leagueId });
    });
    connection.on('LiveMatchesUpdated', (competitionId: string) => {
      this.lastLiveMatchesUpdate.set({ competitionId });
    });
    connection.on('LiveStatusUpdated', (status: LiveWorkerStatus) => {
      this.liveStatus.set(status);
    });
    connection.onreconnecting(() => this.connectionState.set('connecting'));
    connection.onreconnected(() => {
      this.markConnected();
      this.requestResync();
    });
    connection.onclose(() => {
      if (this.connection === connection) {
        this.connectionState.set('disconnected');
        this.connectedAt.set(null);
        this.scheduleRestart();
      }
    });

    this.connection = connection;
    this.hasConnected = false;
    document.addEventListener('visibilitychange', this.onVisibilityChange);
    this.start();
  }

  disconnect(): void {
    const connection = this.connection;
    this.connection = null;
    this.cancelRestart();
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
    connection?.stop();
    this.connectionState.set('disconnected');
    this.connectedAt.set(null);
    this.liveStatus.set(null);
  }

  private start(): void {
    this.cancelRestart();
    const connection = this.connection;
    if (!connection || connection.state !== signalR.HubConnectionState.Disconnected) {
      return;
    }

    this.connectionState.set('connecting');
    connection
      .start()
      .then(() => {
        if (this.connection !== connection) {
          return;
        }
        const isRestart = this.hasConnected;
        this.markConnected();
        if (isRestart) {
          this.requestResync();
        }
      })
      .catch(() => {
        if (this.connection === connection) {
          this.connectionState.set('disconnected');
          this.scheduleRestart();
        }
      });
  }

  private markConnected(): void {
    this.hasConnected = true;
    this.connectionState.set('connected');
    this.connectedAt.set(Date.now());
  }

  private requestResync(): void {
    this.lastResync.set({ requestedAt: Date.now() });
  }

  private scheduleRestart(): void {
    this.cancelRestart();
    this.restartTimer = setTimeout(() => this.start(), RESTART_DELAY_MS);
  }

  private cancelRestart(): void {
    if (this.restartTimer !== null) {
      clearTimeout(this.restartTimer);
      this.restartTimer = null;
    }
  }
}
