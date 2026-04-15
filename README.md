# ZesTour (eat_around)

Hệ thống quản lý và trải nghiệm quán ăn gồm 3 thành phần chính: Backend API, Admin Web và Mobile App.
Dự án cho phép seller gửi đăng ký quán ăn, admin duyệt và quản lý POI, customer xem và tương tác với dữ liệu quán ăn.
Mục tiêu là đồng bộ dữ liệu tập trung giữa web quản trị và mobile thông qua Backend API.

## 1. Cài đặt

### 1.1 Yêu cầu môi trường

- .NET SDK 10
- SQL Server (LocalDB/Express/Full)
- Visual Studio 2022 hoặc VS Code + C# Dev Kit
- Android Emulator (nếu chạy mobile Android)

Kiểm tra nhanh:

```powershell
dotnet --version
sqllocaldb info mssqllocaldb
```

Nếu cần khởi tạo LocalDB:

```powershell
sqllocaldb create mssqllocaldb
sqllocaldb start mssqllocaldb
```

### 1.2 Cấu trúc solution

- admin/AdminWeb
- backend/BackendAPI
- mobile/MobileApp
- shared/SharedLib

### 1.3 Cấu hình quan trọng

1. Backend connection string:

File: backend/BackendAPI/appsettings.json

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=FoodStreetDB;Trusted_Connection=True;TrustServerCertificate=True"
}
```

2. Admin BaseUrl trỏ về backend:

File: admin/AdminWeb/appsettings.json

```json
"BackendApi": {
  "BaseUrl": "http://localhost:5069/"
}
```

3. Mobile API base URL:

File: mobile/MobileApp/Services/APIService.cs

- Android emulator: http://10.0.2.2:5069/api/
- Thiết bị thật: đổi thành IP LAN của máy chạy backend (ví dụ: http://192.168.1.100:5069/api/)

### 1.4 Chạy dự án

Bước 1: Chạy Backend API

```powershell
cd backend/BackendAPI
dotnet restore
dotnet build
dotnet run
```

Kiểm tra:

- Backend: http://localhost:5069
- Swagger: http://localhost:5069/swagger

Bước 2: Chạy Admin Web (terminal mới)

```powershell
cd admin/AdminWeb
dotnet restore
dotnet build
dotnet run
```

Kiểm tra:

- Admin Web: http://localhost:5292

Bước 3: Chạy Mobile App (tùy chọn)

```powershell
cd mobile/MobileApp
dotnet build -f net10.0-android -c Debug
```

Lưu ý: Mở emulator trước khi build/chạy mobile.

## 2. Cách sử dụng

### 2.1 Tài khoản mặc định

Tài khoản seed sẵn:

- admin / admin123
- seller / seller123
- customer / customer123

### 2.2 Luồng sử dụng cơ bản

1. Mở Swagger để test API:
   - http://localhost:5069/swagger
2. Đăng nhập Admin Web:
   - http://localhost:5292
   - Tài khoản: admin / admin123
3. Seller trên mobile:
   - Đăng nhập seller
   - Tạo đăng ký quán ăn
4. Admin duyệt đăng ký:
   - Vào Admin Web
   - Xem Store Registrations
   - Duyệt để tạo POI

### 2.3 Một số endpoint chính

Auth:

- POST /api/auth/register
- POST /api/auth/login
- GET /api/auth/users

Store Registrations:

- POST /api/store-registrations
- GET /api/store-registrations
- PUT /api/store-registrations/{id}
- DELETE /api/store-registrations/{id}

POI:

- GET /api/poi
- POST /api/poi
- PUT /api/poi/{id}
- DELETE /api/poi/{id}

### 2.4 Khắc phục lỗi nhanh

1. Lỗi port đã bị chiếm:

```powershell
netstat -ano | findstr :5069
taskkill /PID <PID> /F
```

2. Admin không có dữ liệu:
- Kiểm tra backend đã chạy đúng port và đúng project backend/BackendAPI.

3. Mobile không gọi được local API:
- Kiểm tra base URL dùng 10.0.2.2 (emulator).
- Kiểm tra backend đang chạy.

## 3. Công nghệ sử dụng

- ASP.NET Core MVC (Admin Web)
- ASP.NET Core Web API (Backend)
- Entity Framework Core + SQL Server
- .NET MAUI (Mobile)
- Swagger / OpenAPI
- Leaflet (map picker trong admin)
- Cloudinary (upload ảnh từ mobile)

Target frameworks:

- AdminWeb: net10.0
- BackendAPI: net10.0
- SharedLib: net10.0
- MobileApp: net10.0-android, net10.0-ios, net10.0-maccatalyst, net10.0-windows

## 4. Đóng góp

Cảm ơn bạn đã quan tâm đóng góp cho dự án.

Quy trình đề xuất:

1. Fork repository
2. Tạo branch mới:

```powershell
git checkout -b feature/ten-tinh-nang
```

3. Commit rõ ràng:

```powershell
git add .
git commit -m "feat: mo ta thay doi"
```

4. Push branch:

```powershell
git push origin feature/ten-tinh-nang
```

5. Tạo Pull Request với mô tả:
- Mục tiêu thay đổi
- Phạm vi ảnh hưởng
- Cách test lại

Khuyến nghị:

- Giữ code style nhất quán
- Không commit secrets
- Test lại các luồng chính trước khi tạo PR

---

Nếu bạn gặp vấn đề khi setup/chạy dự án, vui lòng mở issue kèm log lỗi và bước tái hiện.
