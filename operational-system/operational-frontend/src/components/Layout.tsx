import { useAuth } from '../context/AuthContext';

interface Props {
  children: React.ReactNode;
  activeView: string;
  onViewChange: (view: string) => void;
}

export const Layout = ({ children, activeView, onViewChange }: Props) => {
  const { user, logout } = useAuth();

  return (
    <div>
      <nav style={{ background: '#333', color: '#fff', padding: '12px 20px', display: 'flex', justifyContent: 'space-between' }}>
        <div>
          <strong style={{ marginRight: 20 }}>Operational System</strong>
          <button
            onClick={() => onViewChange('products')}
            style={{
              padding: '4px 12px',
              marginRight: 8,
              fontWeight: activeView === 'products' ? 'bold' : 'normal',
              background: activeView === 'products' ? '#555' : 'transparent',
              color: '#fff',
              border: '1px solid #666',
              cursor: 'pointer'
            }}
          >
            Products
          </button>
          <button
            onClick={() => onViewChange('orders')}
            style={{
              padding: '4px 12px',
              fontWeight: activeView === 'orders' ? 'bold' : 'normal',
              background: activeView === 'orders' ? '#555' : 'transparent',
              color: '#fff',
              border: '1px solid #666',
              cursor: 'pointer'
            }}
          >
            Orders (from Delivery)
          </button>
        </div>
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
