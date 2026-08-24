import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';

export interface OrderNotification {
  orderId: string;
  status: string;
  message: string;
  timestamp: string;
}

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  private connection: signalR.HubConnection;
  
  // Subject — как EventEmitter но для любых подписчиков
  private notificationSubject = new Subject<OrderNotification>();
  public notifications$ = this.notificationSubject.asObservable();

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect()
      .build();

    this.connection.on('OrderStatusChanged', (data: OrderNotification) => {
      this.notificationSubject.next(data);
    });
  }

  async connect(dealerId: string): Promise<void> {
    try {
      await this.connection.start();
      await this.connection.invoke('SubscribeToDealer', dealerId);
      console.log('SignalR connected, subscribed to dealer:', dealerId);
    } catch (err) {
      console.error('SignalR connection error:', err);
    }
  }

  async disconnect(): Promise<void> {
    await this.connection.stop();
  }
}