# QR-Trigger Tự Động - Tài Liệu Triển Khai

## Giới Thiệu
Chức năng QR-Trigger tự động cho phép tạo mã QR code tự động khi tạo một POI mới. Mỗi POI có thể có nhiều QR triggers cho các ngôn ngữ khác nhau.

## Kiến Trúc

### Database Schema
```sql
CREATE TABLE [QRTriggers] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [PoiId] INT NOT NULL FOREIGN KEY,
    [QrContent] NVARCHAR(2048) NOT NULL,         -- Format: {poiId}|{languageCode}
    [LanguageCode] NVARCHAR(10) NOT NULL,
    [QrImageBase64] NVARCHAR(MAX),               -- QR code as Base64 PNG
    [QrImageUrl] NVARCHAR(1000),                 -- URL to stored QR image
    [CreatedAtUtc] DATETIME2 NOT NULL,
    [UpdatedAtUtc] DATETIME2 NOT NULL,
    [ScanCount] INT DEFAULT 0,                   -- Track number of scans
    [Status] NVARCHAR(20) DEFAULT 'Active'
);

CREATE UNIQUE INDEX IX_QRTriggers_PoiId_LanguageCode 
    ON QRTriggers(PoiId, LanguageCode);
```

### QRTrigger Model
```csharp
public class QRTrigger
{
    public int Id { get; set; }
    public int PoiId { get; set; }               // Foreign key to POI
    public string QrContent { get; set; }        // "{poiId}|{languageCode}"
    public string LanguageCode { get; set; }     // e.g., "vi", "en"
    public string? QrImageBase64 { get; set; }   // PNG image as Base64
    public string? QrImageUrl { get; set; }      // URL to stored image
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int ScanCount { get; set; }           // Track scans
    public string Status { get; set; }           // "Active" or "Inactive"
    
    public virtual POI? POI { get; set; }        // Navigation property
}
```

## API Endpoints

### 1. Tạo POI (Tự động tạo QR)
```http
POST /api/poi
Content-Type: application/json

{
    "name": "Phở Ẩm Thực Vĩnh Khánh",
    "description": "Khu ẩm thực nổi tiếng",
    "latitude": 10.7589,
    "longitude": 106.7072,
    "radius": 1200,
    "priority": 10,
    "languageCode": "vi"
}
```

**Response:**
```json
{
    "id": 1,
    "name": "Phở Ẩm Thực Vĩnh Khánh",
    "description": "Khu ẩm thực nổi tiếng",
    "latitude": 10.7589,
    "longitude": 106.7072,
    "radius": 1200,
    "priority": 10,
    "languageCode": "vi",
    "imageUrl": null,
    "audioUrl": null
}
```

**Tự động:** QR trigger sẽ được tạo cho POI với languageCode "vi".

---

### 2. Tạo QR Trigger thêm cho ngôn ngữ khác
```http
POST /api/poi/{poiId}/generate-qr?languageCode=en
```

**Response:**
```json
{
    "message": "QR trigger tạo thành công",
    "qrContent": "1|en",
    "languageCode": "en",
    "qrImageBase64": "iVBORw0KGgoAAAANSUhEUgAA...",
    "createdAt": "2026-04-14T17:00:00Z"
}
```

---

### 3. Lấy danh sách QR Triggers cho POI
```http
GET /api/poi/{poiId}/qr-triggers
```

**Response:**
```json
[
    {
        "id": 1,
        "qrContent": "1|vi",
        "languageCode": "vi",
        "scanCount": 15,
        "createdAtUtc": "2026-04-14T17:00:00Z",
        "imagePreview": "iVBORw0KGgoAAAANSUhEUgAA..."
    },
    {
        "id": 2,
        "qrContent": "1|en",
        "languageCode": "en",
        "scanCount": 0,
        "createdAtUtc": "2026-04-14T17:05:00Z",
        "imagePreview": "iVBORw0KGgoAAAANSUhEUgAA..."
    }
]
```

---

### 4. Lấy QR Trigger theo ID
```http
GET /api/poi/qr-triggers/{qrId}
```

