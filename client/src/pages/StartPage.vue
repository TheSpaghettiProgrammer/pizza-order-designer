<template>
  <q-page class="flex flex-center text-center q-pa-md">
    <div class="column items-center q-gutter-y-lg start-content">
      <h1 class="text-h3 q-my-none">Welcome!</h1>
      <p class="text-h6 q-my-none">Build your pizza and we will get it started.</p>
      <q-btn
        class="start-button"
        color="primary"
        size="xl"
        unelevated
        no-caps
        label="Start order"
        @click="onStart"
      />
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router';
import { useDraftOrderStore } from '@/stores/draft-order-store';

const store = useDraftOrderStore();
const router = useRouter();

function onStart(): void {
  store.startOrder();
  void router.push({ name: 'order' });
}
</script>

<style scoped>
.start-content {
  width: 100%;
}

.start-button {
  width: 100%;
  max-width: 480px;
  min-height: 96px;
}
</style>
