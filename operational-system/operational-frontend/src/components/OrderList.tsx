import { useState, useEffect } from 'react';
import { getDeliveryOrders } from '../api/operationalApi';
import type { DeliveryOrder } from '../types/product';

export const OrderList = ({ refreshKey }: { refreshKey: number }) => {
  const [orders, setOrders] = useState<DeliveryOrder[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetch = async () => {
      setLoading(true);
      try {
        const data = await getDeliveryOrders();
        setOrders(data);
      } finally {
        setLoading(false);
      }
    };
    fetch();
  }, [refreshKey]);

  if (loading) return <p>Loading orders from Delivery System...</p>;

  if (orders.length === 0) return <p>No orders found in Delivery System.</p>;

  return (
    <table border={1} style={{ width: '100%', borderCollapse: 'collapse' }}>
      <thead>
        <tr>
          <th>Product</th>
          <th>Quantity</th>
          <th>Status</th>
          <th>Customer</th>
          <th>Created</th>
        </tr>
      </thead>
      <tbody>
        {orders.map((o) => (
          <tr key={o.id}>
            <td>{o.productName}</td>
            <td>{o.quantity}</td>
            <td>{o.status}</td>
            <td>{o.customerName}</td>
            <td>{new Date(o.createdAt).toLocaleDateString()}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
};
