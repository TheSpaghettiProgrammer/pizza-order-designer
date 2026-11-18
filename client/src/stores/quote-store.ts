import { defineStore } from 'pinia';
import { ref, watch } from 'vue';
import { quoteOrder } from '@/api/orders-api';
import type { PriceBreakdown } from '@/api/types';
import { toOrderBody } from '@/models/draft-order';
import { useDraftOrderStore } from './draft-order-store';

export const QUOTE_DEBOUNCE_MS = 300;

/**
 * The running total. Every number comes from POST /api/orders/quote (D-039): the draft is debounced,
 * sent, and only the answer to the LATEST request is kept. Older answers (a slow earlier request
 * finishing after a newer one) are ignored via a sequence number.
 */
export const useQuoteStore = defineStore('quote', () => {
  const draftStore = useDraftOrderStore();

  const breakdown = ref<PriceBreakdown | null>(null);
  const error = ref<unknown>(null);
  /** True from the first change until the matching answer arrives ("updating…"). */
  const updating = ref(false);

  let sequence = 0;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let inFlight: AbortController | undefined;

  function reset(): void {
    sequence += 1; // invalidates anything still in flight
    clearTimeout(timer);
    inFlight?.abort();
    breakdown.value = null;
    error.value = null;
    updating.value = false;
  }

  async function fetchQuote(mine: number): Promise<void> {
    const draft = draftStore.draft;
    if (!draft || draft.lines.length === 0) {
      reset();
      return;
    }
    inFlight?.abort();
    const controller = new AbortController();
    inFlight = controller;
    try {
      const result = await quoteOrder(toOrderBody(draft), controller.signal);
      if (mine !== sequence) return; // stale
      breakdown.value = result;
      error.value = null;
    } catch (e) {
      if (mine !== sequence) return; // stale
      breakdown.value = null;
      error.value = e;
    } finally {
      if (mine === sequence) updating.value = false;
    }
  }

  function schedule(delay: number): void {
    const draft = draftStore.draft;
    if (!draft || draft.lines.length === 0) {
      reset();
      return;
    }
    sequence += 1;
    const mine = sequence;
    updating.value = true;
    clearTimeout(timer);
    timer = setTimeout(() => void fetchQuote(mine), delay);
  }

  /** Take a price the server handed back elsewhere (a 409 price.changed carries the new quote). */
  function adopt(next: PriceBreakdown): void {
    sequence += 1;
    clearTimeout(timer);
    inFlight?.abort();
    breakdown.value = next;
    error.value = null;
    updating.value = false;
  }

  watch(
    () => draftStore.draft && JSON.stringify(toOrderBody(draftStore.draft)),
    (value, previous) => {
      if (value === previous) return;
      schedule(QUOTE_DEBOUNCE_MS);
    },
  );

  // Pick up a restored draft (refresh) straight away.
  if (draftStore.draft && draftStore.draft.lines.length > 0) schedule(0);

  return { breakdown, error, updating, adopt, reset };
});
