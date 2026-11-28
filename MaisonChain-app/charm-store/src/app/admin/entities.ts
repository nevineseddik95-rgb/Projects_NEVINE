import { CrudConfig, ORDER_STATUSES, PAYMENT_STATUSES } from '../core/models';
import { initialLanguage, translate } from '../core/translations';

export const PRODUCTS: CrudConfig = {
  title: 'admin.products', singular: 'admin.product', endpoint: 'products',
  fields: [
    { key: 'imageUrl', label: 'common.image', type: 'image' },
    { key: 'model', label: 'common.model', type: 'text', required: true },
    { key: 'genreId', label: 'common.genre', type: 'select', lookup: 'genres', lookupLabel: 'name', required: true },
    { key: 'colorId', label: 'common.color', type: 'select', lookup: 'colors', lookupLabel: 'name', required: true },
    { key: 'sizeId', label: 'common.size', type: 'select', lookup: 'sizes', lookupLabel: 'name', required: true },
    { key: 'price', label: 'common.price', type: 'money', required: true },
    { key: 'releaseDate', label: 'shop.released', type: 'date', required: true }
  ]
};

export const GENRES: CrudConfig = {
  title: 'admin.genres', singular: 'admin.genre', endpoint: 'genres',
  fields: [{ key: 'name', label: 'common.name', type: 'text', required: true }]
};

export const COLORS: CrudConfig = {
  title: 'admin.colors', singular: 'admin.color', endpoint: 'colors',
  fields: [{ key: 'name', label: 'common.name', type: 'text', required: true }]
};

export const SIZES: CrudConfig = {
  title: 'admin.sizes', singular: 'admin.size', endpoint: 'sizes',
  fields: [{ key: 'name', label: 'common.name', type: 'text', required: true }]
};

const depositConfirmed = (row: any) => row.paymentStatus === 'Deposit paid' || row.paymentStatus === 'Paid in full';

const orderSmsHref = (row: any): string | null => {
  const phone = String(row.customerPhone ?? '').replace(/[^\d+]/g, '');
  if (!phone) return null;

  const language = initialLanguage();
  const t = (key: string, params: Record<string, string | number> = {}) => translate(language, key, params);
  const money = (value: any) => {
    const locale = language === 'fr' ? 'fr-FR' : 'en-US';
    return `${Math.round(Number(value) || 0).toLocaleString(locale).replace(/[\u202f\u00a0]/g, ' ')} KMF`;
  };
  const balance = row.paymentStatus === 'Paid in full'
    ? 0
    : Math.max(0, Number(row.total) - Number(row.depositAmount));
  const lines = [
    'Beauty & Shine',
    row.customerName ? t('admin.smsHello', { name: row.customerName }) : '',
    t('admin.smsOrder', { reference: row.referenceNumber }),
    t('admin.smsStatus', {
      status: t(`status.${row.status}`),
      payment: t(`payment.${row.paymentStatus}`)
    }),
    t('admin.smsItems', { items: row.model }),
    t('admin.smsAmounts', {
      total: money(row.total),
      deposit: money(row.depositAmount),
      balance: money(balance)
    })
  ].filter(Boolean);

  if (row.trackingNumber) lines.push(t('admin.smsTracking', { tracking: row.trackingNumber }));
  if (row.statusNote) lines.push(t('admin.smsUpdate', { note: row.statusNote }));
  lines.push(t('admin.smsKeepReference'));

  return `sms:${phone}?body=${encodeURIComponent(lines.join('\n'))}`;
};

