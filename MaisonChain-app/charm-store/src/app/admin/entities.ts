import { CrudConfig, ORDER_STATUSES, PAYMENT_STATUSES } from '../core/models';

const statusOptions = ORDER_STATUSES.map(s => ({ value: s, label: s }));
const paymentOptions = PAYMENT_STATUSES.map(s => ({ value: s, label: s }));

export const PRODUCTS: CrudConfig = {
  title: 'Products', singular: 'product', endpoint: 'products',
  fields: [
    { key: 'imageUrl', label: 'Image', type: 'image' },
    { key: 'model', label: 'Model', type: 'text', required: true },
    { key: 'genreId', label: 'Genre', type: 'select', lookup: 'genres', lookupLabel: 'name', required: true },
    { key: 'colorId', label: 'Color', type: 'select', lookup: 'colors', lookupLabel: 'name', required: true },
    { key: 'sizeId', label: 'Size', type: 'select', lookup: 'sizes', lookupLabel: 'name', required: true },
    { key: 'price', label: 'Price (KMF)', type: 'money', required: true },
    { key: 'releaseDate', label: 'Release date', type: 'date', required: true }
  ]
};

export const GENRES: CrudConfig = {
  title: 'Genres', singular: 'genre', endpoint: 'genres',
  fields: [{ key: 'name', label: 'Name', type: 'text', required: true }]
};

export const COLORS: CrudConfig = {
  title: 'Colors', singular: 'color', endpoint: 'colors',
  fields: [{ key: 'name', label: 'Name', type: 'text', required: true }]
};

export const SIZES: CrudConfig = {
  title: 'Sizes', singular: 'size', endpoint: 'sizes',
  fields: [{ key: 'name', label: 'Name', type: 'text', required: true }]
};

// "Commands" = customer orders
// Status = where the order is. Payment = where the money is.
export const COMMANDS: CrudConfig = {
  title: 'Orders', singular: 'order', endpoint: 'commands',
  fields: [
    { key: 'imageUrl', label: 'Image', type: 'image' },
    { key: 'referenceNumber', label: 'Reference', type: 'text', readonly: true },
    { key: 'customerId', label: 'Customer', type: 'select', lookup: 'customers', lookupLabel: 'fullName', required: true },
    { key: 'productId', label: 'Product', type: 'select', lookup: 'products', lookupLabel: 'model', required: true, hideInList: true },
    { key: 'model', label: 'Items', type: 'text', readonly: true },
    { key: 'genreId', label: 'Genre', type: 'select', lookup: 'genres', lookupLabel: 'name', required: true, hideInList: true },
    { key: 'colorId', label: 'Color', type: 'select', lookup: 'colors', lookupLabel: 'name', required: true, hideInList: true },
    { key: 'sizeId', label: 'Size', type: 'select', lookup: 'sizes', lookupLabel: 'name', hideInList: true },
    { key: 'deliveryAddress', label: 'Delivery address', type: 'textarea', required: true, hideInList: true },
    { key: 'date', label: 'Date', type: 'date', required: true },
    { key: 'status', label: 'Status', type: 'select', options: statusOptions, required: true },
    { key: 'total', label: 'Total', type: 'money', required: true },
    { key: 'depositAmount', label: 'Deposit', type: 'money' },
    { key: 'paymentStatus', label: 'Payment', type: 'select', options: paymentOptions, required: true },
    { key: 'paymentMethod', label: 'Pay method', type: 'text', readonly: true, hideInList: true },
    { key: 'paymentReference', label: 'Transaction ID', type: 'text' },
    { key: 'note', label: 'Note', type: 'textarea', hideInList: true }
  ],
  quickChecks: [
    { label: 'Payment to check', test: r => r.paymentStatus === 'Payment submitted' },
    { label: 'Deposit not paid', test: r => r.paymentStatus === 'Unpaid' || r.paymentStatus === 'Payment submitted' },
    { label: 'Open orders only', test: r => r.status !== 'Delivered' && r.status !== 'Cancelled' }
  ],
  rowActions: [
    {
      label: 'Confirm deposit', path: 'confirm-payment',
      confirm: 'Confirm that the deposit was received?',
      show: r => r.paymentStatus === 'Unpaid' || r.paymentStatus === 'Payment submitted'
    },
    {
      label: 'Mark paid in full', path: 'mark-paid-in-full',
      confirm: 'Confirm that the full amount was received?',
      show: r => r.paymentStatus === 'Deposit paid'
    }
  ]
};
export const ADMINS: CrudConfig = {
  title: 'Admins', singular: 'admin', endpoint: 'admins',
  fields: [
    { key: 'username', label: 'Username', type: 'text', required: true },
    { key: 'password', label: 'Password (at least 8 characters; leave empty to keep it)', type: 'password', hideInList: true },
    { key: 'role', label: 'Role', type: 'text', readonly: true },
    { key: 'createdAt', label: 'Created', type: 'datetime', readonly: true }
  ]
};
export const CUSTOMERS: CrudConfig = {
  title: 'Customers', singular: 'customer', endpoint: 'customers',
  fields: [
    { key: 'fullName', label: 'Name', type: 'text', required: true },
    { key: 'email', label: 'Email (optional)', type: 'email' },
    { key: 'phone', label: 'Phone', type: 'text' },
    { key: 'address', label: 'Address', type: 'textarea' },
    { key: 'lastVisit', label: 'Last visit', type: 'datetime', readonly: true }
  ],
  quickChecks: [{ label: 'Never visited', test: r => !r.lastVisit }]
};