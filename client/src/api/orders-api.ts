import { apiRequest, type ApiResponse, type RequestOptions } from './http';
import type { Menu, Order, OrderDraftBody, PriceBreakdown, SubmitOrderBody } from './types';

export async function getMenu(): Promise<Menu> {
  return (await apiRequest<Menu>('/api/menu')).data;
}

export async function quoteOrder(
  draft: OrderDraftBody,
  signal?: AbortSignal,
): Promise<PriceBreakdown> {
  const options: RequestOptions = { method: 'POST', body: draft };
  if (signal) options.signal = signal;
  return (await apiRequest<PriceBreakdown>('/api/orders/quote', options)).data;
}

export function submitOrder(
  body: SubmitOrderBody,
  idempotencyKey: string,
): Promise<ApiResponse<Order>> {
  return apiRequest<Order>('/api/orders', {
    method: 'POST',
    body,
    headers: { 'Idempotency-Key': idempotencyKey },
  });
}

export async function getOrder(id: string): Promise<Order> {
  return (await apiRequest<Order>(`/api/orders/${encodeURIComponent(id)}`)).data;
}

export async function cancelOrderRequest(id: string): Promise<void> {
  await apiRequest<void>(`/api/orders/${encodeURIComponent(id)}`, { method: 'DELETE' });
}
