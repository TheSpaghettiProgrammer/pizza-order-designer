import { useRouter } from 'vue-router';
import { useDraftOrderStore } from '@/stores/draft-order-store';

/** The one way out of an order. Call it from component setup (it uses the router). */
export function useEndOrder(): { endOrder: () => Promise<void> } {
  const store = useDraftOrderStore();
  const router = useRouter();

  // Clear the draft FIRST, then navigate: while a draft exists the guard sends `/` back to `/order`.
  // Used by Cancel order (#25) and by the idle-timeout countdown (#44).
  async function endOrder(): Promise<void> {
    store.cancelOrder();
    await router.push({ name: 'start' });
  }

  return { endOrder };
}
