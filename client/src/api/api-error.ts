import type { PriceBreakdown } from './types';

export type ApiErrorKind = 'problem' | 'network' | 'timeout' | 'unexpected';

export interface ApiErrorInit {
  kind: ApiErrorKind;
  status?: number;
  title?: string;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
  quote?: PriceBreakdown;
}

/** Everything that can go wrong talking to the API, in one shape the UI can map to friendly text. */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status: number | undefined;
  readonly title: string | undefined;
  readonly detail: string | undefined;
  readonly code: string | undefined;
  readonly errors: Record<string, string[]>;
  readonly traceId: string | undefined;
  readonly quote: PriceBreakdown | undefined;

  constructor(init: ApiErrorInit) {
    super(init.detail ?? init.title ?? init.kind);
    this.name = 'ApiError';
    this.kind = init.kind;
    this.status = init.status;
    this.title = init.title;
    this.detail = init.detail;
    this.code = init.code;
    this.errors = init.errors ?? {};
    this.traceId = init.traceId;
    this.quote = init.quote;
  }

  /** Worth an automatic retry: the request may never have reached the server, or we gave up waiting. */
  get isTransient(): boolean {
    return this.kind === 'network' || this.kind === 'timeout';
  }
}

export interface FriendlyMessage {
  message: string;
  /** The server's traceId, shown as a reference code so support can find the logs. */
  reference?: string;
}

const GENERIC = 'Something went wrong. Please try again.';

function withReference(result: FriendlyMessage, error: ApiError): FriendlyMessage {
  return error.traceId ? { ...result, reference: error.traceId } : result;
}

/** Plain-language text for any error. Never a stack trace or raw JSON. */
export function friendlyMessage(error: unknown): FriendlyMessage {
  if (!(error instanceof ApiError)) return { message: GENERIC };
  switch (error.kind) {
    case 'network':
      return { message: 'Connection lost. Please check the connection and try again.' };
    case 'timeout':
      return { message: 'This is taking longer than usual. Please try again.' };
    case 'unexpected':
      return withReference({ message: GENERIC }, error);
    case 'problem': {
      const serverError = (error.status ?? 500) >= 500;
      // The server's title is the customer-facing headline; detail adds the "what now". Show both.
      const text =
        error.title && error.detail && error.detail !== error.title
          ? `${error.title}${/[.!?]$/.test(error.title) ? '' : '.'} ${error.detail}`
          : (error.title ?? error.detail);
      if (serverError || !text) return withReference({ message: GENERIC }, error);
      return { message: text };
    }
  }
}

/** One-line text for banners: the message plus "Reference: ..." when there is one. */
export function describeError(error: unknown): string {
  const { message, reference } = friendlyMessage(error);
  return reference ? `${message} (Reference: ${reference})` : message;
}

/** Field errors whose path starts with `prefix` (e.g. 'promoCode', 'tip'), case-insensitive. */
export function fieldMessages(error: unknown, prefix: string): string[] {
  if (!(error instanceof ApiError)) return [];
  const wanted = prefix.toLowerCase();
  const found: string[] = [];
  for (const [path, messages] of Object.entries(error.errors)) {
    if (path.toLowerCase().startsWith(wanted)) found.push(...messages);
  }
  return found;
}

/** Field errors that belong to none of the given prefixes. */
export function otherFieldMessages(error: unknown, knownPrefixes: string[]): string[] {
  if (!(error instanceof ApiError)) return [];
  const known = knownPrefixes.map((p) => p.toLowerCase());
  return Object.entries(error.errors)
    .filter(([path]) => !known.some((p) => path.toLowerCase().startsWith(p)))
    .flatMap(([, messages]) => messages);
}
