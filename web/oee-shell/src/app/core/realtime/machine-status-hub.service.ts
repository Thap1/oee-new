import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { AuthService } from '../auth/auth.service';

export interface MachineStatusChangedEvent {
  machineId: string;
  status: string;
  counter: number;
  lastReportedAt: string;
}

/**
 * One SignalR connection to this site's hub (Story 2.2, AD-8). `lastEvent` is a plain writable
 * signal (not `.asReadonly()`) so tests can simulate an incoming broadcast by setting it directly,
 * without opening a real WebSocket (see `machine-status-hub.service` usage in dashboard-page.spec.ts).
 */
@Injectable({ providedIn: 'root' })
export class MachineStatusHubService {
  private connection: signalR.HubConnection | null = null;

  readonly lastEvent = signal<MachineStatusChangedEvent | null>(null);
  /** One grouped update for many machines at once (e.g. the demo simulator's tick) — consumers apply
   * the whole batch in a single state update instead of one per machine (see dashboard-page.ts). */
  readonly lastBatch = signal<MachineStatusChangedEvent[] | null>(null);

  constructor(private readonly authService: AuthService) {}

  connect(): void {
    if (this.connection) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/machine-status', { accessTokenFactory: () => this.authService.accessToken ?? '' })
      .withAutomaticReconnect()
      .build();

    this.connection.on('MachineStatusChanged', (event: MachineStatusChangedEvent) => {
      this.lastEvent.set(event);
    });

    this.connection.on('MachineStatusesChanged', (events: MachineStatusChangedEvent[]) => {
      this.lastBatch.set(events);
    });

    void this.connection.start();
  }

  disconnect(): void {
    void this.connection?.stop();
    this.connection = null;
  }
}
