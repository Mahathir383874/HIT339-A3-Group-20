# Moon – API Endpoint Notes

**Name:** Mishkatuz Zannat Moon · **Student ID:** 385411 · **Unit:** HIT339 Group Project

The public API is **read-only** and needs **no login**. Base path: `/api/v1`. All responses are JSON.
Run the app (`dotnet run`) and replace `https://localhost:PORT` with the address shown in the console.

Documentation page: `/api-docs.html` (loads the generated OpenAPI document from `/openapi/v1.json`).

I tested every endpoint and every error case in a browser (tests TC-API-01 to TC-API-14 in `04_Test_Plan_and_Evidence_Log.md`), and they all passed.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/v1/items` | List items (paged) with optional filters |
| GET | `/api/v1/items/{id}` | One item by numeric id |
| GET | `/api/v1/items/by-code/{code}` | One item by library code, e.g. `BOK-1001` |
| GET | `/api/v1/categories` | Categories with item counts |
| GET | `/api/v1/status` | Service operating status and stock summary |

### `GET /api/v1/items`

| Query parameter | Meaning | Default / limits |
|---|---|---|
| `category` | `Book`, `Music` or `Toy` (not case sensitive) | all |
| `status` | `Available`, `Borrowed`, `Damaged` or `Destroyed` | all |
| `search` | Keyword matched against title, code, description, type, author, genre, artist, toy type and manufacturer (same search as the public catalogue and kiosk) | max 100 characters |
| `page` | Page number | 1 (1 to 1,000,000) |
| `pageSize` | Items per page | 20 (1 to 100) |

Example response (shortened; the values are examples and yours will differ):

```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 13,
  "totalPages": 1,
  "items": [
    {
      "id": 1,
      "libraryCode": "BOK-1001",
      "name": "Clean Code: A Handbook of Agile Software Craftsmanship",
      "description": "A handbook of agile software craftsmanship and best programming practices.",
      "category": "Book",
      "status": "Available",
      "isAvailable": true,
      "imageUrl": "https://...",
      "author": "Robert C. Martin",
      "genre": "Technology",
      "isbn": "978-0132350884",
      "pageCount": 464
    }
  ]
}
```

Type-specific fields appear only for that type: books have `author`, `genre`, `isbn`, `pageCount`; music has `artist`, `releaseYear`, `recordLabel`, `durationMinutes`; toys have `toyType`, `recommendedAge`, `manufacturer`, `safetyWarning`.

### `GET /api/v1/categories`

```json
[
  { "name": "Book",  "totalItems": 5, "availableItems": 4 },
  { "name": "Music", "totalItems": 4, "availableItems": 3 },
  { "name": "Toy",   "totalItems": 4, "availableItems": 3 }
]
```

### `GET /api/v1/status`

```json
{
  "service": "Bibliotheca Library API",
  "apiVersion": "v1",
  "status": "Operational",
  "serverTimeUtc": "2026-10-04T02:30:00Z",
  "totalItems": 13,
  "activeLoans": 3,
  "itemsByStatus": { "Available": 9, "Borrowed": 3, "Damaged": 1, "Destroyed": 0 }
}
```

If the database cannot be reached the endpoint returns HTTP **503** with `"status": "Unavailable"`.

## Status codes

| Code | When |
|---|---|
| 200 OK | Valid request (an empty list is still 200) |
| 400 Bad Request | Invalid `category`, `status`, `page`, `pageSize`, `search`, id (`0`, negative or not a number) or library code format |
| 404 Not Found | No item with that id or code |
| 405 Method Not Allowed | Any verb other than GET (POST, PUT, PATCH, DELETE) |
| 503 Service Unavailable | `/status` only, when the database is unreachable |

Errors use the standard problem-details JSON format:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Invalid category",
  "status": 400,
  "detail": "'Magazine' is not a valid category. Allowed values: Book, Music, Toy."
}
```

## Try it (PowerShell – use `curl.exe`, not the `curl` alias)

```powershell
curl.exe -k https://localhost:PORT/api/v1/items
curl.exe -k "https://localhost:PORT/api/v1/items?category=Book&status=Available&pageSize=5"
curl.exe -k "https://localhost:PORT/api/v1/items?search=hobbit"
curl.exe -k https://localhost:PORT/api/v1/items/1
curl.exe -k https://localhost:PORT/api/v1/items/by-code/BOK-1001
curl.exe -k https://localhost:PORT/api/v1/categories
curl.exe -k https://localhost:PORT/api/v1/status

# invalid requests (expect 400 / 404 / 405)
curl.exe -k -i https://localhost:PORT/api/v1/items/0
curl.exe -k -i https://localhost:PORT/api/v1/items/abc
curl.exe -k -i https://localhost:PORT/api/v1/items/99999
curl.exe -k -i "https://localhost:PORT/api/v1/items?category=Magazine"
curl.exe -k -i -X DELETE https://localhost:PORT/api/v1/items/1
```

`-k` accepts the local development certificate. `-i` shows the status line so it can be screenshotted as evidence.

## Security notes

- Only GET actions exist. A CORS policy (`PublicReadOnlyApi`) allows other websites to call the API with GET/HEAD only.
- The API returns purpose-built DTOs, never database entities, so replacement cost, loans, borrowers and fines can never leak.
- Inputs are validated (page limits, page size cap of 100, search length cap, library code pattern), and queries use Entity Framework, so there is no SQL built from user text.
- There is no API key. The data is already public through the catalogue search portal. A key could be added as a small filter on `CatalogApiController`, but I didn't add one.

## Design notes

- "Operating status" is implemented as the operating status of the service (reachable and working) plus stock counts by item status. Each item also carries its own `status`.
- Paging is done in the database (`Skip`/`Take`), so the API stays fast as the catalogue grows.
- Items are ordered by name, then id, so pages are stable.

