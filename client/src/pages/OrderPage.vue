<template>
  <q-page class="q-pa-md">
    <h1 class="text-h4 q-mt-none">Your order</h1>

    <q-banner v-if="menuStore.error" class="bg-negative text-white q-mb-md" rounded>
      {{ describeError(menuStore.error) }}
      <template #action>
        <q-btn flat no-caps label="Try again" @click="reloadMenu" />
      </template>
    </q-banner>

    <q-banner
      v-if="checkout.locked"
      class="bg-grey-3 text-black q-mb-md"
      rounded
      role="status"
      data-testid="order-locked"
    >
      Your order is locked while we confirm it with the kitchen.
    </q-banner>

    <div class="row q-col-gutter-md">
      <div class="col-12 col-md-7" :inert="checkout.locked || undefined">
        <PizzaCustomizer
          v-if="menuStore.menu"
          :menu="menuStore.menu"
          :editing="editingLine"
          @save="onSave"
          @cancel="editingId = null"
        />
        <p v-else-if="menuStore.loading">Loading the menu…</p>
      </div>

      <div class="col-12 col-md-5">
        <q-card flat bordered>
          <q-card-section>
            <div class="text-h6">Your pizzas</div>
            <p v-if="store.itemCount === 0" class="q-mb-none">No pizzas in your order yet.</p>
            <p v-else class="q-mb-none">
              {{ store.itemCount }} {{ store.itemCount === 1 ? 'pizza' : 'pizzas' }} in your order
            </p>
          </q-card-section>
          <OrderLines
            v-if="lines.length"
            :lines="lines"
            :inert="checkout.locked || undefined"
            @edit="(id) => (editingId = id)"
          />
        </q-card>
        <TipPromoPanel v-if="lines.length" class="q-mt-md" :inert="checkout.locked || undefined" />
        <OrderSummary class="q-mt-md" />
        <SubmitPanel v-if="lines.length" class="q-mt-md" />
      </div>
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { describeError } from '@/api/api-error';
import OrderLines from '@/components/OrderLines.vue';
import OrderSummary from '@/components/OrderSummary.vue';
import SubmitPanel from '@/components/SubmitPanel.vue';
import TipPromoPanel from '@/components/TipPromoPanel.vue';
import PizzaCustomizer from '@/components/PizzaCustomizer.vue';
import { useCheckoutStore } from '@/stores/checkout-store';
import { useDraftOrderStore } from '@/stores/draft-order-store';
import { useMenuStore } from '@/stores/menu-store';

const store = useDraftOrderStore();
const menuStore = useMenuStore();
// While a submit is in flight or its outcome is unknown, the order can't change (no second order).
const checkout = useCheckoutStore();

const editingId = ref<string | null>(null);
const lines = computed(() => store.draft?.lines ?? []);
const editingLine = computed(() => lines.value.find((l) => l.id === editingId.value) ?? null);

onMounted(() => void menuStore.load());

function reloadMenu(): void {
  void menuStore.load();
}

function onSave(value: { sizeCode: string; toppingCodes: string[]; quantity: number }): void {
  if (editingId.value) {
    store.updateLine(editingId.value, value);
    editingId.value = null;
  } else {
    store.addLine(value);
  }
}
</script>
