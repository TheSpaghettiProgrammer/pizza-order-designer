import type { OrderDraftBody } from '@/api/types';

// The customer's order while they are still building it. Menu codes only, never prices: the server
// does all pricing (D-039). The only number is `quantity`, a count.

export interface DraftLine {
  /** Client-side key for edit/remove and v-for; #26 generates it. */
  id: string;
  /** Menu code from #8, e.g. 'large' (codes, never DB ids or prices). */
  sizeCode: string;
  /** Menu codes; a set per D-043, which #26's UI and the server enforce. */
  toppingCodes: string[];
  /** Integer count; the 1-20 limit is a server rule (D-043). */
  quantity: number;
}

export type TipMode = 'amount' | 'percent';

export interface TipInput {
  mode: TipMode;
  /** Exactly what the customer typed, e.g. '2.50' or '15'. A string, so no float touches money. */
  value: string;
}

export interface DraftOrder {
  lines: DraftLine[];
  /** One code per order (D-043); raw input, the server trims and normalizes it. */
  promoCode: string | null;
  /** null = no tip chosen yet. */
  tip: TipInput | null;
}

export function createEmptyDraft(): DraftOrder {
  return { lines: [], promoCode: null, tip: null };
}

/** The request body the API expects: codes and the customer's raw input only, never prices. */
export function toOrderBody(draft: DraftOrder): OrderDraftBody {
  const tip = draft.tip && draft.tip.value.trim() !== '' ? { ...draft.tip } : null;
  return {
    lines: draft.lines.map((l) => ({
      sizeCode: l.sizeCode,
      toppingCodes: [...l.toppingCodes],
      quantity: l.quantity,
    })),
    promoCode: draft.promoCode,
    tip,
  };
}
