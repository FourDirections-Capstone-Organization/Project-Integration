import axios from 'axios';
import type { LoginRequest, LoginResponse } from '../types/auth';

const AUTH_URL = import.meta.env.VITE_AUTH_URL || 'http://localhost:5000';

const authApi = axios.create({
  baseURL: AUTH_URL,
});

export const login = async (data: LoginRequest): Promise<LoginResponse> => {
  const response = await authApi.post<LoginResponse>('/api/auth/login', data);
  return response.data;
};
