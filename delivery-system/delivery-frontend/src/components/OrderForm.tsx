import { useState, useEffect } from 'react';
import { createOrder, updateOrder } from '../api/deliveryApi';
import type { Order, CreateOrderDto, UpdateOrderDto } from '../types/order';

interface Props {
  order: Order | null;
  onSaved: () => void;
  onCancel: () => void;
}

export const OrderForm = ({ order, onSaved, onCancel }: Props) => {
  const [productName, setProductName] = useState(order?.productName || '');
  const [quantity, setQuantity] = useState(order?.quantity?.toString() || '1');
  const [status, setStatus] = useState(order?.status || 'Pending');
  const [customerName, setCustomerName] = useState(order?.customerName || '');

  useEffect(() => {
    if (order) {
      setProductName(order.productName);
      setQuantity(order.quantity.toString());
      setStatus(order.status);
      setCustomerName(order.customerName);
    }
  }, [order]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const data = {
      productId: order?.productId || '00000000-0000-0000-0000-000000000000',
      productName,
      quantity: parseInt(quantity),
      status,
      customerName,
    };

    if (order) {
      const { productId, ...updateData } = data;
      await updateOrder(order.id, updateData as UpdateOrderDto);
    } else {
      await createOrder(data as CreateOrderDto);
    }
    onSaved();
  };

  return (
    <form onSubmit={handleSubmit} style={{ marginBottom: 20, padding: 16, border: '1px solid #ccc' }}>
      <h3>{order ? 'Edit Order' : 'New Order'}</h3>
      <div style={{ marginBottom: 8 }}>
        <label>Product Name:</label><br />
        <input value={productName} onChange={(e) => setProductName(e.target.value)} required style={{ width: '100%', padding: 6 }} />
      </div>
      <div style={{ marginBottom: 8 }}>
        <label>Quantity:</label><br />
        <input type="number" min="1" value={quantity} onChange={(e) => setQuantity(e.target.value)} required style={{ width: '100%', padding: 6 }} />
      </div>
      <div style={{ marginBottom: 8 }}>
        <label>Status:</label><br />
        <select value={status} onChange={(e) => setStatus(e.target.value)} style={{ width: '100%', padding: 6 }}>
          <option>Pending</option>
          <option>Shipped</option>
          <option>Delivered</option>
        </select>
      </div>
      <div style={{ marginBottom: 8 }}>
        <label>Customer Name:</label><br />
        <input value={customerName} onChange={(e) => setCustomerName(e.target.value)} required style={{ width: '100%', padding: 6 }} />
      </div>
      <button type="submit">{order ? 'Update' : 'Create'}</button>{' '}
      <button type="button" onClick={onCancel}>Cancel</button>
    </form>
  );
};
