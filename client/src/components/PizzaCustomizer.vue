<template>
  <q-card flat bordered class="customizer">
    <q-card-section>
      <div class="text-h6">{{ editing ? 'Change this pizza' : 'Build a pizza' }}</div>
    </q-card-section>

    <q-card-section>
      <div class="text-subtitle1 q-mb-sm">1. Pick a size</div>
      <div class="row q-gutter-sm" role="radiogroup" aria-label="Size">
        <q-btn
          v-for="size in menu.sizes"
          :key="size.code"
          :unelevated="sizeCode === size.code"
          :outline="sizeCode !== size.code"
          color="primary"
          no-caps
          size="lg"
          class="size-button"
          role="radio"
          :aria-checked="sizeCode === size.code"
          :data-testid="`size-${size.code}`"
          @click="sizeCode = size.code"
        >
          <div class="column items-center">
            <span>{{ size.name }}</span>
            <span class="text-caption">{{ formatMoney(size.basePrice) }}</span>
          </div>
        </q-btn>
      </div>
    </q-card-section>

    <q-card-section>
      <div class="text-subtitle1 q-mb-sm">2. Choose toppings</div>
      <p v-if="!sizeCode" class="text-grey-8">Pick a size to see topping prices.</p>
      <div v-for="category in menu.toppingCategories" :key="category.code" class="q-mb-md">
        <div class="text-subtitle2">{{ category.name }}</div>
        <div class="row">
          <q-checkbox
            v-for="topping in category.toppings"
            :key="topping.code"
            :model-value="toppings.includes(topping.code)"
            :label="toppingLabel(topping.name, topping.pricesBySize)"
            size="lg"
            class="col-12 col-sm-6"
            :data-testid="`topping-${topping.code}`"
            @update:model-value="toggleTopping(topping.code)"
          />
        </div>
      </div>
    </q-card-section>

    <q-card-section>
      <div class="text-subtitle1 q-mb-sm">3. How many?</div>
      <QuantityStepper v-model="quantity" label="pizzas to add" />
    </q-card-section>

    <q-card-actions class="q-pa-md">
      <q-btn
        color="primary"
        unelevated
        no-caps
        size="lg"
        :disable="!sizeCode"
        :label="editing ? 'Save changes' : 'Add to order'"
        data-testid="add-to-order"
        @click="save"
      />
      <q-btn v-if="editing" flat no-caps size="lg" label="Cancel edit" @click="emit('cancel')" />
    </q-card-actions>
  </q-card>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import QuantityStepper from '@/components/QuantityStepper.vue';
import type { Menu } from '@/api/types';
import type { DraftLine } from '@/models/draft-order';
import { formatMoney } from '@/utils/format';

const props = defineProps<{ menu: Menu; editing: DraftLine | null }>();
const emit = defineEmits<{
  save: [value: { sizeCode: string; toppingCodes: string[]; quantity: number }];
  cancel: [];
}>();

const sizeCode = ref<string | null>(null);
const toppings = ref<string[]>([]);
const quantity = ref(1);

watch(
  () => props.editing,
  (line) => {
    sizeCode.value = line?.sizeCode ?? null;
    toppings.value = line ? [...line.toppingCodes] : [];
    quantity.value = line?.quantity ?? 1;
  },
  { immediate: true },
);

function toppingLabel(name: string, prices: Record<string, number>): string {
  const price = sizeCode.value ? prices[sizeCode.value] : undefined;
  return price === undefined ? name : `${name} (+${formatMoney(price)})`;
}

// A set, no duplicates (D-043).
function toggleTopping(code: string): void {
  toppings.value = toppings.value.includes(code)
    ? toppings.value.filter((c) => c !== code)
    : [...toppings.value, code];
}

function save(): void {
  if (!sizeCode.value) return;
  emit('save', {
    sizeCode: sizeCode.value,
    toppingCodes: toppings.value,
    quantity: quantity.value,
  });
  sizeCode.value = null;
  toppings.value = [];
  quantity.value = 1;
}
</script>

<style scoped>
.size-button {
  min-width: 120px;
  min-height: 72px;
}
</style>
