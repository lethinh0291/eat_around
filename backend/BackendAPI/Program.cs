using BackendAPI.Data;
using Microsoft.EntityFrameworkCore;
using SharedLib.Models;
using System.Globalization;
using System.Net.Http.Json;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
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

file sealed class NominatimGeoResult
{
    public string Lat { get; set; } = string.Empty;
    public string Lon { get; set; } = string.Empty;
}