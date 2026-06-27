import { useState, useEffect } from 'react';
import { createOrder, updateOrder, getProducts } from '../api/deliveryApi';
import type { Order, CreateOrderDto, UpdateOrderDto, ProductReference } from '../types/order';

interface Props {
  order: Order | null;
  onSaved: () => void;
  onCancel: () => void;
}

export const OrderForm = ({ order, onSaved, onCancel }: Props) => {
  const [products, setProducts] = useState<ProductReference[]>([]);
  const [selectedProductId, setSelectedProductId] = useState(order?.productId || '');
  const [selectedProductName, setSelectedProductName] = useState(order?.productName || '');
  const [quantity, setQuantity] = useState(order?.quantity?.toString() || '1');
  const [status, setStatus] = useState(order?.status || 'Pending');
  const [customerName, setCustomerName] = useState(order?.customerName || '');

  useEffect(() => {
    getProducts().then(setProducts).catch(() => {});
  }, []);

  useEffect(() => {
    if (order) {
      setSelectedProductId(order.productId);
      setSelectedProductName(order.productName);
      setQuantity(order.quantity.toString());
      setStatus(order.status);
      setCustomerName(order.customerName);
    }
  }, [order]);

  const handleProductChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const id = e.target.value;
    setSelectedProductId(id);
    const product = products.find((p) => p.id === id);
    setSelectedProductName(product?.name || '');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const data = {
      productId: selectedProductId,
      productName: selectedProductName,
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
        <label>Product (from Operational System):</label><br />
        <select value={selectedProductId} onChange={handleProductChange} required style={{ width: '100%', padding: 6 }}>
          <option value="">-- Select a product --</option>
          {products.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name} (${p.price.toFixed(2)})
            </option>
          ))}
        </select>
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
