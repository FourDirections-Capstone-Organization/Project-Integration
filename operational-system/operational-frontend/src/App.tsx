import { useState } from 'react';
import { AuthProvider, useAuth } from './context/AuthContext';
import { LoginPage } from './components/LoginPage';
import { Layout } from './components/Layout';
import { ProductList } from './components/ProductList';
import { ProductForm } from './components/ProductForm';
import type { Product } from './types/product';

function AppContent() {
  const { isAuthenticated } = useAuth();
  const [editingProduct, setEditingProduct] = useState<Product | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  if (!isAuthenticated) return <LoginPage />;

  return (
    <Layout>
      <h2>Products</h2>
      {showForm && (
        <ProductForm
          product={editingProduct}
          onSaved={() => {
            setShowForm(false);
            setEditingProduct(null);
            setRefreshKey((k) => k + 1);
          }}
          onCancel={() => {
            setShowForm(false);
            setEditingProduct(null);
          }}
        />
      )}
      <button
        onClick={() => {
          setEditingProduct(null);
          setShowForm(true);
        }}
        style={{ marginBottom: 12, padding: '8px 16px' }}
      >
        + New Product
      </button>
      <ProductList
        onEdit={(product) => {
          setEditingProduct(product);
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
