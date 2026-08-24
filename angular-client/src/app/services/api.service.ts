import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Part, Order, StockItem } from '../models/models';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // ── Parts ────────────────────────────────────────
  getParts(): Observable<Part[]> {
    return this.http.get<Part[]>(`${this.apiUrl}/api/parts`);
  }

  searchParts(query: string): Observable<Part[]> {
    return this.http.get<Part[]>(`${this.apiUrl}/api/parts/search?q=${query}`);
  }

  // ── Stock ────────────────────────────────────────
  getStock(): Observable<StockItem[]> {
    return this.http.get<StockItem[]>(`${this.apiUrl}/api/stock`);
  }

  // ── Orders ───────────────────────────────────────
  getOrders(): Observable<Order[]> {
    return this.http.get<Order[]>(`${this.apiUrl}/api/orders`);
  }

  createOrder(order: {
    dealerId: string;
    comment?: string;
    items: {
      partId: string;
      articleNumber: string;
      partName: string;
      quantity: number;
      unitPrice: number;
    }[]
  }): Observable<Order> {
    return this.http.post<Order>(`${this.apiUrl}/api/orders`, order);
  }
}