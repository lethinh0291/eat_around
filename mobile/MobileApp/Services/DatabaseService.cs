using SQLite;
using SharedLib.Models;

namespace MobileApp.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _db;

    private async Task Init()
    {
        if (_db != null)
            return;

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "POI.db");

        _db = new SQLiteAsyncConnection(databasePath);


        await _db.CreateTableAsync<POI>();
    }


    public async Task<List<POI>> GetPOIsAsync()
    {
        await Init();
        var db = _db ?? throw new InvalidOperationException("Database connection was not initialized.");
        return await db.Table<POI>().ToListAsync();
    }


    public async Task SavePoisAsync(List<POI> pois)
    {
        await Init();

        var db = _db ?? throw new InvalidOperationException("Database connection was not initialized.");
        await db.DeleteAllAsync<POI>();
        await db.InsertAllAsync(pois);
    }
}