**Response:**
```json
{
    "id": 1,
    "poiId": 1,
    "qrContent": "1|vi",
    "languageCode": "vi",
    "qrImageBase64": "iVBORw0KGgoAAAANSUhEUgAA...",
    "scanCount": 15,
    "status": "Active",
    "createdAtUtc": "2026-04-14T17:00:00Z",
    "updatedAtUtc": "2026-04-14T17:00:00Z"
}
```

---

### 5. Ghi nhận lần quét QR
```http
POST /api/poi/qr-triggers/{qrId}/track-scan
Content-Type: application/json

{
    "deviceId": "device-123",
    "latitude": 10.7589,
    "longitude": 106.7072
}
```

**Response:**
```json
{
    "message": "Quét QR được ghi nhận thành công",
    "qrId": 1,
    "scanCount": 16
}
```

---

### 6. Xóa QR Trigger
```http
DELETE /api/poi/qr-triggers/{qrId}
```

**Response:** 204 No Content

---

## Services

### IQRGeneratorService

```csharp
public interface IQRGeneratorService
{
    /// <summary>
    /// Tạo QR trigger cho một POI
    /// </summary>
    Task<QRTrigger> GenerateQRTriggerAsync(int poiId, string languageCode = "vi");

    /// <summary>
    /// Tạo QR content từ POI ID và language code
    /// Format: {poiId}|{languageCode}
    /// </summary>
    string GenerateQRContent(int poiId, string languageCode = "vi");

    /// <summary>
    /// Tạo QR code PNG image từ content
    /// </summary>
    Task<string> GenerateQRImageBase64Async(string content, int pixelPerModule = 10);

    /// <summary>
    /// Tạo QR code ASCII format từ content
    /// </summary>
    string GenerateQRImageSvg(string content);
}
```

### Cách sử dụng trong Controller
```csharp
public class PoiController : ControllerBase
{
    private readonly IQRGeneratorService _qrGeneratorService;

    public PoiController(IQRGeneratorService qrGeneratorService)
    {
        _qrGeneratorService = qrGeneratorService;
    }

    [HttpPost]
    public async Task<ActionResult<POI>> Create(POI poi)
    {
        // Tạo POI
        _context.POIs.Add(poi);
        await _context.SaveChangesAsync();

        // Tạo QR trigger tự động
        var qrTrigger = await _qrGeneratorService.GenerateQRTriggerAsync(
            poi.Id, 
            poi.LanguageCode ?? "vi"
        );
        _context.QRTriggers.Add(qrTrigger);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = poi.Id }, poi);
    }
}
```

## QR Content Format

QR code content được encode theo format:
```
{poiId}|{languageCode}
```

**Ví dụ:**
- `1|vi` - POI ID 1 với Tiếng Việt
- `42|en` - POI ID 42 với Tiếng Anh
- `5|ja` - POI ID 5 với Tiếng Nhật

Mobile app sẽ parse format này khi quét QR code để:
1. Lấy POI ID (`poiId`)
2. Lấy Language Code (`languageCode`)
3. Fetch content từ API với các thông số trên
4. Trigger playback với đúng ngôn ngữ

## Công nghệ sử dụng

- **QRCoder 1.4.3** - Thư viện tạo QR code cho .NET
- **Entity Framework Core** - ORM cho database
- **ASP.NET Core** - Web framework

## Migration

Migration file: `20260414170004_AddQRTrigger.cs`

Để apply migration:
```bash
cd ./backend/BackendAPI
dotnet ef database update
```

## Dependency Injection

Đã đăng ký trong Program.cs:
```csharp
builder.Services.AddScoped<IQRGeneratorService, QRGeneratorService>();
```

## Tương lai

### Có thể bổ sung:
1. **Upload QR Image** - Lưu QR image lên cloud storage (S3, Azure Blob)
2. **Custom QR Styling** - Tùy chỉnh màu sắc, logo QR
3. **Batch QR Generation** - Tạo QR cho nhiều POI cùng lúc
4. **QR Analytics** - Thống kê quét QR theo thời gian, vị trí
5. **Dynamic QR** - QR code động có thể chứa URL, tel, email
6. **QR Print** - Export QR code để in lên bản đồ vật lý

