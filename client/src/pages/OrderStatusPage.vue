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

    <q-card v-if="order" flat bordered>
      <q-card-section>
        <div class="text-h6 text-positive">Thank you! Your order has been placed.</div>
        <p class="q-mt-sm q-mb-none">
          Order number: <strong data-testid="order-id">{{ order.id }}</strong>
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
import { onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { describeError } from '@/api/api-error';
import { getOrder } from '@/api/orders-api';
import type { Order } from '@/api/types';
import { formatMoney } from '@/utils/format';

const route = useRoute();
const router = useRouter();
const order = ref<Order | null>(null);
const error = ref<unknown>(null);

onMounted(async () => {
  try {
    order.value = await getOrder(String(route.params.id));
  } catch (e) {
    error.value = e;
  }
});

function startNew(): void {
  void router.push({ name: 'start' });
}
</script>

<style scoped>
.description {
  white-space: pre-line;
}
</style>
