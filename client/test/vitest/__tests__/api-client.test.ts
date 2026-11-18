import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, describeError, fieldMessages, friendlyMessage } from '@/api/api-error';
import { apiRequest } from '@/api/http';
import { newId } from '@/utils/id';

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

function stubFetch(impl: (input: string, init: RequestInit) => Promise<Response>): void {
  vi.stubGlobal('fetch', vi.fn(impl));
}

function problem(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

async function failure(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise;
  } catch (error) {
    if (error instanceof ApiError) return error;
    throw error;
  }
  throw new Error('expected the request to fail');
}

describe('apiRequest error mapping', () => {
  it('maps ProblemDetails, including field errors and the quote extension', async () => {
    stubFetch(() =>
      Promise.resolve(
        problem(409, {
          title: 'The price changed',
          detail: 'The total is now $10.00.',
          status: 409,
          code: 'price.changed',
          traceId: 'abc123',
          errors: { promoCode: ['Not valid.'] },
          quote: { total: 10 },
        }),
      ),
    );
    const error = await failure(apiRequest('/api/orders', { method: 'POST', body: {} }));

    expect(error.kind).toBe('problem');
    expect(error.status).toBe(409);
    expect(error.code).toBe('price.changed');
    expect(error.errors).toEqual({ promoCode: ['Not valid.'] });
    expect(error.quote?.total).toBe(10);
    expect(friendlyMessage(error)).toEqual({ message: 'The total is now $10.00.' });
  });

  it('maps a dropped connection to a network error with friendly text', async () => {
    stubFetch(() => Promise.reject(new TypeError('Failed to fetch')));
    const error = await failure(apiRequest('/api/menu'));

    expect(error.kind).toBe('network');
    expect(error.isTransient).toBe(true);
    expect(friendlyMessage(error).message).toMatch(/connection lost/i);
  });

  it('maps a slow server to a timeout error', async () => {
    vi.useFakeTimers();
    stubFetch(
      (_path, init) =>
        new Promise((_resolve, reject) => {
          init.signal?.addEventListener('abort', () =>
            reject(new DOMException('aborted', 'AbortError')),
          );
        }),
    );
    const pending = failure(apiRequest('/api/menu', { timeoutMs: 1000 }));
    await vi.advanceTimersByTimeAsync(1001);
    const error = await pending;

    expect(error.kind).toBe('timeout');
    expect(error.isTransient).toBe(true);
  });

  it('shows "Something went wrong" plus the traceId for a server error', async () => {
    stubFetch(() =>
      Promise.resolve(
        problem(500, { title: 'boom', detail: 'System.Exception at ...', traceId: 't-1' }),
      ),
    );
    const error = await failure(apiRequest('/api/menu'));

    expect(error.isTransient).toBe(false);
    const friendly = friendlyMessage(error);
    expect(friendly.message).toMatch(/something went wrong/i);
    expect(friendly.message).not.toContain('Exception');
    expect(friendly.reference).toBe('t-1');
    expect(describeError(error)).toContain('Reference: t-1');
  });

  it('treats a non-JSON error body as unexpected without leaking it', async () => {
    stubFetch(() => Promise.resolve(new Response('<html>Bad gateway</html>', { status: 502 })));
    const error = await failure(apiRequest('/api/menu'));

    expect(error.kind).toBe('unexpected');
    expect(friendlyMessage(error).message).not.toContain('html');
  });

  it('returns undefined data for a 204', async () => {
    stubFetch(() => Promise.resolve(new Response(null, { status: 204 })));
    expect((await apiRequest('/api/orders/1', { method: 'DELETE' })).data).toBeUndefined();
  });

  it('filters field errors by path prefix', () => {
    const error = new ApiError({
      kind: 'problem',
      status: 400,
      errors: { promoCode: ['bad code'], 'tip.value': ['bad tip'] },
    });
    expect(fieldMessages(error, 'promoCode')).toEqual(['bad code']);
    expect(fieldMessages(error, 'tip')).toEqual(['bad tip']);
    expect(fieldMessages(new Error('x'), 'tip')).toEqual([]);
  });
});

describe('newId', () => {
  it('works when crypto.randomUUID is unavailable (plain-HTTP origins)', () => {
    vi.stubGlobal('crypto', { getRandomValues: (a: Uint8Array) => a.fill(7) });
    expect(newId()).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
    );
    vi.stubGlobal('crypto', undefined);
    expect(newId()).toMatch(/^[0-9a-f-]{36}$/);
  });
});
