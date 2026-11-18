<template>
  <q-btn
    flat
    size="lg"
    no-caps
    icon="close"
    label="Cancel order"
    class="cancel-button"
    @click="confirmCancel"
  />
</template>

<script setup lang="ts">
import { useQuasar } from 'quasar';
import { useEndOrder } from '@/composables/use-end-order';

const $q = useQuasar();
const { endOrder } = useEndOrder();

// Always asks, even for an empty order. Focus starts on "Keep my order", so a stray Enter is safe.
function confirmCancel(): void {
  $q.dialog({
    title: 'Cancel this order?',
    message: 'Everything in your order will be removed.',
    cancel: { label: 'Keep my order', flat: true, noCaps: true },
    ok: { label: 'Yes, cancel order', color: 'negative', noCaps: true },
    focus: 'cancel',
  }).onOk(() => {
    void endOrder();
  });
}
</script>

<style scoped>
.cancel-button {
  min-height: 48px;
}
</style>
