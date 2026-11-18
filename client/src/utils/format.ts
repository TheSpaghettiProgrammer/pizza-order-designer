const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });

/** Display only. The number came from the server; the UI never does money arithmetic (D-039). */
export function formatMoney(amount: number): string {
  return usd.format(amount);
}
