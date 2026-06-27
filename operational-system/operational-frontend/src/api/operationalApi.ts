import axios from 'axios';
import type { Product, CreateProductDto, UpdateProductDto } from '../types/product';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5001';

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

export const getProducts = async (): Promise<Product[]> => {
  const response = await api.get<Product[]>('/api/products');
  return response.data;
};

export const getProduct = async (id: string): Promise<Product> => {
  const response = await api.get<Product>(`/api/products/${id}`);
  return response.data;
};

export const createProduct = async (data: CreateProductDto): Promise<Product> => {
  const response = await api.post<Product>('/api/products', data);
  return response.data;
};

export const updateProduct = async (id: string, data: UpdateProductDto): Promise<Product> => {
  const response = await api.put<Product>(`/api/products/${id}`, data);
  return response.data;
};

export const deleteProduct = async (id: string): Promise<void> => {
  await api.delete(`/api/products/${id}`);
};
