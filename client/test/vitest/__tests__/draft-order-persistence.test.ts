import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { DraftOrder } from '@/models/draft-order';
import {
  createDraftPersistence,
  DRAFT_STORAGE_KEY,
  parseStoredDraft,
} from '@/stores/draft-order-persistence';

const fullDraft: DraftOrder = {
  lines: [{ id: 'a', sizeCode: 'large', toppingCodes: ['pepperoni', 'bacon'], quantity: 2 }],
  promoCode: 'SAVE5',
  tip: { mode: 'percent', value: '15' },
};

function envelope(draft: unknown, version: unknown = 1): string {
  return JSON.stringify({ version, draft });
}

// A complete Storage (no cast) whose every method fails, like a full or blocked store.
function storageThatThrows(): Storage {
  const fail = (): never => {
    throw new DOMException('full', 'QuotaExceededError');
  };
  return {
    get length(): number {
      return 0;
    },
    key: () => null,
    clear: fail,
    getItem: fail,
    setItem: fail,
    removeItem: fail,
  };
}

beforeEach(() => {
  sessionStorage.clear();
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('parseStoredDraft', () => {
  it('round-trips a valid envelope and keeps only the known fields', () => {
    const raw = JSON.stringify({
      version: 1,
      extra: 'x',
      draft: {
        extra: 1,
        lines: [{ ...fullDraft.lines[0], extra: true }],
        promoCode: 'SAVE5',
        tip: { mode: 'percent', value: '15', extra: 1 },
      },
    });

    expect(parseStoredDraft(raw)).toStrictEqual(fullDraft);
  });

  const validLine = { id: 'a', sizeCode: 'large', toppingCodes: [], quantity: 1 };
  it.each<[string, string | null]>([
    ['null', null],
    ['invalid JSON', '{not json'],
    ['version 2', envelope({ lines: [], promoCode: null, tip: null }, 2)],
    ['missing draft', JSON.stringify({ version: 1 })],
    ['lines not an array', envelope({ lines: {}, promoCode: null, tip: null })],
    [
      'fractional quantity',
      envelope({ lines: [{ ...validLine, quantity: 1.5 }], promoCode: null, tip: null }),
    ],
    [
      'string quantity',
      envelope({ lines: [{ ...validLine, quantity: '2' }], promoCode: null, tip: null }),
    ],
    [
      'toppingCodes not strings',
      envelope({ lines: [{ ...validLine, toppingCodes: [1] }], promoCode: null, tip: null }),
    ],
    ['tip mode cash', envelope({ lines: [], promoCode: null, tip: { mode: 'cash', value: '1' } })],
    [
      'numeric tip value',
      envelope({ lines: [], promoCode: null, tip: { mode: 'amount', value: 1 } }),
    ],
    ['numeric promoCode', envelope({ lines: [], promoCode: 5, tip: null })],
  ])('returns null for %s', (_name, raw) => {
    expect(parseStoredDraft(raw)).toBeNull();
  });
});

describe('createDraftPersistence', () => {
  it('returns null and removes the key when the stored value is unusable', () => {
    sessionStorage.setItem(DRAFT_STORAGE_KEY, '{not json');
    const p = createDraftPersistence();

    expect(p.load()).toBeNull();
    expect(sessionStorage.getItem(DRAFT_STORAGE_KEY)).toBeNull();
  });

  it('saves an envelope and loads it back', () => {
    const p = createDraftPersistence();

    p.save(fullDraft);
    const stored: unknown = JSON.parse(sessionStorage.getItem(DRAFT_STORAGE_KEY) ?? 'null');

    expect(stored).toEqual({ version: 1, draft: fullDraft });
    expect(p.load()).toEqual(fullDraft);
  });

  it.each<[string, () => void]>([
    [
      'the getter throws',
      () => {
        vi.spyOn(globalThis, 'sessionStorage', 'get').mockImplementation(() => {
          throw new DOMException('denied', 'SecurityError');
        });
      },
    ],
    [
      'the methods throw',
      () => {
        vi.spyOn(globalThis, 'sessionStorage', 'get').mockReturnValue(storageThatThrows());
      },
    ],
  ])('never throws and warns exactly once when %s', (_name, breakStorage) => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    breakStorage();
    const p = createDraftPersistence();

    expect(p.load()).toBeNull();
    expect(() => {
      p.save(fullDraft);
      p.save(null);
    }).not.toThrow();
    expect(warn).toHaveBeenCalledTimes(1);
  });
});
