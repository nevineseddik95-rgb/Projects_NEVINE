# Charm Store (Angular 18)

Storefront and admin area for a shop selling Bracelets, necklaces ,keychains and MORE!.

## Run

```bash
npm install
npm start            # http://localhost:4200
```

Edit `proxy.conf.json` if your .NET API is not on `https://localhost:5001`.

* Storefront: `/`
* Admin: `/admin/login`  (dashboard, orders, products, customers, genres, colors, sizes)

## Filters

* **Storefront (`/`)**: search, sort (newest, name, price), price range, in-stock only, and genre, color and size chips.
* **Admin lists**: search box, sortable column headers, and automatic filters built from each screen's fields
  (dropdowns for genre/color/size/customer/status, date ranges for dates and last visit, min/max for price, stock and totals).
  Tick-box filters are defined per screen in `entities.ts` (`quickChecks`): low stock, out of stock, open orders, never visited.

## API contract

JSON uses camelCase and an `id` primary key. All endpoints below are under `/api`.

| Resource | Endpoint | Fields used |
|---|---|---|
| Products | `products` | id, name, description, price, stock, genreId, colorId, sizeId, **imageUrl** |
| Genres | `genres` | id, name |
| Colors | `colors` | id, name, hex |
| Sizes | `sizes` | id, label |
| Orders (Commands table) | `commands` | id, customerId, date, status, total, note, **imageUrl** |
| Customers | `customers` | id, fullName, email, phone, lastVisit |
| Login | `POST auth/login` | body `{username, password}` -> `{token}` |
| Image upload | `POST upload` | multipart field `file` -> `{url}` |

Each resource supports `GET`, `GET /{id}`, `POST`, `PUT /{id}`, `DELETE /{id}`.
Order status values: Pending, Processing, Shipped, Delivered, Cancelled.

If your property names differ, change the `key` values in `src/app/admin/entities.ts`.
Each admin screen is one config object there, so adding a field is a one-line change.

## Images

Choosing a file in a product or order form sends it to `POST /api/upload`; the returned
`url` is saved in `imageUrl`. See `backend-snippets/` for the controller and `Program.cs` lines.
For production set `apiUrl` and `filesUrl` in `src/environments/environment.ts`.
