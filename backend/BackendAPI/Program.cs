var builder = WebApplication.CreateBuilder(args);

// 🔥 THÊM DÒNG NÀY
builder.Services.AddControllers();

var app = builder.Build();

// 🔥 THÊM DÒNG NÀY
app.MapControllers();

app.Run();