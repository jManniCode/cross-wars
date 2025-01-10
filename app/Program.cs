
using app;using Npgsql;Console.WriteLine("Hello, World!");

Database database = new();

NpgsqlDataSource db;
    db = database.Connection();



await using (var cmd = db.CreateCommand("SELECT * FROM player"))
await using (var reader = await cmd.ExecuteReaderAsync())
    while (await reader.ReadAsync())
    {
        Console.WriteLine("Hejsan Svejsan!");
        Console.WriteLine(
            $"{reader.GetInt32(0)} "+
            $"{reader.GetString(1)} "+
            $"{reader.GetString(2)} "); 
    }
