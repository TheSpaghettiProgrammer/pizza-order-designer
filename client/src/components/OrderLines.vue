<template>
  <q-list separator>
    <q-item v-for="line in lines" :key="line.id" class="order-line" data-testid="order-line">
      <q-item-section>
        <q-item-label>{{ menuStore.sizeName(line.sizeCode) }} pizza</q-item-label>
        <q-item-label caption>
          {{
            line.toppingCodes.length
              ? line.toppingCodes.map((c) => menuStore.toppingName(c)).join(', ')
              : 'Plain cheese'
          }}
        </q-item-label>
      </q-item-section>
      <q-item-section side>
        <div class="row items-center no-wrap q-gutter-sm">
          <QuantityStepper
            :model-value="line.quantity"
            :label="`${menuStore.sizeName(line.sizeCode)} pizzas`"
            @update:model-value="(q) => draftStore.setQuantity(line.id, q)"
          />
          <q-btn flat round icon="edit" aria-label="Edit pizza" @click="emit('edit', line.id)" />
          <q-btn
            flat
            round
            icon="delete"
            color="negative"
            aria-label="Remove pizza"
            @click="draftStore.removeLine(line.id)"
          />
        </div>
      </q-item-section>
    </q-item>
  </q-list>
</template>

<script setup lang="ts">
import QuantityStepper from '@/components/QuantityStepper.vue';
import type { DraftLine } from '@/models/draft-order';
import { useDraftOrderStore } from '@/stores/draft-order-store';
import { useMenuStore } from '@/stores/menu-store';

defineProps<{ lines: DraftLine[] }>();
const emit = defineEmits<{ edit: [id: string] }>();

const draftStore = useDraftOrderStore();
const menuStore = useMenuStore();
</script>
