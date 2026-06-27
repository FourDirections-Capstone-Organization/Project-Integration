export interface Order {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  status: string;
  customerName: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateOrderDto {
  productId: string;
  productName: string;
  quantity: number;
  status: string;
  customerName: string;
}

export interface UpdateOrderDto {
  productName: string;
  quantity: number;
  status: string;
  customerName: string;
}

export interface ProductReference {
  id: string;
  name: string;
  description: string | null;
  price: number;
}
