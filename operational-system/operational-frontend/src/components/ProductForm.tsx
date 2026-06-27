import { useState, useEffect } from 'react';
import { createProduct, updateProduct } from '../api/operationalApi';
import type { Product, CreateProductDto, UpdateProductDto } from '../types/product';

interface Props {
  product: Product | null;
  onSaved: () => void;
  onCancel: () => void;
}

export const ProductForm = ({ product, onSaved, onCancel }: Props) => {
  const [name, setName] = useState(product?.name || '');
  const [description, setDescription] = useState(product?.description || '');
  const [price, setPrice] = useState(product?.price?.toString() || '');

  useEffect(() => {
    if (product) {
      setName(product.name);
      setDescription(product.description || '');
      setPrice(product.price.toString());
    }
  }, [product]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const data = { name, description, price: parseFloat(price) };

    if (product) {
      await updateProduct(product.id, data as UpdateProductDto);
    } else {
      await createProduct(data as CreateProductDto);
    }
    onSaved();
  };

  return (
    <form onSubmit={handleSubmit} style={{ marginBottom: 20, padding: 16, border: '1px solid #ccc' }}>
      <h3>{product ? 'Edit Product' : 'New Product'}</h3>
      <div style={{ marginBottom: 8 }}>
        <label>Name:</label><br />
        <input value={name} onChange={(e) => setName(e.target.value)} required style={{ width: '100%', padding: 6 }} />
      </div>
      <div style={{ marginBottom: 8 }}>
        <label>Description:</label><br />
        <textarea value={description} onChange={(e) => setDescription(e.target.value)} style={{ width: '100%', padding: 6 }} />
      </div>
      <div style={{ marginBottom: 8 }}>
        <label>Price:</label><br />
        <input type="number" step="0.01" min="0.01" value={price} onChange={(e) => setPrice(e.target.value)} required style={{ width: '100%', padding: 6 }} />
      </div>
      <button type="submit">{product ? 'Update' : 'Create'}</button>{' '}
      <button type="button" onClick={onCancel}>Cancel</button>
    </form>
  );
};
