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

/** sessionStorage key for a submit whose outcome we don't know yet (survives a refresh). */
export const PENDING_SUBMIT_KEY = 'pizza-order-designer:pending-submit';

export type CheckoutPhase = 'idle' | 'sending' | 'failed' | 'priceChanged';

interface PendingSubmit {
  key: string;
  body: SubmitOrderBody;
}

const sleep = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

function loadPending(): PendingSubmit | null {
  try {
    const raw = globalThis.sessionStorage.getItem(PENDING_SUBMIT_KEY);
    if (!raw) return null;
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== 'object' || parsed === null) return null;
    const candidate = parsed as Partial<PendingSubmit>;
    return typeof candidate.key === 'string' && typeof candidate.body === 'object' && candidate.body
      ? { key: candidate.key, body: candidate.body }
      : null;
  } catch {
    return null;
  }
}

function savePending(pending: PendingSubmit | null): void {
  try {
    if (pending) globalThis.sessionStorage.setItem(PENDING_SUBMIT_KEY, JSON.stringify(pending));
    else globalThis.sessionStorage.removeItem(PENDING_SUBMIT_KEY);
  } catch {
    // Storage unavailable: the key still lives in memory for this page.
  }
}

/** The server may have saved the order even though we got no answer (or no readable answer). */
function outcomeIsUnclear(error: unknown): boolean {
  return error instanceof ApiError && (error.isTransient || (error.status ?? 0) >= 500);
}

/**
 * Retry-safe checkout (#30). The Idempotency-Key belongs to one request BODY: automatic retries and
 * the manual Retry button resend exactly that body with the same key, so the server answers with the
 * original order instead of creating a second one.
 *
 * While a submit is in flight, or failed in a way where the server may already have the order
 * (network error, timeout, 5xx), the order is LOCKED: no edits, no Cancel order, only Retry. That is
 * what stops "edit the tip, tap Place order" from minting a new key and placing a second order. The
 * pending key and body are kept in sessionStorage, so a refresh doesn't lose them either.
 * A definite rejection (400, or 409 price.changed) unlocks the order: nothing was stored.
 */
export const useCheckoutStore = defineStore('checkout', () => {
  const draftStore = useDraftOrderStore();
  const quoteStore = useQuoteStore();

  const restored = draftStore.draft ? loadPending() : null;
  if (!restored) savePending(null);

  const phase = ref<CheckoutPhase>(restored ? 'failed' : 'idle');
  const outcomeUnclear = ref(restored !== null);
  const attempt = ref(0);
  const slow = ref(false);
  const error = ref<unknown>(null);
  const changedTotal = ref<number | null>(null);
  let pending: PendingSubmit | null = restored;

  const stillSending = computed(
    () => phase.value === 'sending' && (attempt.value > 1 || slow.value),
  );

  /** True while the order must not change: a submit is in flight or its outcome is unknown. */
  const locked = computed(
    () => phase.value === 'sending' || (phase.value === 'failed' && outcomeUnclear.value),
  );

  function clearPending(): void {
    pending = null;
    outcomeUnclear.value = false;
    savePending(null);
  }

  function reset(): void {
    phase.value = 'idle';
    attempt.value = 0;
    slow.value = false;
    error.value = null;
    changedTotal.value = null;
    clearPending();
  }

  /** Returns the placed order, or null when it failed (phase and error say why). */
  async function submit(): Promise<Order | null> {
    if (phase.value === 'sending') return null;

    // A submit with an unknown outcome is resolved by resending EXACTLY the same request.
    if (!pending) {
      const draft = draftStore.draft;
      const quote = quoteStore.breakdown;
      if (!draft || draft.lines.length === 0 || !quote || quoteStore.updating) return null;
      pending = { key: newId(), body: { ...toOrderBody(draft), expectedTotal: quote.total } };
    }
    const { key, body } = pending;
    savePending(pending);

    phase.value = 'sending';
    error.value = null;
    changedTotal.value = null;
    slow.value = false;
    const slowTimer = setTimeout(() => (slow.value = true), SLOW_AFTER_MS);

    try {
      for (let n = 1; n <= MAX_ATTEMPTS; n++) {
        attempt.value = n;
        try {
          const response = await submitOrder(body, key);
          phase.value = 'idle';
          clearPending();
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
      if (outcomeIsUnclear(e)) {
        // Keep the key and body: Retry must resend the same request (the server may have the order).
        outcomeUnclear.value = true;
        phase.value = 'failed';
      } else if (e instanceof ApiError && e.code === 'price.changed' && e.quote) {
        clearPending(); // nothing was stored; confirming the new total is a new request with a new key
        quoteStore.adopt(e.quote);
        changedTotal.value = e.quote.total;
        phase.value = 'priceChanged';
      } else {
        clearPending(); // a definite rejection: nothing was stored
        phase.value = 'failed';
      }
      return null;
    } finally {
      clearTimeout(slowTimer);
      slow.value = false;
    }
  }

  // Editing the order after a definite rejection makes the banner stale. (While locked, edits are
  // blocked in the UI, so an unclear outcome is never cleared this way.)
  watch(
    () => draftStore.draft && JSON.stringify(toOrderBody(draftStore.draft)),
    () => {
      if (phase.value === 'priceChanged' || (phase.value === 'failed' && !outcomeUnclear.value)) {
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

  return {
    phase,
    attempt,
    stillSending,
    locked,
    outcomeUnclear,
    error,
    changedTotal,
    submit,
    dismissPriceChange,
    reset,
  };
});
