<template>
  <q-page class="q-pa-md">
    <h1 class="text-h4 q-mt-none">Your order</h1>

    <q-banner v-if="menuStore.error" class="bg-negative text-white q-mb-md" rounded>
      {{ describeError(menuStore.error) }}
      <template #action>
        <q-btn flat no-caps label="Try again" @click="reloadMenu" />
      </template>
    </q-banner>

    <div class="row q-col-gutter-md">
      <div class="col-12 col-md-7">
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
          <OrderLines v-if="lines.length" :lines="lines" @edit="(id) => (editingId = id)" />
        </q-card>
        <OrderSummary class="q-mt-md" />
      </div>
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { describeError } from '@/api/api-error';
import OrderLines from '@/components/OrderLines.vue';
import OrderSummary from '@/components/OrderSummary.vue';
import PizzaCustomizer from '@/components/PizzaCustomizer.vue';
import { useDraftOrderStore } from '@/stores/draft-order-store';
import { useMenuStore } from '@/stores/menu-store';

const store = useDraftOrderStore();
const menuStore = useMenuStore();

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
