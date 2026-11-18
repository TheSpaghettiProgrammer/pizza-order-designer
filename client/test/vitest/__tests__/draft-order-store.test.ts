import { createPinia, setActivePinia } from 'pinia';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createEmptyDraft } from '@/models/draft-order';
import { DRAFT_STORAGE_KEY } from '@/stores/draft-order-persistence';
import { useDraftOrderStore } from '@/stores/draft-order-store';

function readStored(): unknown {
  return JSON.parse(sessionStorage.getItem(DRAFT_STORAGE_KEY) ?? 'null');
}

// A fresh Pinia, like a page refresh: the store loads whatever is in sessionStorage.
function reloadedStore(): ReturnType<typeof useDraftOrderStore> {
  setActivePinia(createPinia());
  return useDraftOrderStore();
}

beforeEach(() => {
  sessionStorage.clear();
  setActivePinia(createPinia());
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('draft order store', () => {
  it('startOrder creates an empty draft and persists it immediately', () => {
    const store = useDraftOrderStore();

    store.startOrder();

    expect(store.hasDraft).toBe(true);
    expect(store.draft).toEqual({ lines: [], promoCode: null, tip: null });
    expect(store.itemCount).toBe(0);
    expect(readStored()).toEqual({ version: 1, draft: createEmptyDraft() });
  });

  it('cancelOrder clears the draft in memory and in storage', () => {
    const store = useDraftOrderStore();
    store.startOrder();

    store.cancelOrder();

    expect(store.draft).toBeNull();
    expect(store.hasDraft).toBe(false);
    expect(sessionStorage.getItem(DRAFT_STORAGE_KEY)).toBeNull();
  });

  it('rehydrates a draft with nested changes and sums quantities', () => {
    const store = useDraftOrderStore();
    store.startOrder();
    store.draft?.lines.push(
      { id: 'a', sizeCode: 'large', toppingCodes: ['bacon'], quantity: 2 },
      { id: 'b', sizeCode: 'small', toppingCodes: [], quantity: 3 },
    );

    const reloaded = reloadedStore();

    expect(reloaded.draft).toEqual(store.draft);
    expect(reloaded.itemCount).toBe(5);
  });

  it('start over does not inherit a previous draft', () => {
    sessionStorage.setItem(
      DRAFT_STORAGE_KEY,
      JSON.stringify({
        version: 1,
        draft: {
          lines: [{ id: 'a', sizeCode: 'large', toppingCodes: [], quantity: 2 }],
          promoCode: 'SAVE5',
          tip: { mode: 'amount', value: '2.50' },
        },
      }),
    );
    const store = reloadedStore();
    expect(store.itemCount).toBe(2);

    store.startOrder();

    expect(store.draft).toEqual({ lines: [], promoCode: null, tip: null });
    expect(readStored()).toEqual({ version: 1, draft: createEmptyDraft() });
  });

  it.each<[string, string | null]>([
    ['nothing is stored', null],
    ['the stored value is unparseable', '{not json'],
  ])('starts with no draft when %s', (_name, stored) => {
    if (stored !== null) sessionStorage.setItem(DRAFT_STORAGE_KEY, stored);

    const store = reloadedStore();

    expect(store.draft).toBeNull();
    expect(store.hasDraft).toBe(false);
  });

  it('works in memory only, warning once, when storage is unavailable', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    vi.spyOn(globalThis, 'sessionStorage', 'get').mockImplementation(() => {
      throw new DOMException('denied', 'SecurityError');
    });

    const store = reloadedStore();
    expect(store.draft).toBeNull();

    store.startOrder();
    expect(store.hasDraft).toBe(true);
    store.draft?.lines.push({ id: 'a', sizeCode: 'large', toppingCodes: [], quantity: 1 });
    expect(store.itemCount).toBe(1);
    store.cancelOrder();
    expect(store.hasDraft).toBe(false);

    expect(warn).toHaveBeenCalledTimes(1);
  });
});
