import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrderNotification } from '../../services/signalr.service';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.scss'
})
export class NotificationsComponent {
  @Input() notifications: OrderNotification[] = [];

  getStatusClass(status: string): string {
    switch (status) {
      case 'Confirmed': return 'status-confirmed';
      case 'Rejected': return 'status-rejected';
      default: return 'status-pending';
    }
  }
}