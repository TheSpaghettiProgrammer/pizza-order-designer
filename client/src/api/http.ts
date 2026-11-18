import { ApiError } from './api-error';
import type { PriceBreakdown } from './types';

export const DEFAULT_TIMEOUT_MS = 8000;

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  headers?: Record<string, string>;
  timeoutMs?: number;
  /** Lets the caller abandon the request (e.g. a newer quote supersedes it). */
  signal?: AbortSignal;
}

export interface ApiResponse<T> {
  status: number;
  data: T;
  etag: string | null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function str(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined;
}

function toApiError(status: number, payload: unknown): ApiError {
  if (!isRecord(payload) || (payload.title === undefined && payload.detail === undefined)) {
    return new ApiError({ kind: 'unexpected', status });
  }
  const errors: Record<string, string[]> = {};
  if (isRecord(payload.errors)) {
    for (const [path, messages] of Object.entries(payload.errors)) {
      if (Array.isArray(messages)) {
        errors[path] = messages.filter((m): m is string => typeof m === 'string');
      }
    }
  }
  const init: ConstructorParameters<typeof ApiError>[0] = { kind: 'problem', status, errors };
  const title = str(payload.title);
  const detail = str(payload.detail);
  const code = str(payload.code);
  const traceId = str(payload.traceId);
  if (title !== undefined) init.title = title;
  if (detail !== undefined) init.detail = detail;
  if (code !== undefined) init.code = code;
  if (traceId !== undefined) init.traceId = traceId;
  if (isRecord(payload.quote)) init.quote = payload.quote as unknown as PriceBreakdown;
  return new ApiError(init);
}

/** Thin fetch wrapper: JSON in and out, a timeout, and every failure turned into an ApiError. */
export async function apiRequest<T>(
  path: string,
  options: RequestOptions = {},
): Promise<ApiResponse<T>> {
  const controller = new AbortController();
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, options.timeoutMs ?? DEFAULT_TIMEOUT_MS);
  const onCallerAbort = () => controller.abort();
  options.signal?.addEventListener('abort', onCallerAbort);

  const headers: Record<string, string> = { Accept: 'application/json', ...options.headers };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';

  try {
    let response: Response;
    try {
      response = await fetch(path, {
        method: options.method ?? 'GET',
        headers,
        body: options.body === undefined ? null : JSON.stringify(options.body),
        signal: controller.signal,
      });
    } catch (error) {
      if (timedOut) throw new ApiError({ kind: 'timeout' });
      if (options.signal?.aborted) throw error;
      throw new ApiError({ kind: 'network' });
    }

    let payload: unknown = undefined;
    if (response.status !== 204) {
      try {
        payload = await response.json();
      } catch {
        // A success whose body we couldn't read (e.g. the connection dropped mid-body) is NOT a
        // success to the caller: the outcome is unknown, so treat it like a lost connection.
        if (response.ok) throw new ApiError({ kind: timedOut ? 'timeout' : 'network' });
        payload = undefined;
      }
    }
    if (!response.ok) throw toApiError(response.status, payload);
    return { status: response.status, data: payload as T, etag: response.headers.get('ETag') };
  } finally {
    clearTimeout(timer);
    options.signal?.removeEventListener('abort', onCallerAbort);
  }
}
