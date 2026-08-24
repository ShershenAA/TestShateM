export interface Part {
  id: string;
  articleNumber: string;
  name: string;
  description?: string;
  brand: string;
  category: string;
  price: number;
  isActive: boolean;
}

export interface StockItem {
  id: string;
  partId: string;
  articleNumber: string;
  partName: string;
  quantity: number;
  reserved: number;
  available: number;
}

export interface Order {
  id: string;
  dealerId: string;
  status: OrderStatus;
  totalAmount: number;
  createdAt: string;
  comment?: string;
  items: OrderItem[];
}

export interface OrderItem {
  id: string;
  partId: string;
  articleNumber: string;
  partName: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface CartItem {
  part: Part;
  quantity: number;
}

export enum OrderStatus {
  Pending = 0,
  Confirmed = 1,
  Rejected = 2,
  Shipped = 3,
  Delivered = 4,
  Cancelled = 5
}

export const OrderStatusLabel: Record<OrderStatus, string> = {
  [OrderStatus.Pending]: 'Ожидает',
  [OrderStatus.Confirmed]: 'Подтверждён',
  [OrderStatus.Rejected]: 'Отклонён',
  [OrderStatus.Shipped]: 'Отгружен',
  [OrderStatus.Delivered]: 'Доставлен',
  [OrderStatus.Cancelled]: 'Отменён'
};