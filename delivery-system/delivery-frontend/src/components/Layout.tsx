import { useAuth } from '../context/AuthContext';

export const Layout = ({ children }: { children: React.ReactNode }) => {
  const { user, logout } = useAuth();

  return (
    <div>
      <nav style={{ background: '#333', color: '#fff', padding: '12px 20px', display: 'flex', justifyContent: 'space-between' }}>
        <strong>Delivery System</strong>
        {user && (
          <span>
            {user.name} ({user.role}){' '}
            <button onClick={logout} style={{ marginLeft: 12, padding: '4px 12px' }}>
              Logout
            </button>
          </span>
        )}
      </nav>
      <main style={{ padding: 20 }}>{children}</main>
    </div>
  );
};
