<template>
  <div class="row items-center q-gutter-sm no-wrap">
    <q-btn
      round
      outline
      color="primary"
      icon="remove"
      size="md"
      :disable="modelValue <= MIN_QUANTITY"
      :aria-label="`Fewer ${label}`"
      @click="emit('update:modelValue', modelValue - 1)"
    />
    <span class="text-h6 quantity-value" data-testid="quantity">{{ modelValue }}</span>
    <q-btn
      round
      outline
      color="primary"
      icon="add"
      size="md"
      :disable="modelValue >= MAX_QUANTITY"
      :aria-label="`More ${label}`"
      @click="emit('update:modelValue', modelValue + 1)"
    />
  </div>
</template>

<script setup lang="ts">
// Limits mirror the server's (D-043); the server stays the authority.
const MIN_QUANTITY = 1;
const MAX_QUANTITY = 20;

defineProps<{ modelValue: number; label: string }>();
const emit = defineEmits<{ 'update:modelValue': [value: number] }>();
</script>

<style scoped>
.quantity-value {
  min-width: 2ch;
  text-align: center;
}
</style>
