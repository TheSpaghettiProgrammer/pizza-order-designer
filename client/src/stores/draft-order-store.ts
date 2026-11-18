import { defineStore } from 'pinia';
import { computed, ref, watch } from 'vue';
import { createEmptyDraft, type DraftOrder } from '@/models/draft-order';
import { createDraftPersistence } from './draft-order-persistence';

export const useDraftOrderStore = defineStore('draftOrder', () => {
  const persistence = createDraftPersistence();

  // null = no order in progress (the start screen). Loading here means every new Pinia rehydrates.
  const draft = ref<DraftOrder | null>(persistence.load());

  const hasDraft = computed(() => draft.value !== null);
  const itemCount = computed(
    () => draft.value?.lines.reduce((count, line) => count + line.quantity, 0) ?? 0,
  );

  // Always a fresh draft: a new order never inherits the previous customer's lines.
  function startOrder(): void {
    draft.value = createEmptyDraft();
  }

  function cancelOrder(): void {
    draft.value = null;
  }

  // Sync + deep: every mutation is in storage before the next statement runs.
  watch(draft, (value) => persistence.save(value), { deep: true, flush: 'sync' });

  return { draft, hasDraft, itemCount, startOrder, cancelOrder };
});
