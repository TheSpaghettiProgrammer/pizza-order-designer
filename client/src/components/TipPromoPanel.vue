<template>
  <q-card flat bordered>
    <q-card-section>
      <div class="text-h6 q-mb-sm">Tip</div>
      <div class="row items-center q-gutter-sm">
        <q-btn-toggle
          :model-value="mode"
          :options="[
            { label: '%', value: 'percent' },
            { label: '$', value: 'amount' },
          ]"
          unelevated
          toggle-color="primary"
          no-caps
          size="lg"
          aria-label="Tip type"
          @update:model-value="onMode"
        />
        <template v-if="mode === 'percent'">
          <q-btn
            v-for="preset in PRESETS"
            :key="preset"
            :unelevated="isPreset(preset)"
            :outline="!isPreset(preset)"
            color="primary"
            no-caps
            size="lg"
            :label="`${preset}%`"
            :aria-pressed="isPreset(preset)"
            @click="draftStore.setTip({ mode: 'percent', value: preset })"
          />
        </template>
        <q-btn
          :unelevated="draftStore.draft?.tip == null"
          :outline="draftStore.draft?.tip != null"
          color="primary"
          no-caps
          size="lg"
          label="No tip"
          :aria-pressed="draftStore.draft?.tip == null"
          @click="draftStore.setTip(null)"
        />
      </div>
      <q-input
        class="q-mt-md"
        outlined
        inputmode="decimal"
        :label="mode === 'percent' ? 'Custom tip (%)' : 'Custom tip ($)'"
        :model-value="draftStore.draft?.tip?.value ?? ''"
        :error="tipErrors.length > 0"
        :error-message="tipErrors.join(' ')"
        data-testid="tip-input"
        @update:model-value="(v) => onCustomTip(String(v ?? ''))"
      />
    </q-card-section>

    <q-separator />

    <q-card-section>
      <div class="text-h6 q-mb-sm">Promo code</div>
      <div class="row q-gutter-sm no-wrap items-start">
        <q-input
          v-model="promoText"
          class="col"
          outlined
          label="Promo code"
          :error="promoErrors.length > 0"
          :error-message="promoErrors.join(' ')"
          data-testid="promo-input"
          @keyup.enter="applyPromo"
        />
        <q-btn
          color="primary"
          unelevated
          no-caps
          size="lg"
          label="Apply"
          :disable="promoText.trim() === ''"
          @click="applyPromo"
        />
        <q-btn
          v-if="draftStore.draft?.promoCode"
          flat
          no-caps
          size="lg"
          label="Remove"
          @click="removePromo"
        />
      </div>
      <p v-if="quote.breakdown?.promoCode" class="text-positive q-mt-sm q-mb-none" role="status">
        Code {{ quote.breakdown.promoCode }} applied.
      </p>
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { fieldMessages } from '@/api/api-error';
import type { TipMode } from '@/models/draft-order';
import { useDraftOrderStore } from '@/stores/draft-order-store';
import { useQuoteStore } from '@/stores/quote-store';

const PRESETS = ['10', '15', '20'];

const draftStore = useDraftOrderStore();
const quote = useQuoteStore();

const chosenMode = ref<TipMode>('percent');
const mode = computed<TipMode>(() => draftStore.draft?.tip?.mode ?? chosenMode.value);
const promoText = ref(draftStore.draft?.promoCode ?? '');

const tipErrors = computed(() => fieldMessages(quote.error, 'tip'));
const promoErrors = computed(() => fieldMessages(quote.error, 'promoCode'));

function isPreset(preset: string): boolean {
  const tip = draftStore.draft?.tip;
  return tip?.mode === 'percent' && tip.value === preset;
}

function onMode(next: TipMode): void {
  chosenMode.value = next;
  const tip = draftStore.draft?.tip;
  if (tip) draftStore.setTip({ mode: next, value: tip.value });
}

// Exactly what was typed goes to the server as a string; it parses and validates it (D-039).
function onCustomTip(text: string): void {
  draftStore.setTip(text.trim() === '' ? null : { mode: mode.value, value: text });
}

function applyPromo(): void {
  draftStore.setPromoCode(promoText.value);
}

function removePromo(): void {
  promoText.value = '';
  draftStore.setPromoCode(null);
}
</script>
