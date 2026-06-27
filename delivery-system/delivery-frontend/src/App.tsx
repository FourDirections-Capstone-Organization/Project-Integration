import { useState } from 'react';
import { AuthProvider, useAuth } from './context/AuthContext';
import { LoginPage } from './components/LoginPage';
import { Layout } from './components/Layout';
import { OrderList } from './components/OrderList';
import { OrderForm } from './components/OrderForm';
import type { Order } from './types/order';

function AppContent() {
  const { isAuthenticated } = useAuth();
  const [editingOrder, setEditingOrder] = useState<Order | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  if (!isAuthenticated) return <LoginPage />;

  return (
    <Layout>
      <h2>Orders</h2>
      {showForm && (
        <OrderForm
          order={editingOrder}
          onSaved={() => {
            setShowForm(false);
            setEditingOrder(null);
            setRefreshKey((k) => k + 1);
          }}
          onCancel={() => {
            setShowForm(false);
            setEditingOrder(null);
          }}
        />
      )}
      <button
        onClick={() => {
          setEditingOrder(null);
          setShowForm(true);
        }}
        style={{ marginBottom: 12, padding: '8px 16px' }}
      >
        + New Order
      </button>
      <OrderList
        onEdit={(order) => {
          setEditingOrder(order);
          setShowForm(true);
        }}
        onRefresh={refreshKey}
      />
    </Layout>
  );
}

function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  );
}

export default App;
