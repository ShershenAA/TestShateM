import { Component, Input, Output, EventEmitter, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { CartItem, Order } from '../../models/models';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.scss'
})
export class CartComponent {
  @Input() items: CartItem[] = [];
  @Output() orderPlaced = new EventEmitter<Order>();
  @Output() removeItem = new EventEmitter<string>();

  dealerId = '3fa85f64-5717-4562-b3fc-2c963f66afa6'; // фиксированный для демо
  comment = '';
  loading = false;
  message = '';

  constructor(private api: ApiService) {}

  get total(): number {
    return this.items.reduce((sum, i) => sum + i.part.price * i.quantity, 0);
  }

  updateQuantity(item: CartItem, quantity: number): void {
    if (quantity < 1) return;
    item.quantity = quantity;
  }

  placeOrder(): void {
    if (this.items.length === 0) return;

    this.loading = true;
    this.message = '';

    const order = {
      dealerId: this.dealerId,
      comment: this.comment,
      items: this.items.map(i => ({
        partId: i.part.id,
        articleNumber: i.part.articleNumber,
        partName: i.part.name,
        quantity: i.quantity,
        unitPrice: i.part.price
      }))
    };

    this.api.createOrder(order).subscribe({
      next: (createdOrder) => {
        this.message = `Заказ #${createdOrder.id.slice(0, 8)} создан!`;
        this.loading = false;
        this.orderPlaced.emit(createdOrder);
      },
      error: (err) => {
        this.message = 'Ошибка при создании заказа';
        this.loading = false;
        console.error(err);
      }
    });
  }
}