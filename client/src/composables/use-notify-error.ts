import { Notify } from 'quasar';
import { friendlyMessage } from '@/api/api-error';

/** General (non-field) errors: a Quasar banner with friendly text and, if any, a reference code. */
export function notifyError(error: unknown): void {
  const { message, reference } = friendlyMessage(error);
  Notify.create({
    type: 'negative',
    message,
    caption: reference ? `Reference: ${reference}` : '',
    position: 'top',
    timeout: 6000,
    actions: [{ label: 'Dismiss', color: 'white' }],
  });
}
