// Shapes returned by the order service (docs/API.md). Money fields are numbers the server computed;
// the UI only displays them (D-039).

export interface MenuSize {
  code: string;
  name: string;
  basePrice: number;
}

export interface MenuTopping {
  code: string;
  name: string;
  pricesBySize: Record<string, number>;
}

export interface MenuToppingCategory {
  code: string;
  name: string;
  rate: number;
  toppings: MenuTopping[];
}

export interface Menu {
  sizes: MenuSize[];
  toppingCategories: MenuToppingCategory[];
  taxRate: number;
  tipBase: string;
}

export interface PricedTopping {
  code: string;
  name: string;
  categoryCode: string;
  price: number;
}

export interface PricedLine {
  sizeCode: string;
  sizeName: string;
  quantity: number;
  basePrice: number;
  toppings: PricedTopping[];
  unitPrice: number;
  lineTotal: number;
  description: string;
}

export interface PriceBreakdown {
  lines: PricedLine[];
  subtotal: number;
  promoCode: string | null;
  discount: number;
  taxRate: number;
  tax: number;
  tipBase: string;
  tip: number;
  total: number;
  description: string;
}

export type OrderStatus = 'NotStarted' | 'Started' | 'ReadyForPickup' | 'Delivered';

export interface Order {
  id: string;
  status: OrderStatus;
  createdAt: string;
  updatedAt: string;
  version: string;
  pricing: PriceBreakdown;
}

/** The request body for quote, submit and update (docs/API.md "OrderDraft"). */
export interface OrderDraftBody {
  lines: { sizeCode: string; toppingCodes: string[]; quantity: number }[];
  promoCode: string | null;
  tip: { mode: 'amount' | 'percent'; value: string } | null;
}

export interface SubmitOrderBody extends OrderDraftBody {
  expectedTotal: number;
}
