<template>
  <q-layout view="hHh lpR fFf">
    <q-header elevated>
      <q-toolbar>
        <q-toolbar-title>Pizza Order Designer</q-toolbar-title>
        <CancelOrderButton v-if="route.meta.building" />
      </q-toolbar>
    </q-header>

    <q-page-container>
      <router-view />
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router';
import CancelOrderButton from '@/components/CancelOrderButton.vue';

const route = useRoute();
</script>
