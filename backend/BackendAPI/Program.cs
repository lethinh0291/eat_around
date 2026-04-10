using BackendAPI.Data;
using Microsoft.EntityFrameworkCore;
using SharedLib.Models;


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