import { createPinia, setActivePinia } from 'pinia';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { nextTick } from 'vue';
import type { OrderDraftBody, PriceBreakdown } from '@/api/types';
import { useDraftOrderStore } from '@/stores/draft-order-store';
import { QUOTE_DEBOUNCE_MS, useQuoteStore } from '@/stores/quote-store';

const quoteOrder =
  vi.fn<(draft: OrderDraftBody, signal?: AbortSignal) => Promise<PriceBreakdown>>();
vi.mock('@/api/orders-api', () => ({
  quoteOrder: (draft: OrderDraftBody, signal?: AbortSignal) => quoteOrder(draft, signal),
}));

function breakdown(total: number): PriceBreakdown {
  return {
    lines: [],
    subtotal: total,
    promoCode: null,
    discount: 0,
    taxRate: 0,
    tax: 0,
    tipBase: 'PreTax',
    tip: 0,
    total,
    description: '',
  };
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

beforeEach(() => {
  vi.useFakeTimers();
  sessionStorage.clear();
  quoteOrder.mockReset();
  setActivePinia(createPinia());
});

afterEach(() => {
  vi.useRealTimers();
});

describe('quote store', () => {
  it('debounces changes into one request and shows "updating" until it answers', async () => {
    const draft = useDraftOrderStore();
    const quote = useQuoteStore();
    quoteOrder.mockResolvedValue(breakdown(10));
    draft.startOrder();
    draft.addLine({ sizeCode: 'large', toppingCodes: [], quantity: 1 });
    await nextTick();
    const id = draft.draft!.lines[0]!.id;
    draft.setQuantity(id, 2);
    await nextTick();
    draft.setQuantity(id, 3);
    await nextTick();

    expect(quote.updating).toBe(true);
    expect(quoteOrder).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(QUOTE_DEBOUNCE_MS);

    expect(quoteOrder).toHaveBeenCalledTimes(1);
    expect(quoteOrder.mock.calls[0]![0].lines[0]!.quantity).toBe(3);
    expect(quote.breakdown?.total).toBe(10);
    expect(quote.updating).toBe(false);
  });

  it('ignores a stale response that arrives after a newer one', async () => {
    const draft = useDraftOrderStore();
    const quote = useQuoteStore();
    const first = deferred<PriceBreakdown>();
    const second = deferred<PriceBreakdown>();
    quoteOrder.mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise);

    draft.startOrder();
    draft.addLine({ sizeCode: 'large', toppingCodes: [], quantity: 1 });
    await nextTick();
    await vi.advanceTimersByTimeAsync(QUOTE_DEBOUNCE_MS); // request 1 in flight

    draft.setQuantity(draft.draft!.lines[0]!.id, 2);
    await nextTick();
    await vi.advanceTimersByTimeAsync(QUOTE_DEBOUNCE_MS); // request 2 in flight
    expect(quoteOrder).toHaveBeenCalledTimes(2);

    second.resolve(breakdown(20));
    await vi.advanceTimersByTimeAsync(0);
    first.resolve(breakdown(10)); // the older answer lands last
    await vi.advanceTimersByTimeAsync(0);

    expect(quote.breakdown?.total).toBe(20);
    expect(quote.updating).toBe(false);
  });

  it('ignores a stale failure too, and clears when the order empties', async () => {
    const draft = useDraftOrderStore();
    const quote = useQuoteStore();
    const first = deferred<PriceBreakdown>();
    quoteOrder.mockReturnValueOnce(first.promise).mockResolvedValueOnce(breakdown(20));

    draft.startOrder();
    draft.addLine({ sizeCode: 'large', toppingCodes: [], quantity: 1 });
    await nextTick();
    await vi.advanceTimersByTimeAsync(QUOTE_DEBOUNCE_MS);
    draft.setQuantity(draft.draft!.lines[0]!.id, 2);
    await nextTick();
    await vi.advanceTimersByTimeAsync(QUOTE_DEBOUNCE_MS);
    first.reject(new Error('late failure'));
    await vi.advanceTimersByTimeAsync(0);

    expect(quote.error).toBeNull();
    expect(quote.breakdown?.total).toBe(20);

    draft.removeLine(draft.draft!.lines[0]!.id);
    await nextTick();
    expect(quote.breakdown).toBeNull();
    expect(quoteOrder).toHaveBeenCalledTimes(2);
  });
});
