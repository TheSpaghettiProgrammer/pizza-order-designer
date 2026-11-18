import { installQuasarPlugin } from '@quasar/quasar-app-extension-testing-unit-vitest';
import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import IndexPage from '@/pages/IndexPage.vue';

installQuasarPlugin();

describe('IndexPage', () => {
  it('renders the app title', () => {
    // QPage must live inside a QLayout; stub it so the page can be mounted on its own.
    const wrapper = mount(IndexPage, {
      global: { stubs: { QPage: { template: '<div class="q-page"><slot /></div>' } } },
    });

    expect(wrapper.text()).toContain('Pizza Order Designer');
  });
});
