// With `ng serve` the proxy (proxy.conf.json) forwards /api and /uploads to the .NET API.
// For production, set apiUrl to the full API address, e.g. 'https://api.mystore.com/api',
// and filesUrl to the API origin, e.g. 'https://api.mystore.com'.
export const environment = {
  // Set to true to bring back the login requirement for /admin pages.
  requireLogin: true,
  apiUrl: '/api',
  filesUrl: '',hubUrl: '/hubs/admin'
};
