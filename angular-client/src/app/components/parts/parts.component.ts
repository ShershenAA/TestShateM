import { Component, OnInit, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { Part, CartItem } from '../../models/models';

@Component({
  selector: 'app-parts',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './parts.component.html',
  styleUrl: './parts.component.scss'
})
export class PartsComponent implements OnInit {
  @Output() addToCart = new EventEmitter<CartItem>();

  parts: Part[] = [];
  loading = false;
  searchQuery = '';

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.loadParts();
  }

  loadParts(): void {
    this.loading = true;
    this.api.getParts().subscribe({
      next: (parts) => {
        this.parts = parts;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading parts:', err);
        this.loading = false;
      }
    });
  }

  search(): void {
    if (!this.searchQuery.trim()) {
      this.loadParts();
      return;
    }
    this.loading = true;
    this.api.searchParts(this.searchQuery).subscribe({
      next: (parts) => {
        this.parts = parts;
        this.loading = false;
      },
      error: (err) => {
        console.error('Search error:', err);
        this.loading = false;
      }
    });
  }

  onAddToCart(part: Part): void {
    this.addToCart.emit({ part, quantity: 1 });
  }
}