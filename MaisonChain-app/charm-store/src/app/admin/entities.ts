import { CrudConfig } from '../core/models';

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

const depositConfirmed = (row: any) => row.paymentStatus === 'Deposit paid' || row.paymentStatus === 'Paid in full';

export const COMMANDS: CrudConfig = {
  title: 'Orders', singular: 'order', endpoint: 'commands',
  fields: [
    { key: 'imageUrl', label: 'Image', type: 'image' },
    { key: 'referenceNumber', label: 'Reference', type: 'text', readonly: true },
    { key: 'createdAt', label: 'Created', type: 'datetime', readonly: true },
    { key: 'customerId', label: 'Customer', type: 'select', lookup: 'customers', lookupLabel: 'fullName', required: true },
    { key: 'productId', label: 'Product', type: 'select', lookup: 'products', lookupLabel: 'model', required: true, hideInList: true },
    { key: 'model', label: 'Items', type: 'text', readonly: true },
    { key: 'genreId', label: 'Genre', type: 'select', lookup: 'genres', lookupLabel: 'name', required: true, hideInList: true },
    { key: 'colorId', label: 'Color', type: 'select', lookup: 'colors', lookupLabel: 'name', required: true, hideInList: true },
    { key: 'sizeId', label: 'Size', type: 'select', lookup: 'sizes', lookupLabel: 'name', hideInList: true },
    { key: 'deliveryAddress', label: 'Delivery address', type: 'textarea', required: true, hideInList: true },
    { key: 'date', label: 'Order date', type: 'date', required: true },
    { key: 'status', label: 'Status', type: 'text', readonly: true },
    { key: 'statusUpdatedAt', label: 'Status updated', type: 'datetime', readonly: true, hideInList: true },
    { key: 'statusNote', label: 'Last status update', type: 'textarea', readonly: true, hideInList: true },
    { key: 'trackingNumber', label: 'Tracking number', type: 'text', hideInList: true },
    { key: 'total', label: 'Total', type: 'money', required: true },
    { key: 'depositAmount', label: 'Deposit', type: 'money' },
    { key: 'paymentStatus', label: 'Payment', type: 'text', readonly: true },
    { key: 'paymentMethod', label: 'Pay method', type: 'text', readonly: true, hideInList: true },
    { key: 'paymentReference', label: 'Transaction ID', type: 'text' },
    { key: 'note', label: 'Internal note', type: 'textarea', hideInList: true },
    { key: 'updatedAt', label: 'Last updated', type: 'datetime', readonly: true, hideInList: true },
    { key: 'deliveredAt', label: 'Delivered at', type: 'datetime', readonly: true, hideInList: true }
  ],
  quickChecks: [
    { label: 'Payment to check', test: row => row.paymentStatus === 'Payment submitted' },
    { label: 'Deposit not paid', test: row => row.paymentStatus === 'Unpaid' || row.paymentStatus === 'Payment submitted' },
    { label: 'Open orders only', test: row => row.status !== 'Delivered' && row.status !== 'Cancelled' }
  ],
  rowActions: [
    {
      label: 'Confirm deposit', path: 'confirm-payment',
      confirm: 'Confirm that the deposit was received?',
      show: row => row.paymentStatus === 'Unpaid' || row.paymentStatus === 'Payment submitted'
    },
    {
      label: 'Mark paid in full', path: 'mark-paid-in-full',
      confirm: 'Confirm that the full amount was received?',
      show: row => row.paymentStatus === 'Deposit paid'
    },
    {
      label: 'Start processing', path: 'status',
      confirm: 'Move this order to Processing?',
      show: row => row.status === 'Pending' && depositConfirmed(row),
      body: () => ({ status: 'Processing' })
    },
    {
      label: 'Mark shipped', path: 'status',
      confirm: 'Move this order to Shipped? Add a tracking number in Edit first if available.',
      show: row => row.status === 'Processing' && depositConfirmed(row),
      body: row => ({ status: 'Shipped', trackingNumber: row.trackingNumber })
    },
    {
      label: 'Mark delivered', path: 'status',
      confirm: 'Confirm that this order was delivered?',
      show: row => row.status === 'Shipped' && row.paymentStatus === 'Paid in full',
      body: () => ({ status: 'Delivered' })
    },
    {
      label: 'Cancel order', path: 'status',
      confirm: 'Cancel this order? This cannot be undone. Refund any confirmed payment manually.',
      show: row => row.status === 'Pending' || row.status === 'Processing',
      body: () => ({ status: 'Cancelled' })
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
  quickChecks: [{ label: 'Never visited', test: row => !row.lastVisit }]
};
