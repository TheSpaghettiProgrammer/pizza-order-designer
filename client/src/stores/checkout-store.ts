import { defineStore } from 'pinia';
import { computed, ref, watch } from 'vue';
import { ApiError } from '@/api/api-error';
import { submitOrder } from '@/api/orders-api';
import type { Order, SubmitOrderBody } from '@/api/types';
import { toOrderBody } from '@/models/draft-order';
import { newId } from '@/utils/id';
import { useDraftOrderStore } from './draft-order-store';
import { useQuoteStore } from './quote-store';

export const MAX_ATTEMPTS = 3;
export const BACKOFF_MS = 500;
const SLOW_AFTER_MS = 3000;

export type CheckoutPhase = 'idle' | 'sending' | 'failed' | 'priceChanged';

const sleep = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

/**
 * Retry-safe checkout (#30). The Idempotency-Key belongs to one request BODY: automatic retries and
 * the manual Retry button resend the same body, so they reuse the key and the server answers with
 * the original order instead of creating a second one. A different body (the customer edited the
 * order, or confirmed a new total after price.changed) gets a NEW key, because the same key with a
 * different body is rejected (422).
 */
export const useCheckoutStore = defineStore('checkout', () => {
  const draftStore = useDraftOrderStore();
  const quoteStore = useQuoteStore();

  const phase = ref<CheckoutPhase>('idle');
  const attempt = ref(0);
  const slow = ref(false);
  const error = ref<unknown>(null);
  const changedTotal = ref<number | null>(null);

  let key: string | null = null;
  let keyedBody: string | null = null;

  const stillSending = computed(
    () => phase.value === 'sending' && (attempt.value > 1 || slow.value),
  );

  function keyFor(body: SubmitOrderBody): string {
    const signature = JSON.stringify(body);
    if (key === null || signature !== keyedBody) {
      key = newId();
      keyedBody = signature;
    }
    return key;
  }

  function forgetKey(): void {
    key = null;
    keyedBody = null;
  }

  function reset(): void {
    phase.value = 'idle';
    attempt.value = 0;
    slow.value = false;
    error.value = null;
    changedTotal.value = null;
    forgetKey();
  }

  /** Returns the placed order, or null when it failed (phase and error say why). */
  async function submit(): Promise<Order | null> {
    if (phase.value === 'sending') return null;
    const draft = draftStore.draft;
    const quote = quoteStore.breakdown;
    if (!draft || draft.lines.length === 0 || !quote || quoteStore.updating) return null;

    const body: SubmitOrderBody = { ...toOrderBody(draft), expectedTotal: quote.total };
    const idempotencyKey = keyFor(body);

    phase.value = 'sending';
    error.value = null;
    changedTotal.value = null;
    slow.value = false;
    const slowTimer = setTimeout(() => (slow.value = true), SLOW_AFTER_MS);

    try {
      for (let n = 1; n <= MAX_ATTEMPTS; n++) {
        attempt.value = n;
        try {
          const response = await submitOrder(body, idempotencyKey);
          phase.value = 'idle';
          forgetKey();
          return response.data;
        } catch (e) {
          const retry = e instanceof ApiError && e.isTransient && n < MAX_ATTEMPTS;
          if (!retry) throw e;
          await sleep(BACKOFF_MS * n);
        }
      }
      return null;
    } catch (e) {
      error.value = e;
      if (e instanceof ApiError && e.code === 'price.changed' && e.quote) {
        quoteStore.adopt(e.quote);
        changedTotal.value = e.quote.total;
        phase.value = 'priceChanged';
      } else {
        // 4xx other than a price change are not retried with this key (it may be bound to a body
        // the server rejected); 5xx and network failures may be, hence the manual Retry button.
        if (e instanceof ApiError && !e.isTransient && (e.status ?? 0) < 500) forgetKey();
        phase.value = 'failed';
      }
      return null;
    } finally {
      clearTimeout(slowTimer);
      slow.value = false;
    }
  }

  // Editing the order while a failure or price-change banner is up makes the banner stale.
  watch(
    () => draftStore.draft && JSON.stringify(toOrderBody(draftStore.draft)),
    () => {
      if (phase.value === 'failed' || phase.value === 'priceChanged') {
        phase.value = 'idle';
        changedTotal.value = null;
        error.value = null;
      }
    },
  );

  /** The customer declined the new total and goes back to the order. */
  function dismissPriceChange(): void {
    phase.value = 'idle';
    changedTotal.value = null;
    error.value = null;
  }

  return { phase, attempt, stillSending, error, changedTotal, submit, dismissPriceChange, reset };
});
