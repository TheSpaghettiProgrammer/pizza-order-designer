import { defineStore } from 'pinia';
import { ref } from 'vue';
import { getMenu } from '@/api/orders-api';
import type { Menu } from '@/api/types';

/** The menu comes from the server (nothing hard-coded in the UI); loaded once per page load. */
export const useMenuStore = defineStore('menu', () => {
  const menu = ref<Menu | null>(null);
  const loading = ref(false);
  const error = ref<unknown>(null);

  async function load(): Promise<void> {
    if (menu.value || loading.value) return;
    loading.value = true;
    error.value = null;
    try {
      menu.value = await getMenu();
    } catch (e) {
      error.value = e;
    } finally {
      loading.value = false;
    }
  }

  function sizeName(code: string): string {
    return menu.value?.sizes.find((s) => s.code === code)?.name ?? code;
  }

  function toppingName(code: string): string {
    for (const category of menu.value?.toppingCategories ?? []) {
      const topping = category.toppings.find((t) => t.code === code);
      if (topping) return topping.name;
    }
    return code;
  }

  return { menu, loading, error, load, sizeName, toppingName };
});
