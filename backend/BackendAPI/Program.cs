using BackendAPI.Data;
using BackendAPI.Services;
using Microsoft.EntityFrameworkCore;
using SharedLib.Models;
using System.Globalization;
using System.Net.Http.Json;
using ProgramHelpers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
// Register QR Generator Service
builder.Services.AddScoped<IQRGeneratorService, QRGeneratorService>();
// Sau đó dưới app.UseRouting() thêm:

//test api
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await EnsureUserColumnsAsync(db);
    await EnsureStoreRegistrationColumnsAsync(db);
    await EnsureOperationalTablesAsync(db);
    await BackfillStoreRegistrationLocationsAsync(db);

    if (!db.POIs.Any())
    {
        db.POIs.AddRange(
            new POI
            {
                Name = "Phố Ẩm Thực Vĩnh Khánh",
                Description = "Khu ẩm thực nổi tiếng tại Phường 10, Quận 4, TP.HCM.",
                Latitude = 10.7589,
                Longitude = 106.7072,
                Radius = 1200,
                Priority = 10,
                LanguageCode = "vi"
            },
            new POI
            {
                Name = "Bánh Mì Huỳnh Hoa",
                Description = "Bánh mì nổi tiếng, nhiều nhân, đậm vị.",
                Latitude = 10.7714,
                Longitude = 106.6960,
                Radius = 400,
                Priority = 8,
                LanguageCode = "vi"
            },
            new POI
            {
                Name = "Ốc Oanh Vĩnh Khánh",
                Description = "Quán ốc quen thuộc trên tuyến ẩm thực Vĩnh Khánh.",
                Latitude = 10.7585,
                Longitude = 106.7079,
                Radius = 600,
                Priority = 7,
                LanguageCode = "vi"
            }
        );

        db.SaveChanges();
    }

    if (!db.Users.Any())
    {
        db.Users.AddRange(
            new User
            {
                Name = "System Admin",
                Email = "admin@zestour.local",
                Username = "admin",
                Password = "admin123",
                Role = "admin"
            },
            new User
            {
                Name = "Seller Demo",
                Email = "seller@zestour.local",
                Username = "seller",
                Password = "seller123",
                Role = "seller"
            },
            new User
            {
                Name = "Customer Demo",
                Email = "customer@zestour.local",
                Username = "customer",
                Password = "customer123",
                Role = "customer"
            }
        );

        db.SaveChanges();
    }
}

static async Task EnsureStoreRegistrationColumnsAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('StoreRegistrations', 'ImageUrlsJson') IS NULL
BEGIN
    ALTER TABLE [StoreRegistrations] ADD [ImageUrlsJson] nvarchar(max) NULL;
END;

IF COL_LENGTH('StoreRegistrations', 'Latitude') IS NULL
BEGIN
    ALTER TABLE [StoreRegistrations] ADD [Latitude] float NULL;
END;

IF COL_LENGTH('StoreRegistrations', 'Longitude') IS NULL
BEGIN
    ALTER TABLE [StoreRegistrations] ADD [Longitude] float NULL;
END;

IF COL_LENGTH('StoreRegistrations', 'RadiusMeters') IS NULL
BEGIN
    ALTER TABLE [StoreRegistrations] ADD [RadiusMeters] float NULL;
END;
""");
}

static async Task EnsureUserColumnsAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('Users', 'AvatarUrl') IS NULL
BEGIN
    ALTER TABLE [Users] ADD [AvatarUrl] nvarchar(1000) NULL;
END;
""");
}

static async Task BackfillStoreRegistrationLocationsAsync(AppDbContext db)
{
    var missingLocations = await db.StoreRegistrations
        .Where(item => item.Latitude == null || item.Longitude == null)
        .ToListAsync();

    if (missingLocations.Count == 0)
    {
        return;
    }

    using var httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    if (httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
    {
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ZesTour-Backend/1.0");
    }

    var updated = false;
    foreach (var item in missingLocations)
    {
        var address = item.Address?.Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            continue;
        }

        try
        {
            var encoded = Uri.EscapeDataString(address);
            var endpoint = $"https://nominatim.openstreetmap.org/search?format=jsonv2&countrycodes=vn&limit=1&q={encoded}";
            var results = await httpClient.GetFromJsonAsync<List<NominatimGeoResult>>(endpoint);
            var first = results?.FirstOrDefault();
            if (first is null ||
                !double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                continue;
            }

            item.Latitude = latitude;
            item.Longitude = longitude;
            item.RadiusMeters = item.RadiusMeters is > 0 ? item.RadiusMeters : 140;
            updated = true;
        }
        catch
        {
            // Ignore startup backfill failures and continue serving the app.
        }
    }

    if (updated)
    {
        await db.SaveChangesAsync();
    }
}

//Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowAll");
app.MapControllers();

app.Run();

static async Task EnsureOperationalTablesAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[ListenLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [ListenLogs] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DeviceId] NVARCHAR(120) NOT NULL,
        [PoiId] INT NOT NULL,
        [LanguageCode] NVARCHAR(10) NOT NULL,
        [ContentType] NVARCHAR(20) NOT NULL,
        [DurationSeconds] FLOAT NOT NULL,
        [Latitude] FLOAT NOT NULL,
        [Longitude] FLOAT NOT NULL,
        [PlayedAtUtc] DATETIME2 NOT NULL
    );
END;

IF OBJECT_ID(N'[SystemLogEntries]', N'U') IS NULL
BEGIN
    CREATE TABLE [SystemLogEntries] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Category] NVARCHAR(40) NOT NULL,
        [Level] NVARCHAR(20) NOT NULL,
        [Source] NVARCHAR(200) NOT NULL,
        [Message] NVARCHAR(600) NOT NULL,
        [Details] NVARCHAR(1200) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL
    );
END;

IF OBJECT_ID(N'[Tours]', N'U') IS NULL
BEGIN
    CREATE TABLE [Tours] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(150) NOT NULL,
        [Description] NVARCHAR(1000) NULL,
        [CoverImageUrl] NVARCHAR(1000) NULL,
        [IsActive] BIT NOT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        [UpdatedAtUtc] DATETIME2 NOT NULL
    );
END;

IF OBJECT_ID(N'[TourStops]', N'U') IS NULL
BEGIN
    CREATE TABLE [TourStops] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [TourId] INT NOT NULL,
        [PoiId] INT NOT NULL,
        [SortOrder] INT NOT NULL,
        [Note] NVARCHAR(500) NULL,
        CONSTRAINT [FK_TourStops_Tours_TourId] FOREIGN KEY ([TourId]) REFERENCES [Tours]([Id]) ON DELETE CASCADE
    );
END;

IF OBJECT_ID(N'[PoiTranslations]', N'U') IS NULL
BEGIN
    CREATE TABLE [PoiTranslations] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PoiId] INT NOT NULL,
        [LanguageCode] NVARCHAR(10) NOT NULL,
        [Title] NVARCHAR(200) NULL,
        [ContentText] NVARCHAR(3000) NOT NULL,
        [AudioUrl] NVARCHAR(1000) NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [SubmittedBy] NVARCHAR(120) NULL,
        [ReviewedBy] NVARCHAR(120) NULL,
        [SubmittedAtUtc] DATETIME2 NOT NULL,
        [ReviewedAtUtc] DATETIME2 NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ListenLogs_PlayedAtUtc_PoiId_LanguageCode' AND object_id = OBJECT_ID(N'[ListenLogs]'))
BEGIN
    CREATE INDEX [IX_ListenLogs_PlayedAtUtc_PoiId_LanguageCode] ON [ListenLogs]([PlayedAtUtc], [PoiId], [LanguageCode]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SystemLogEntries_CreatedAtUtc_Category_Level' AND object_id = OBJECT_ID(N'[SystemLogEntries]'))
BEGIN
    CREATE INDEX [IX_SystemLogEntries_CreatedAtUtc_Category_Level] ON [SystemLogEntries]([CreatedAtUtc], [Category], [Level]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TourStops_TourId_SortOrder' AND object_id = OBJECT_ID(N'[TourStops]'))
BEGIN
    CREATE UNIQUE INDEX [IX_TourStops_TourId_SortOrder] ON [TourStops]([TourId], [SortOrder]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PoiTranslations_PoiId_LanguageCode' AND object_id = OBJECT_ID(N'[PoiTranslations]'))
BEGIN
    CREATE INDEX [IX_PoiTranslations_PoiId_LanguageCode] ON [PoiTranslations]([PoiId], [LanguageCode]);
END;
""");
}