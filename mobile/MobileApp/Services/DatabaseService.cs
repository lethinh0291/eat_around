using SQLite;
using SharedLib.Models;

namespace MobileApp.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection _db;

    private async Task Init()
    {
        if (_db != null)
            return;

        [cite_start]// Tạo đường dẫn lưu trữ Database trên thiết bị [cite: 78]
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "POI.db");

        _db = new SQLiteAsyncConnection(databasePath);

        [cite_start]// Khởi tạo bảng POI dựa trên Model đã có [cite: 95]
        await _db.CreateTableAsync<POI>();
    }

    [cite_start]// Lấy danh sách POI từ bộ nhớ cục bộ (dùng khi không có wifi) [cite: 4, 71]
    public async Task<List<POI>> GetPOIsAsync()
    {
        await Init();
        return await _db.Table<POI>().ToListAsync();
    }

    [cite_start]// Lưu/Cập nhật danh sách POI từ Server về máy [cite: 96, 106]
    public async Task SavePoisAsync(List<POI> pois)
    {
        await Init();
        [cite_start]// Xóa dữ liệu cũ và chèn dữ liệu mới để đảm bảo đồng bộ nhất [cite: 106]
        await _db.DeleteAllAsync<POI>();
        await _db.InsertAllAsync(pois);
    }
}