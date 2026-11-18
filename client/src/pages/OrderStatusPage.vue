<template>
  <q-page class="q-pa-md">
    <h1 class="text-h4 q-mt-none">Your order</h1>

    <q-banner
      v-if="error"
      class="bg-negative text-white q-mb-md"
      rounded
      data-testid="status-error"
    >
      {{ describeError(error) }}
    </q-banner>

    <q-banner
      v-if="notice"
      class="bg-warning text-black q-mb-md"
      rounded
      data-testid="status-notice"
    >
      {{ notice }}
    </q-banner>

    <q-card v-if="cancelled" flat bordered data-testid="cancelled">
      <q-card-section>
        <div class="text-h6">Your order has been cancelled.</div>
        <p class="q-mb-none">Nothing will be made or charged for it.</p>
      </q-card-section>
    </q-card>

    <q-card v-else-if="order" flat bordered>
      <q-card-section>
        <div class="text-h6 text-positive">Thank you! Your order has been placed.</div>
        <p class="q-mt-sm q-mb-none">
          Order number: <strong data-testid="order-id">{{ order.id }}</strong>
        </p>
      </q-card-section>

      <q-separator />
      <q-card-section>
        <ol class="steps" aria-label="Order progress">
          <li
            v-for="(step, i) in STEPS"
            :key="step.status"
            :class="{ done: i <= currentIndex, current: i === currentIndex }"
            :aria-current="i === currentIndex ? 'step' : undefined"
            :data-testid="`step-${step.status}`"
          >
            <q-icon
              :name="i <= currentIndex ? 'check_circle' : 'radio_button_unchecked'"
              size="sm"
            />
            {{ step.label }}
          </li>
        </ol>
        <p class="q-mt-md q-mb-none text-subtitle1" role="status" data-testid="status-text">
          {{ STEPS[currentIndex]?.message }}
        </p>
      </q-card-section>

      <q-separator />
      <q-card-section>
        <div class="description" data-testid="order-description">
          {{ order.pricing.description }}
        </div>
        <p class="text-h6 q-mt-md q-mb-none">
          Total: <span data-testid="order-total">{{ formatMoney(order.pricing.total) }}</span>
        </p>
      </q-card-section>

      <q-card-actions v-if="order.status === 'NotStarted'" class="q-pa-md">
        <q-btn
          outline
          color="negative"
          no-caps
          size="lg"
          icon="close"
          label="Cancel this order"
          :loading="cancelling"
          data-testid="cancel-order"
          @click="confirmCancel"
        />
      </q-card-actions>
    </q-card>

    <p v-else-if="!error">Loading your order…</p>

    <q-btn
      class="q-mt-md"
      color="primary"
      unelevated
      no-caps
      size="lg"
      label="Start a new order"
      @click="startNew"
    />
  </q-page>
</template>

<script setup lang="ts">
import { useQuasar } from 'quasar';
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ApiError, describeError } from '@/api/api-error';
import { cancelOrderRequest, getOrder } from '@/api/orders-api';
import type { Order, OrderStatus } from '@/api/types';
import { notifyError } from '@/composables/use-notify-error';
import { formatMoney } from '@/utils/format';

const POLL_MS = 3000;

const STEPS: { status: OrderStatus; label: string; message: string }[] = [
  {
    status: 'NotStarted',
    label: 'Received',
    message: 'We have your order. The kitchen has not started yet.',
  },
  { status: 'Started', label: 'Preparing', message: 'The kitchen is making your pizza.' },
  {
    status: 'ReadyForPickup',
    label: 'Ready for pickup',
    message: 'Your order is ready. Please pick it up.',
  },
  { status: 'Delivered', label: 'Delivered', message: 'Enjoy your pizza!' },
];

const $q = useQuasar();
const route = useRoute();
const router = useRouter();
const id = String(route.params.id);

const order = ref<Order | null>(null);
const error = ref<unknown>(null);
const notice = ref('');
const cancelled = ref(false);
const cancelling = ref(false);

const currentIndex = computed(() =>
  Math.max(
    0,
    STEPS.findIndex((s) => s.status === order.value?.status),
  ),
);

let timer: ReturnType<typeof setTimeout> | undefined;
let stopped = false;

async function refresh(): Promise<void> {
  try {
    order.value = await getOrder(id);
    error.value = null;
  } catch (e) {
    // Keep showing the last known status; a blip should not wipe the screen.
    error.value = e;
    if (e instanceof ApiError && e.status === 404) stopped = true;
  }
}

function scheduleNext(): void {
  if (stopped) return;
  timer = setTimeout(() => {
    void poll();
  }, POLL_MS);
}

async function poll(): Promise<void> {
  await refresh();
  if (order.value?.status === 'Delivered') stopped = true;
  scheduleNext();
}

onMounted(() => void poll());
onBeforeUnmount(() => {
  stopped = true;
  clearTimeout(timer);
});

function confirmCancel(): void {
  $q.dialog({
    title: 'Cancel this order?',
    message: 'The kitchen has not started yet, so you can still cancel.',
    cancel: { label: 'Keep my order', flat: true, noCaps: true },
    ok: { label: 'Yes, cancel order', color: 'negative', noCaps: true },
    focus: 'cancel',
  }).onOk(() => {
    void cancel();
  });
}

async function cancel(): Promise<void> {
  cancelling.value = true;
  try {
    await cancelOrderRequest(id);
    markCancelled();
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) {
      // D-040: a 404 on a cancel (retry) means it is already cancelled.
      markCancelled();
    } else if (e instanceof ApiError && e.code === 'order.already_started') {
      notice.value =
        'Sorry, the kitchen has just started your order, so it can no longer be cancelled.';
      await refresh();
    } else {
      notifyError(e);
    }
  } finally {
    cancelling.value = false;
  }
}

function markCancelled(): void {
  cancelled.value = true;
  notice.value = '';
  error.value = null;
  stopped = true;
  clearTimeout(timer);
}

function startNew(): void {
  void router.push({ name: 'start' });
}
</script>

<style scoped>
.description {
  white-space: pre-line;
}

.steps {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  list-style: none;
  margin: 0;
  padding: 0;
  color: grey;
}

.steps li.done {
  color: var(--q-primary);
}

.steps li.current {
  font-weight: bold;
}
</style>