export const COMMANDS: CrudConfig = {
  title: 'admin.orders', singular: 'admin.order', endpoint: 'commands',
  editLabel: 'common.customize', canCreate: true,
  fields: [
    { key: 'imageUrl', label: 'common.image', type: 'image' },
    { key: 'referenceNumber', label: 'common.reference', type: 'text', readonly: true },
    { key: 'createdAt', label: 'common.created', type: 'datetime', readonly: true },
    { key: 'customerId', label: 'common.customer', type: 'select', lookup: 'customers', lookupLabel: 'fullName', displayKey: 'customerName', required: true },
    { key: 'customerEmail', label: 'admin.customerEmail', type: 'email', readonly: true, hideInList: true },
    { key: 'customerPhone', label: 'admin.customerPhone', type: 'text', readonly: true, hideInList: true },
    { key: 'productId', label: 'common.product', type: 'select', lookup: 'products', lookupLabel: 'model', required: true, hideInList: true },
    { key: 'model', label: 'common.items', type: 'text', readonly: true },
    { key: 'genreId', label: 'common.genre', type: 'select', lookup: 'genres', lookupLabel: 'name', required: true, hideInList: true },
    { key: 'colorId', label: 'common.color', type: 'select', lookup: 'colors', lookupLabel: 'name', required: true, hideInList: true },
    { key: 'sizeId', label: 'common.size', type: 'select', lookup: 'sizes', lookupLabel: 'name', hideInList: true },
    { key: 'deliveryAddress', label: 'checkout.deliveryAddress', type: 'textarea', required: true, hideInList: true },
    { key: 'date', label: 'admin.orderDate', type: 'date', required: true },
    { key: 'status', label: 'common.status', type: 'select', readonly: true, options: ORDER_STATUSES.map(value => ({ value, label: value })) },
    { key: 'statusUpdatedAt', label: 'common.statusUpdated', type: 'datetime', readonly: true, hideInList: true },
    { key: 'statusNote', label: 'common.lastStatusUpdate', type: 'textarea', readonly: true, hideInList: true },
    { key: 'trackingNumber', label: 'common.trackingNumber', type: 'text', readonly: true, hideInList: true },
    { key: 'total', label: 'common.total', type: 'money', required: true },
    {
      key: 'commissionAmount', label: 'commissions.orderCommission', type: 'money', required: true,
      defaultValue: 500, hideInList: true, ownerOnly: true,
      showInForm: (row, isNew) => !isNew && row.createdByAdminRole === 'Viewer'
    },
    { key: 'depositAmount', label: 'common.deposit', type: 'money' },
    { key: 'paymentStatus', label: 'common.payment', type: 'select', readonly: true, options: PAYMENT_STATUSES.map(value => ({ value, label: value })) },
    { key: 'paymentMethod', label: 'common.payMethod', type: 'text', readonly: true, hideInList: true },
    { key: 'paymentReference', label: 'common.transactionId', type: 'text' },
    { key: 'note', label: 'common.internalNote', type: 'textarea', hideInList: true },
    { key: 'updatedAt', label: 'common.updated', type: 'datetime', readonly: true, hideInList: true },
    { key: 'deliveredAt', label: 'common.deliveredAt', type: 'datetime', readonly: true, hideInList: true }
  ],
  quickChecks: [
    { label: 'admin.paymentToCheck', test: row => row.paymentStatus === 'Payment submitted' },
    { label: 'admin.depositNotPaid', test: row => row.paymentStatus === 'Unpaid' || row.paymentStatus === 'Payment submitted' },
    { label: 'admin.openOrders', test: row => row.status !== 'Delivered' && row.status !== 'Cancelled' }
  ],
  rowActions: [
    {
      label: 'admin.textCustomer', path: 'sms',
      show: row => !!row.customerPhone,
      href: orderSmsHref
    },
    {
      label: 'admin.confirmDeposit', path: 'confirm-payment',
      confirm: 'admin.confirmDepositPrompt',
      show: row => row.paymentStatus === 'Unpaid' || row.paymentStatus === 'Payment submitted'
    },
    {
      label: 'admin.markPaidFull', path: 'mark-paid-in-full',
      confirm: 'admin.markPaidFullPrompt',
      show: row => row.paymentStatus === 'Deposit paid'
    },
    {
      label: 'admin.startProcessing', path: 'status',
      confirm: 'admin.processingPrompt',
      show: row => row.status === 'Pending' && depositConfirmed(row),
      body: () => ({ status: 'Processing' })
    },
    {
      label: 'admin.markShipped', path: 'status',
      confirm: 'admin.shippedPrompt',
      show: row => row.status === 'Processing' && depositConfirmed(row),
      body: () => ({ status: 'Shipped' })
    },
    {
      label: 'admin.markDelivered', path: 'status',
      confirm: 'admin.deliveredPrompt',
      show: row => row.status === 'Shipped' && row.paymentStatus === 'Paid in full',
      body: () => ({ status: 'Delivered' })
    },
    {
      label: 'admin.cancelOrder', path: 'status',
      confirm: 'admin.cancelPrompt',
      show: row => row.status === 'Pending' || row.status === 'Processing',
      body: () => ({ status: 'Cancelled' })
    }
  ]
};

export const ADMINS: CrudConfig = {
  title: 'admin.admins', singular: 'admin.admin', endpoint: 'admins',
  canEditRow: row => row.role !== 'Owner',
  canDeleteRow: row => row.role !== 'Owner',
  fields: [
    { key: 'username', label: 'admin.username', type: 'text', required: true },
    { key: 'phone', label: 'common.phone', type: 'text' },
    { key: 'password', label: 'admin.password', type: 'password', required: true, hideInList: true, showInDetail: false, showInForm: true },
    { key: 'role', label: 'admin.role', type: 'text', readonly: true, defaultValue: 'Viewer' },
    { key: 'createdAt', label: 'common.created', type: 'datetime', readonly: true }
  ]
};

export const CUSTOMERS: CrudConfig = {
  title: 'admin.customers', singular: 'admin.customer', endpoint: 'customers',
  fields: [
    { key: 'fullName', label: 'common.name', type: 'text', required: true },
    { key: 'email', label: 'common.email', type: 'email' },
    { key: 'phone', label: 'common.phone', type: 'text' },
    { key: 'address', label: 'common.address', type: 'textarea' },
    { key: 'lastVisit', label: 'common.updated', type: 'datetime', readonly: true }
  ],
  quickChecks: [{ label: 'admin.neverVisited', test: row => !row.lastVisit }]
};
