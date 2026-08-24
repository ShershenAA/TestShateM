import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PartsComponent } from './components/parts/parts.component';
import { CartComponent } from './components/cart/cart.component';
import { NotificationsComponent } from './components/notifications/notifications.component';
import { CartItem, Order } from './models/models';
import { SignalrService, OrderNotification } from './services/signalr.service';
import { AuthService } from './services/auth.service';
import { Subscription } from 'rxjs';
import {LoginComponent} from './components/login/login.component';

type Tab = 'catalog' | 'cart' | 'notifications';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, PartsComponent, CartComponent, NotificationsComponent, LoginComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  activeTab: Tab = 'catalog';
  cartItems: CartItem[] = [];
  notifications: OrderNotification[] = [];
  unreadCount = 0;
  private subscription?: Subscription;

  tabs: { id: Tab; label: string }[] = [
    { id: 'catalog', label: 'Каталог' },
    { id: 'cart', label: 'Корзина' },
    { id: 'notifications', label: 'Уведомления' }
  ];

  private dealerId = '3fa85f64-5717-4562-b3fc-2c963f66afa6';

  constructor(private signalr: SignalrService, public auth: AuthService) {}

  ngOnInit(): void {
    if (this.auth.isLoggedIn()) {
      this.initSignalR();
    }
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.signalr.disconnect();
  }

  private initSignalR(): void {
    const userId = this.auth.getUserId() ?? '3fa85f64-5717-4562-b3fc-2c963f66afa6';
    this.signalr.connect(userId);

    this.subscription = this.signalr.notifications$.subscribe(notification => {
      this.notifications.unshift(notification);
      if (this.activeTab !== 'notifications') {
        this.unreadCount++;
      }
    });
  }

  get cartCount(): number {
    return this.cartItems.reduce((sum, i) => sum + i.quantity, 0);
  }

  getBadge(tabId: Tab): number {
    if (tabId === 'cart') return this.cartCount;
    if (tabId === 'notifications') return this.unreadCount;
    return 0;
  }

  switchTab(tab: Tab): void {
    this.activeTab = tab;
    if (tab === 'notifications') {
      this.unreadCount = 0;
    }
  }

  onAddToCart(item: CartItem): void {
    const existing = this.cartItems.find(i => i.part.id === item.part.id);
    if (existing) {
      existing.quantity += 1;
    } else {
      this.cartItems.push({ ...item });
    }
  }

  onRemoveItem(partId: string): void {
    this.cartItems = this.cartItems.filter(i => i.part.id !== partId);
  }

  onOrderPlaced(order: Order): void {
    this.cartItems = [];
    this.switchTab('notifications');
  }

  logout(): void {
    this.auth.logout();
    window.location.reload();
  }
}
