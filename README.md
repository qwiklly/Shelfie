# 📦 Shelfie – Smart Inventory App

Shelfie is a smart inventory app built with .NET MAUI and ASP.NET Core.  
It helps you manage items by categories (such as products, medicines, and custom types), track expiration dates, and securely store data using a backend and PostgreSQL.

![ShelfieCard](https://github.com/user-attachments/assets/774ceda1-9f56-4318-91a9-209e9e65cbdb)

---

## ✨ Features

- 📊 Dashboard with category statistics and recent operations
- 📁 Product & medicine management (add/edit/delete, track expiry)
- 🧩 Custom categories with flexible fields (define fields once and reuse them when adding items).
- 🛡️ Secure profile system (login, register, update, logout)
- 🌐 Localization (English / Russian)
- 📡 Backend synchronization 
- 📱 Mobile-first design using .NET MAUI (Blazor)

---

## 🛠️ Tech Stack

- **Frontend**: .NET MAUI (Blazor)
- **Backend**: ASP.NET Core 
- **Database**: PostgreSQL, Entity Framework Core
- **Auth**: JWT-based authentication
- **Languages**: C#, JS, Razor, HTML/CSS

---

## 🚀 Running Locally

You need the .NET 8 SDK with the MAUI workload, PostgreSQL and an Android emulator.

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

3. Open `Shelfie.sln` and run `MauiApp1` on the Android emulator. It connects to the backend at `http://10.0.2.2:5111/api/`.

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
