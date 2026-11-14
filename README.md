# 📦 Shelfie – Smart Inventory App

Shelfie is a smart home inventory app built with .NET MAUI and ASP.NET Core.  
It keeps track of food, medicines, books and any custom categories, warns before things expire and builds your shopping list.
**It works fully offline**: everything is stored on the phone and synchronised with the server whenever it is reachable.

![ShelfieCard](https://github.com/user-attachments/assets/774ceda1-9f56-4318-91a9-209e9e65cbdb)

---

## ✨ Features

- 📴 **Offline-first** — local SQLite database on the device; use it without an account (guest mode) and sign in later to upload everything
- 🔄 **Sync between devices** — changes are queued while offline and synchronised when the server is back (last write wins)
- ⏰ **Expiry tracking** — "expiring soon" and "expired" lists plus local notifications N days before
- 📷 **Barcode scanner** — finds your own items offline, otherwise fills name/brand/weight from Open Food Facts
- 🛒 **Shopping list** — quick −/+ on every item; when something runs out it goes to the list, "bought" restocks it
- 📍 **Storage locations & photos** — fridge, freezer, first-aid kit…; photos stay on the device
- 🧩 **Categories with typed fields** — Products, Medications, Books built in; custom categories with text / number / date / yes-no fields; rename or reorder fields without losing values
- 📊 **Dashboard & statistics** — used vs. thrown away over 30 days, history of all operations
- 📤 **CSV export** — from the app (works offline) or `GET /api/items/export`
- 🌐 Russian / English

---

## 🛠️ Tech Stack

- **Frontend**: .NET MAUI (Blazor)
- **Backend**: ASP.NET Core 
- **Database**: PostgreSQL, Entity Framework Core
- **Auth**: JWT-based authentication
- **Languages**: C#, JS, Razor, HTML/CSS

---

## 📷 Screenshots

### Custom Category View  
![Analysis](https://github.com/user-attachments/assets/ee190b47-fcae-4aa2-81d0-d3c338a30988)

<div align="center">
  
| Categories Page                  | Dashboard                        |
|----------------------------------|----------------------------------|
| <img src="https://github.com/user-attachments/assets/386c7078-9f42-4a7b-accb-57e240c4b037" width="300"/> | <img src="https://github.com/user-attachments/assets/cf613a7b-0adb-4e78-b288-d57fac75b530" width="300"/> |

| Authorization                    | Registration                     |
|----------------------------------|----------------------------------|
| <img src="https://github.com/user-attachments/assets/ec982a8b-797c-4c71-bea2-161ce1df3bc6" width="300"/> | <img src="https://github.com/user-attachments/assets/f1c41b9a-d9f9-4191-bf15-8bb33721620b" width="300"/> |

| Account edit Page                | Logout Page                      |
|----------------------------------|----------------------------------|
| <img src="https://github.com/user-attachments/assets/d2df9fe0-a95f-4919-84a4-80a09b26299e" width="300"/> | <img src="https://github.com/user-attachments/assets/6b57578e-e36f-4424-8b51-cfe4dc2f5f4d" width="300"/> |

</div>

### Swagger window 
![Screenshot 2025-04-05 230704](https://github.com/user-attachments/assets/df4693e6-bbfb-4cd4-bb79-9e8974b69039)

---
## 📦 Download

You can download the latest APK [here](https://github.com/qwiklly/Shelfie/releases/tag/v1.0.1-beta).

---
## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

## 🏗️ Project structure

| Project | What it is |
|---|---|
| `Shelfie.Shared` | Contracts and rules shared by server and app (DTOs, built-in categories, validation, CSV export) |
| `ShelfieBackend` | ASP.NET Core API + PostgreSQL (REST by id, `/api/sync`, admin pages) |
| `Shelfie.Client` | Offline core of the app: local SQLite inventory, sync engine, account handling, barcode lookup — plain .NET, fully unit-tested |
| `MauiApp1` | The .NET MAUI Blazor app (UI, camera, notifications) |
| `*.Tests` | xUnit v3 tests |

## 🐳 Running the server with Docker

```bash
cp .env.example .env   # then edit the password and JWT key
docker compose up -d
```

The API listens on port `5111`, migrations are applied on start. On a real phone set **Settings → Server address** to `http://<your PC's IP>:5111`.

## 🚀 Running Locally

You need the .NET 10 SDK with the MAUI workload, PostgreSQL and an Android emulator.
The app itself works without the server — the server is only needed for accounts and sync.

1. Create a PostgreSQL user and database, then set your connection string and JWT key:

```bash
cd ShelfieBackend
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=shelfie_backend_db;Username=shelfie_user;Password=YOUR_PASSWORD;TrustServerCertificate=true;"
dotnet user-secrets set "Jwt:Key" "A_RANDOM_KEY_OF_AT_LEAST_32_CHARACTERS"
```

2. Apply the migrations and start the backend:

```bash
dotnet ef database update
dotnet run --launch-profile http
```

The backend refuses to start without a `Jwt:Key` of at least 32 characters.

3. Open `Shelfie.sln` and run `MauiApp1` on the Android emulator. By default it uses `http://10.0.2.2:5111` (the host machine as seen from the emulator); change it in **Settings → Server address**.

> ⚠️ Upgrading an existing database: the `UnifiedInventory` migration moves products, medications, custom categories and history into the new tables and drops the old ones. Back up the database first (`pg_dump`). It also disables the password of the `admin@example.com` account created by the first migration, because that password is published in this repository.

## 🧪 Tests

```bash
dotnet test --project ShelfieBackend.Tests
dotnet test --project Shelfie.Client.Tests
```

`ShelfieBackend.Tests` runs the real API in memory on SQLite (no PostgreSQL needed), including end-to-end offline sync between two simulated devices. `Shelfie.Client.Tests` covers the offline inventory, notifications planning, barcode parsing and shared rules. CI runs both on every push (`.github/workflows/ci.yml`).
