import type { RouteLocationNormalized, RouteLocationRaw } from 'vue-router';

/**
 * Kiosk rules. Building routes need an open order; while an order is open the start screen is
 * unreachable, so Cancel order is the only exit. Anything else (the 404 page) is left alone.
 */
export function kioskRedirect(
  to: Pick<RouteLocationNormalized, 'name' | 'meta'>,
  hasDraft: boolean,
): RouteLocationRaw | true {
  if (to.meta.building && !hasDraft) return { name: 'start' };
  if (to.name === 'start' && hasDraft) return { name: 'order' };
  return true;
}
