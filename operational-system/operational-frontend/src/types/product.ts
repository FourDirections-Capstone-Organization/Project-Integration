export interface Product {
  id: string;
  name: string;
  description: string | null;
  price: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateProductDto {
  name: string;
  description?: string;
  price: number;
}

export interface UpdateProductDto {
  name: string;
  description?: string;
  price: number;
}

export interface DeliveryOrder {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  status: string;
  customerName: string;
  createdAt: string;
}
