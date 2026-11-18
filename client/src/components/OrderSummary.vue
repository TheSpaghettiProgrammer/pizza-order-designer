<template>
  <q-card flat bordered data-testid="order-summary">
    <q-card-section>
      <div class="row items-center justify-between">
        <div class="text-h6">Price</div>
        <div v-if="quote.updating" class="text-grey-8" role="status" data-testid="updating">
          <q-spinner size="1em" class="q-mr-xs" />
          Updating…
        </div>
      </div>

      <p v-if="!hasLines" class="q-mt-sm q-mb-none">Add a pizza to see the price.</p>

      <p v-else-if="quote.error && !quote.updating" class="q-mt-sm q-mb-none text-negative">
        {{ priceProblem }}
      </p>

      <template v-else-if="quote.breakdown">
        <q-list dense class="q-mt-sm">
          <q-item v-for="(row, i) in rows" :key="i" class="summary-row">
            <q-item-section>{{ row.label }}</q-item-section>
            <q-item-section side :class="row.class" :data-testid="row.testid">
              {{ row.value }}
            </q-item-section>
          </q-item>
          <q-separator class="q-my-sm" />
          <q-item class="summary-row text-h6">
            <q-item-section>Total</q-item-section>
            <q-item-section side class="text-black" data-testid="total">
              {{ formatMoney(quote.breakdown.total) }}
            </q-item-section>
          </q-item>
        </q-list>

        <q-btn
          flat
          no-caps
          color="primary"
          icon="description"
          label="Describe my order"
          class="q-mt-sm"
          @click="showDescription = !showDescription"
        />
        <div v-if="showDescription" class="description q-mt-sm" data-testid="description">
          {{ quote.breakdown.description }}
        </div>
      </template>
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { friendlyMessage } from '@/api/api-error';
import { useDraftOrderStore } from '@/stores/draft-order-store';
import { useQuoteStore } from '@/stores/quote-store';
import { formatMoney } from '@/utils/format';

const quote = useQuoteStore();
const draftStore = useDraftOrderStore();
const showDescription = ref(false);

const hasLines = computed(() => (draftStore.draft?.lines.length ?? 0) > 0);

// Field problems (promo, tip) are shown next to their inputs; this is the general fallback.
const priceProblem = computed(() => {
  const { message } = friendlyMessage(quote.error);
  return `We can't show the price right now. ${message}`;
});

interface Row {
  label: string;
  value: string;
  testid: string;
  class?: string;
}

const rows = computed<Row[]>(() => {
  const b = quote.breakdown;
  if (!b) return [];
  const result: Row[] = [{ label: 'Subtotal', value: formatMoney(b.subtotal), testid: 'subtotal' }];
  if (b.promoCode) {
    result.push({
      label: `Discount (${b.promoCode})`,
      value: `-${formatMoney(b.discount)}`,
      testid: 'discount',
      class: 'text-positive',
    });
  }
  result.push({ label: 'Tax', value: formatMoney(b.tax), testid: 'tax' });
  result.push({ label: 'Tip', value: formatMoney(b.tip), testid: 'tip' });
  return result;
});
</script>

<style scoped>
.description {
  white-space: pre-line;
}
</style>
