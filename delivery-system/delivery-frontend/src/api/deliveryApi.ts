import axios from 'axios';
import type { Order, CreateOrderDto, UpdateOrderDto, ProductReference } from '../types/order';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5002';

const api = axios.create({
  baseURL: API_URL,
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('accessToken');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export const getOrders = async (): Promise<Order[]> => {
  const response = await api.get<Order[]>('/api/orders');
  return response.data;
};

export const getOrder = async (id: string): Promise<Order> => {
  const response = await api.get<Order>(`/api/orders/${id}`);
  return response.data;
};

export const createOrder = async (data: CreateOrderDto): Promise<Order> => {
  const response = await api.post<Order>('/api/orders', data);
  return response.data;
};

export const updateOrder = async (id: string, data: UpdateOrderDto): Promise<Order> => {
  const response = await api.put<Order>(`/api/orders/${id}`, data);
  return response.data;
};

export const deleteOrder = async (id: string): Promise<void> => {
  await api.delete(`/api/orders/${id}`);
};

export const getProducts = async (): Promise<ProductReference[]> => {
  const response = await api.get<ProductReference[]>('/api/products');
  return response.data;
};
