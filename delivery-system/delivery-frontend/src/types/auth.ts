export interface LoginRequest {
  employeeNumber: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  role: string;
  name: string;
  employeeNumber: string;
}

export interface User {
  accessToken: string;
  role: string;
  name: string;
  employeeNumber: string;
}
