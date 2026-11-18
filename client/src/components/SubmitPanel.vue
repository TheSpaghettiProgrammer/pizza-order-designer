<template>
  <q-card flat bordered>
    <q-card-section>
      <q-banner
        v-if="checkout.phase === 'priceChanged' && checkout.changedTotal !== null"
        class="bg-warning text-black q-mb-md"
        rounded
        data-testid="price-changed"
      >
        The price changed. Your new total is
        <strong>{{ formatMoney(checkout.changedTotal) }}</strong>. Please confirm to place your
        order at this price.
        <template #action>
          <q-btn flat no-caps label="Review order" @click="checkout.dismissPriceChange()" />
          <q-btn
            unelevated
            no-caps
            color="primary"
            label="Confirm new total"
            data-testid="confirm-price"
            @click="place"
          />
        </template>
      </q-banner>

      <q-banner
        v-else-if="checkout.phase === 'failed'"
        class="bg-negative text-white q-mb-md"
        rounded
        data-testid="submit-failed"
      >
        {{ describeError(checkout.error) }}
        <template #action>
          <q-btn flat no-caps label="Retry" data-testid="retry" @click="place" />
        </template>
      </q-banner>

      <p v-if="checkout.stillSending" class="q-mb-md" role="status" data-testid="still-sending">
        <q-spinner size="1em" class="q-mr-xs" />
        Still sending… Connection lost, retrying.
      </p>

      <q-btn
        v-if="checkout.phase !== 'priceChanged'"
        color="primary"
        unelevated
        no-caps
        size="xl"
        class="full-width place-button"
        :loading="checkout.phase === 'sending'"
        :disable="!canPlace"
        label="Place order"
        data-testid="place-order"
        @click="place"
      />
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useRouter } from 'vue-router';
import { describeError } from '@/api/api-error';
import { useCheckoutStore } from '@/stores/checkout-store';
import { useDraftOrderStore } from '@/stores/draft-order-store';
import { useQuoteStore } from '@/stores/quote-store';
import { formatMoney } from '@/utils/format';

const checkout = useCheckoutStore();
const draftStore = useDraftOrderStore();
const quote = useQuoteStore();
const router = useRouter();

// An order needs at least one line and a fresh server price.
const canPlace = computed(
  () => (draftStore.draft?.lines.length ?? 0) > 0 && quote.breakdown !== null && !quote.updating,
);

async function place(): Promise<void> {
  const order = await checkout.submit();
  if (!order) return;
  // Clear the draft first (the order is the server's now), then leave the building route.
  draftStore.cancelOrder();
  quote.reset();
  checkout.reset();
  await router.push({ name: 'order-status', params: { id: order.id } });
}
</script>

<style scoped>
.place-button {
  min-height: 72px;
}
</style>
