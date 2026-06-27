import { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { login } from '../api/authApi';

export const LoginPage = () => {
  const { setUser } = useAuth();
  const [employeeNumber, setEmployeeNumber] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      const result = await login({ employeeNumber, password });
      localStorage.setItem('accessToken', result.accessToken);
      setUser({
        accessToken: result.accessToken,
        role: result.role,
        name: result.name,
        employeeNumber: result.employeeNumber,
      });
    } catch {
      setError('Invalid credentials');
    }
  };

  return (
    <div style={{ maxWidth: 400, margin: '100px auto', padding: 20 }}>
      <h1 style={{ marginBottom: 24, wordSpacing: 8 }}>Delivery System</h1>
      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: 12 }}>
          <label>Employee Number:</label><br />
          <input
            value={employeeNumber}
            onChange={(e) => setEmployeeNumber(e.target.value)}
            required
            style={{ width: '100%', padding: 8 }}
          />
        </div>
        <div style={{ marginBottom: 12 }}>
          <label>Password:</label><br />
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            style={{ width: '100%', padding: 8 }}
          />
        </div>
        {error && <p style={{ color: 'red' }}>{error}</p>}
        <button type="submit" style={{ padding: '10px 20px', width: '100%' }}>
          Login
        </button>
      </form>
    </div>
  );
};
