import { useRouter } from 'vue-router';
import { useCheckoutStore } from '@/stores/checkout-store';
import { useDraftOrderStore } from '@/stores/draft-order-store';
import { useQuoteStore } from '@/stores/quote-store';

/** The one way out of an order. Call it from component setup (it uses the router). */
export function useEndOrder(): { endOrder: () => Promise<void> } {
  const store = useDraftOrderStore();
  const checkout = useCheckoutStore();
  const quote = useQuoteStore();
  const router = useRouter();

  // Clear the draft FIRST, then navigate: while a draft exists the guard sends `/` back to `/order`.
  // Used by Cancel order (#25) and by the idle-timeout countdown (#44). Refused while a submit is
  // in flight or its outcome is unknown: the order may already be at the kitchen.
  async function endOrder(): Promise<void> {
    if (checkout.locked) return;
    store.cancelOrder();
    quote.reset();
    checkout.reset();
    await router.push({ name: 'start' });
  }

  return { endOrder };
}
