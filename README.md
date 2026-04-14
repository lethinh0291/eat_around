# ZesTour Solution

## 1) Tong quan

ZesTour la he thong quan ly va trai nghiem quan an gom 4 phan:

- Admin Web: quan tri user va quan an, duyet dang ky quan, chinh sua du lieu map.
- Backend API: cung cap API cho admin web va mobile app, luu du lieu SQL Server.
- Mobile App (.NET MAUI): app cho customer/seller, dang nhap, dang ky quan, quan ly quan cua seller.
- SharedLib: model dung chung giua cac project.

Muc tieu chinh:

- Quan ly diem/quan an tap trung.
- Duyet quan dang ky tu seller len ban do.
- Dong bo du lieu giua web admin va mobile.

## 2) Kien truc du an

C.sln

- admin/AdminWeb
- backend/BackendAPI
- mobile/MobileApp
- shared/SharedLib

Target framework hien tai:

- AdminWeb: net10.0
- BackendAPI: net10.0
- SharedLib: net10.0
- MobileApp: net10.0-android, net10.0-ios, net10.0-maccatalyst, net10.0-windows

## 3) Tinh nang chinh

### Admin Web

- Dang nhap quan tri bang cookie auth.
- Dashboard tong quan co KPI, chart, hoat dong gan day va cac khu vuc san cho analytics/tour/logs/settings.
- Quan ly Users:
  - Tao user
  - Chinh sua bang popup
  - Xoa user
- Quan ly Quan an:
  - Bang quan da len map
  - Chinh sua quan bang popup (ten, mo ta, toa do, radius, priority, image/audio URL)
  - Xoa quan
  - Hien thi anh review
- Duyet danh sach quan dang ky:
  - Xem thong tin va anh
  - Chon toa do tren map
  - Duyet de tao POI
  - Chinh sua dang ky bang popup
  - Xoa dang ky

### Mobile App

- Dang ky / dang nhap theo role (customer, seller, admin).
- Seller gui dang ky quan an.
- Seller quan ly quan da gui (xem/sua/xoa theo owner).
- Upload anh quan qua Cloudinary truoc khi gui dang ky.

### Backend API

- Auth: register, login, quan ly users (CRUD + role).
- Store registrations: tao, lay danh sach, owner-scope update/delete, admin duyet/xoa.
- POI CRUD.
- EF Core migration tu chay khi khoi dong backend.
- Seed du lieu mau POI va user khi DB trong.

## 4) Cong nghe su dung

- ASP.NET Core MVC
- ASP.NET Core Web API
- Entity Framework Core + SQL Server
- .NET MAUI
- Swagger/OpenAPI
- Leaflet (map picker o admin)
- Cloudinary (upload anh quan tu mobile)

## 5) Yeu cau moi truong

- .NET SDK 10
- SQL Server (LocalDB/Express/full)
- Android Emulator hoac thiet bi that (neu chay mobile Android)
- Visual Studio 2022 hoac VS Code + C# Dev Kit

## 6) Cau hinh quan trong

### Backend DB connection

File: backend/BackendAPI/appsettings.json

Connection string mac dinh:
Server=localhost;Database=FoodStreetDB;Trusted_Connection=True;TrustServerCertificate=True

### Admin tro ve backend

File: admin/AdminWeb/appsettings.json

BaseUrl mac dinh:
http://localhost:5069/

### Tai khoan admin web mac dinh

File: admin/AdminWeb/appsettings.json

- Username: admin
- Password: admin123

Luu y: backend cung seed user mac dinh:

- admin / admin123
- seller / seller123
- customer / customer123

### Mobile base URL

File: mobile/MobileApp/Services/APIService.cs

Mac dinh dung emulator Android:
http://10.0.2.2:5069/api/

## 7) Cach chay nhanh (khuyen nghi)

### Buoc 1: chay Backend API

PowerShell:

- cd backend/BackendAPI
- dotnet run

Backend mac dinh:

- http://localhost:5069
- Swagger: http://localhost:5069/swagger

### Buoc 2: chay Admin Web

PowerShell moi:

- cd admin/AdminWeb
- dotnet run

Admin mac dinh:

- http://localhost:5292

### Buoc 3: chay Mobile (Android)

- Mo emulator truoc
- Build/run project mobile/MobileApp voi target net10.0-android

## 8) Migration database

Trong backend/BackendAPI:

- dotnet ef migrations add TenMigration
- dotnet ef database update

Luu y:

- Ung dung backend dang goi db.Database.Migrate() khi startup, nen DB se tu cap nhat migration hien co.

## 9) API chinh (tom tat)

Auth:

- POST /api/auth/register
- POST /api/auth/login
- GET /api/auth/users
- POST /api/auth/users
- PUT /api/auth/users/{id}
- PUT /api/auth/users/{id}/role
- DELETE /api/auth/users/{id}

Store registrations:

- POST /api/store-registrations
- GET /api/store-registrations
- GET /api/store-registrations/owner?ownerName=...
- PUT /api/store-registrations/{id}
- DELETE /api/store-registrations/{id}

POI:

- GET /api/poi
- GET /api/poi/{id}
- POST /api/poi
- PUT /api/poi/{id}
- DELETE /api/poi/{id}

## 10) Troubleshooting thuong gap

### 1. Port da bi chiem (5069 hoac 5292)

- Dau hieu: dotnet run bao Address already in use.
- Cach xu ly: dung process cu dang giu port roi chay lai.

### 2. Build loi file bi lock (MSB3021/MSB3027)

- Nguyen nhan: app dang chay giu file trong bin/Debug.
- Cach xu ly:
  - Stop app dang chay truoc khi build
  - Hoac build ra thu muc output tam rieng

### 3. Admin khong thay user/store du DB co du lieu

- Nguyen nhan pho bien: backend instance dang chay la ban cu hoac sai thu muc.
- Cach xu ly: chay lai backend dung project folder backend/BackendAPI.

### 4. Mobile goi backend local khong duoc

- Dung Android emulator thi base URL phai la 10.0.2.2.
- AndroidManifest da bat usesCleartextTraffic=true cho HTTP local.

### 5. Favicon tab chua doi ngay

- Browser thuong cache favicon.
- Hard refresh (Ctrl+F5) hoac mo tab moi.

## 11) Ghi chu phat trien

- Hien dieu huong admin da gom trong tam vao Users va Quan an.
- Quan ly quan da bao gom thao tac map nen khong can dung tab POI rieng trong menu chinh.
- Co the tach cau hinh moi truong bang appsettings.Development.json va secret manager khi can.
