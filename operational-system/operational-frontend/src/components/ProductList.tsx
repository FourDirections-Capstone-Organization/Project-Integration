import { useState, useEffect } from 'react';
import { getProducts, deleteProduct } from '../api/operationalApi';
import type { Product } from '../types/product';

interface Props {
  onEdit: (product: Product) => void;
  onRefresh: number;
}

export const ProductList = ({ onEdit, onRefresh }: Props) => {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchProducts = async () => {
    setLoading(true);
    try {
      const data = await getProducts();
      setProducts(data);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchProducts();
  }, [onRefresh]);

  const handleDelete = async (id: string) => {
    if (!window.confirm('Delete this product?')) return;
    await deleteProduct(id);
    fetchProducts();
  };

  if (loading) return <p>Loading...</p>;

  return (
    <table border={1} style={{ width: '100%', borderCollapse: 'collapse' }}>
      <thead>
        <tr>
          <th>Name</th>
          <th>Description</th>
          <th>Price</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        {products.map((p) => (
          <tr key={p.id}>
            <td>{p.name}</td>
            <td>{p.description || '-'}</td>
            <td>${p.price.toFixed(2)}</td>
            <td>
              <button onClick={() => onEdit(p)}>Edit</button>{' '}
              <button onClick={() => handleDelete(p.id)}>Delete</button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
};
