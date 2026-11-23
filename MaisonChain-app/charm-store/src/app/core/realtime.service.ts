import { Injectable, NgZone, inject } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

/** Receives "something changed" messages from the API, so lists and the bell update without a refresh. */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private auth = inject(AuthService);
  private zone = inject(NgZone);
  private connection?: signalR.HubConnection;

  /** names of the lists that changed, e.g. ["commands", "customers"]; "*" means refresh everything */
  readonly changes$ = new Subject<string[]>();

  start(): void {
    if (this.connection || !this.auth.token) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl, { accessTokenFactory: () => this.auth.token ?? '' })
      .withAutomaticReconnect()
      .build();

    this.connection.on('changed', (resources: string[]) => this.zone.run(() => this.changes$.next(resources)));
    this.connection.onreconnected(() => this.zone.run(() => this.changes$.next(['*'])));   // catch up after a lost connection
    this.connection.start().catch(() => { /* the one-minute check keeps the lists fresh anyway */ });
  }

  stop(): void {
    this.connection?.stop();
    this.connection = undefined;
  }
}