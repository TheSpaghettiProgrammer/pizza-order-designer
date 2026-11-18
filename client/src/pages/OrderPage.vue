<template>
  <q-page class="q-pa-md">
    <h1 class="text-h4 q-mt-none">Your order</h1>
    <p v-if="store.itemCount === 0">No pizzas in your order yet.</p>
    <p v-else>
      {{ store.itemCount }} {{ store.itemCount === 1 ? 'pizza' : 'pizzas' }} in your order
    </p>
  </q-page>
</template>

<script setup lang="ts">
import { useDraftOrderStore } from '@/stores/draft-order-store';

const store = useDraftOrderStore();
</script>
