export interface Option { value: any; label: string; }

export type FieldType =
  | 'text' | 'email' | 'textarea' | 'number' | 'money'
  | 'select' | 'image' | 'color' | 'date' | 'datetime' | 'password';

export interface Field {
  key: string;
  label: string;
  type: FieldType;
  required?: boolean;
  readonly?: boolean;      // shown in the list, not editable in the form
  hideInList?: boolean;
  options?: Option[];      // fixed choices for a select
  lookup?: string;         // endpoint that supplies the choices for a select
  lookupLabel?: string;    // property used as the label, or several separated by commas
}

export interface RowAction {
  label: string;
  path: string;
  confirm?: string;
  show?: (row: any) => boolean;
  body?: (row: any) => any;
  href?: (row: any) => string | null;
}

export interface CrudConfig {
  title: string;       // "Products"
  singular: string;    // "product"
  endpoint: string;    // "products"  ->  /api/products
  fields: Field[];
  quickChecks?: { label: string; test: (row: any) => boolean }[];   // tick-box filters
  rowActions?: RowAction[];                                           // extra buttons on each row
}

export const ORDER_STATUSES = ['Pending', 'Processing', 'Shipped', 'Delivered', 'Cancelled'];
export const PAYMENT_STATUSES = ['Unpaid', 'Payment submitted', 'Deposit paid', 'Paid in full'];