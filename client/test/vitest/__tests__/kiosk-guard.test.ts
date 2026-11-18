import { describe, expect, it } from 'vitest';
import { kioskRedirect } from '@/router/kiosk-guard';

type Target = Parameters<typeof kioskRedirect>[0];

const start: Target = { name: 'start', meta: {} };
const order: Target = { name: 'order', meta: { building: true } };
const notFound: Target = { name: undefined, meta: {} };

const routesByName: Record<string, Target> = { start, order };

describe('kioskRedirect', () => {
  const table: [string, Target, boolean, { name: string } | true][] = [
    ['building route without a draft goes to start', order, false, { name: 'start' }],
    ['start with a draft goes to the order', start, true, { name: 'order' }],
    ['start without a draft stays', start, false, true],
    ['building route with a draft stays', order, true, true],
    ['404 without a draft is untouched', notFound, false, true],
    ['404 with a draft is untouched', notFound, true, true],
  ];

  it.each(table)('%s', (_name, to, hasDraft, expected) => {
    expect(kioskRedirect(to, hasDraft)).toEqual(expected);
  });

  it.each(table.filter(([, , , expected]) => expected !== true))(
    'the redirect target of "%s" is not redirected again',
    (_name, _to, hasDraft, expected) => {
      const target = routesByName[(expected as { name: string }).name];
      if (!target) throw new Error('unknown redirect target');

      expect(kioskRedirect(target, hasDraft)).toBe(true);
    },
  );
});
