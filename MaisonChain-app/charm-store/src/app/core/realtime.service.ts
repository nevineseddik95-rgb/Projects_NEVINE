import { Injectable, NgZone, inject } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

export type RealtimeState = 'connecting' | 'online' | 'offline';

@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private auth = inject(AuthService);
  private zone = inject(NgZone);
  private connection?: signalR.HubConnection;
  private retryTimer?: ReturnType<typeof setTimeout>;
  private stopping = false;

  readonly changes$ = new Subject<string[]>();
  readonly state$ = new BehaviorSubject<RealtimeState>('offline');

  start(): void {
    if (this.connection || !this.auth.token) return;
    this.stopping = false;
    this.state$.next('connecting');
    this.connect();
  }

  private connect(): void {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl, { accessTokenFactory: () => this.auth.token ?? '' })
      .withAutomaticReconnect()
      .build();

    this.connection.on('changed', (resources: string[]) =>
      this.zone.run(() => this.changes$.next(resources)));
    this.connection.onreconnecting(() => this.zone.run(() => this.state$.next('connecting')));
    this.connection.onreconnected(() => this.zone.run(() => {
      this.state$.next('online');
      this.changes$.next(['*']);
    }));
    this.connection.onclose(() => this.zone.run(() => this.state$.next('offline')));

    this.connection.start().then(() => {
      if (!this.stopping) this.state$.next('online');
    }).catch(() => {
      if (this.stopping) return;
      this.state$.next('offline');
      this.retryTimer = setTimeout(() => {
        void this.connection?.stop();
        this.connection = undefined;
        this.connect();
      }, 5000);
    });
  }

  stop(): void {
    this.stopping = true;
    if (this.retryTimer) clearTimeout(this.retryTimer);
    this.retryTimer = undefined;
    this.state$.next('offline');
    const connection = this.connection;
    this.connection = undefined;
    void connection?.stop();
  }
}
