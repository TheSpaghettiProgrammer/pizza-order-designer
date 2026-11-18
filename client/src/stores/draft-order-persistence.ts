import { type DraftLine, type DraftOrder, type TipInput } from '@/models/draft-order';

export const DRAFT_STORAGE_KEY = 'pizza-order-designer:draft-order';

// Bump when the DraftOrder shape changes. Older drafts are then discarded on load (fine for a draft).
export const DRAFT_SCHEMA_VERSION = 1;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isStringArray(value: unknown): value is string[] {
  return Array.isArray(value) && value.every((item) => typeof item === 'string');
}

function parseLine(value: unknown): DraftLine | null {
  if (!isRecord(value)) return null;
  const { id, sizeCode, toppingCodes, quantity } = value;
  if (
    typeof id !== 'string' ||
    typeof sizeCode !== 'string' ||
    !isStringArray(toppingCodes) ||
    typeof quantity !== 'number' ||
    !Number.isInteger(quantity)
  ) {
    return null;
  }
  return { id, sizeCode, toppingCodes: [...toppingCodes], quantity };
}

function parseTip(value: unknown): TipInput | null | undefined {
  if (value === null) return null;
  if (!isRecord(value)) return undefined;
  const { mode, value: amount } = value;
  if ((mode !== 'amount' && mode !== 'percent') || typeof amount !== 'string') return undefined;
  return { mode, value: amount };
}

/** Pure and never throws. Returns a fresh object with only the known fields, or null if unusable. */
export function parseStoredDraft(raw: string | null): DraftOrder | null {
  if (raw === null) return null;

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return null;
  }

  if (!isRecord(parsed) || parsed.version !== DRAFT_SCHEMA_VERSION) return null;
  const draft = parsed.draft;
  if (!isRecord(draft) || !Array.isArray(draft.lines)) return null;

  const lines: DraftLine[] = [];
  for (const item of draft.lines as unknown[]) {
    const line = parseLine(item);
    if (line === null) return null;
    lines.push(line);
  }

  const promoCode = draft.promoCode;
  if (promoCode !== null && typeof promoCode !== 'string') return null;

  const tip = parseTip(draft.tip);
  if (tip === undefined) return null;

  return { lines, promoCode, tip };
}

export interface DraftPersistence {
  load(): DraftOrder | null;
  save(draft: DraftOrder | null): void;
}

/** One instance per store, so broken storage warns once per page load. */
export function createDraftPersistence(): DraftPersistence {
  let warned = false;

  function warnOnce(error: unknown): void {
    if (warned) return;
    warned = true;
    console.warn("The order can't be saved in this browser, so it won't survive a refresh.", error);
  }

  return {
    load() {
      try {
        const storage = globalThis.sessionStorage;
        const raw = storage.getItem(DRAFT_STORAGE_KEY);
        const draft = parseStoredDraft(raw);
        if (draft === null && raw !== null) storage.removeItem(DRAFT_STORAGE_KEY);
        return draft;
      } catch (error) {
        warnOnce(error);
        return null;
      }
    },

    save(draft) {
      try {
        const storage = globalThis.sessionStorage;
        if (draft === null) {
          storage.removeItem(DRAFT_STORAGE_KEY);
        } else {
          storage.setItem(
            DRAFT_STORAGE_KEY,
            JSON.stringify({ version: DRAFT_SCHEMA_VERSION, draft }),
          );
        }
      } catch (error) {
        warnOnce(error);
      }
    },
  };
}
