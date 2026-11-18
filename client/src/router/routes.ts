import type { RouteRecordRaw } from 'vue-router';

declare module 'vue-router' {
  interface RouteMeta {
    /** An order is being built here: needs an open draft, shows Cancel order. */
    building?: boolean;
  }
}

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: () => import('@/layouts/MainLayout.vue'),
    children: [
      { path: '', name: 'start', component: () => import('@/pages/StartPage.vue') },
      {
        path: 'order',
        name: 'order',
        component: () => import('@/pages/OrderPage.vue'),
        meta: { building: true },
      },
      // Not a building route: the draft is already gone once an order is placed.
      {
        path: 'orders/:id',
        name: 'order-status',
        component: () => import('@/pages/OrderStatusPage.vue'),
      },
    ],
  },

  // Always leave this as last one,
  // but you can also remove it
  {
    path: '/:catchAll(.*)*',
    component: () => import('@/pages/ErrorNotFound.vue'),
  },
];

export default routes;
