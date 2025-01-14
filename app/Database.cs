namespace app;

using Npgsql;
public class Database
{
    private readonly string _host = "localhost";
    private readonly string _port = "5432";
    private readonly string _username = "postgres";
    private readonly string _password = "Sanooj14321";
    private readonly string _database = "crosswarsdatabase";

    private NpgsqlDataSource _connection;

    public NpgsqlDataSource Connection()
    {
        return _connection;
    } 
    
    public Database()
    {
        // bygg en anslutningssträng (Adress och inloggning till databasen) 
        string connectionString = $"Host={_host};Port={_port};Username={_username};Password={_password};Database={_database}";
        // använd den för att hämta en anslutning
        _connection = NpgsqlDataSource.Create(connectionString);
    }
}