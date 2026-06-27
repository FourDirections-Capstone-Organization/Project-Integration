import { useState, useEffect } from 'react';
import { getOrders, deleteOrder } from '../api/deliveryApi';
import type { Order } from '../types/order';

interface Props {
  onEdit: (order: Order) => void;
  onRefresh: number;
}

export const OrderList = ({ onEdit, onRefresh }: Props) => {
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchOrders = async () => {
    setLoading(true);
    try {
      const data = await getOrders();
      setOrders(data);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchOrders();
  }, [onRefresh]);

  const handleDelete = async (id: string) => {
    if (!window.confirm('Delete this order?')) return;
    await deleteOrder(id);
    fetchOrders();
  };

  if (loading) return <p>Loading...</p>;

  return (
    <table border={1} style={{ width: '100%', borderCollapse: 'collapse' }}>
      <thead>
        <tr>
          <th>Product Name</th>
          <th>Quantity</th>
          <th>Status</th>
          <th>Customer</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        {orders.map((o) => (
          <tr key={o.id}>
            <td>{o.productName}</td>
            <td>{o.quantity}</td>
            <td>{o.status}</td>
            <td>{o.customerName}</td>
            <td>
              <button onClick={() => onEdit(o)}>Edit</button>{' '}
              <button onClick={() => handleDelete(o.id)}>Delete</button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
};
