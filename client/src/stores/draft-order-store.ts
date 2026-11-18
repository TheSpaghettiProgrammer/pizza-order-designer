import { defineStore } from 'pinia';
import { computed, ref, watch } from 'vue';
import { createEmptyDraft, type DraftOrder, type TipInput } from '@/models/draft-order';
import { newId } from '@/utils/id';
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

  interface LineInput {
    sizeCode: string;
    toppingCodes: string[];
    quantity: number;
  }

  function addLine(input: LineInput): void {
    draft.value?.lines.push({
      id: newId(),
      sizeCode: input.sizeCode,
      toppingCodes: [...input.toppingCodes],
      quantity: input.quantity,
    });
  }

  function updateLine(id: string, input: LineInput): void {
    const line = draft.value?.lines.find((l) => l.id === id);
    if (!line) return;
    line.sizeCode = input.sizeCode;
    line.toppingCodes = [...input.toppingCodes];
    line.quantity = input.quantity;
  }

  function removeLine(id: string): void {
    if (!draft.value) return;
    draft.value.lines = draft.value.lines.filter((l) => l.id !== id);
  }

  function setQuantity(id: string, quantity: number): void {
    const line = draft.value?.lines.find((l) => l.id === id);
    if (line) line.quantity = quantity;
  }

  /** Raw input; the server trims and normalizes. Blank clears the code. */
  function setPromoCode(code: string | null): void {
    if (!draft.value) return;
    const trimmed = code?.trim() ?? '';
    draft.value.promoCode = trimmed === '' ? null : trimmed;
  }

  function setTip(tip: TipInput | null): void {
    if (draft.value) draft.value.tip = tip;
  }

  // Sync + deep: every mutation is in storage before the next statement runs.
  watch(draft, (value) => persistence.save(value), { deep: true, flush: 'sync' });

  return {
    draft,
    hasDraft,
    itemCount,
    startOrder,
    cancelOrder,
    addLine,
    updateLine,
    removeLine,
    setQuantity,
    setPromoCode,
    setTip,
  };
});
