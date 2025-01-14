using Npgsql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;
using System.Data.Common;
using Microsoft.VisualBasic;

namespace app;

public class Actions
{
    private readonly NpgsqlDataSource _db;

    public Actions(WebApplication app, NpgsqlDataSource db)
    {
        _db = db;

        // Endpoint to add a new player
       
        app.MapGet("api/randomCrossWordInfo/",getCrossWordId );
        app.MapPost("api/SetupEmptyTiles", AddEmptyTiles);
        
        app.MapPost("/new-player/", async (HttpContext context) =>
        {
            try
            {
                var requestBody = await context.Request.ReadFromJsonAsync<WordRequest>();
                if (requestBody?.Word is null)
                {
                    return Results.BadRequest("Word is required.");
                }

                string cookie = context.Request.Cookies["ClientId"] ?? "anonymous";

                // Insert the player into the database
                bool success = await NewPlayer(requestBody.Word, cookie);
                return success
                    ? Results.Ok(new { message = "Player added successfully!" })
                    : Results.Problem("Failed to add player to the database.", statusCode: 500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return Results.Problem("An unexpected error occurred.", statusCode: 500);
            }
        });
    }

    // Method to add a new player to the database
    private async Task<bool> NewPlayer(string name, string cookie)
    {
        try
        {
            await using var cmd = _db.CreateCommand("INSERT INTO player (name, cookie) VALUES ($1, $2)");
            cmd.Parameters.AddWithValue(name);
            cmd.Parameters.AddWithValue(cookie);
            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            Console.WriteLine($"Rows Affected: {rowsAffected}");
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error: {ex.Message}");
            return false;
        }
    }
    private async Task<string> getCrossWordId(){
        List<string> results= new();  

        await using var cmd= _db.CreateCommand(); 
        cmd.CommandText="select * from cross_word order by random() limit 1"; 
        await using var reader= await cmd.ExecuteReaderAsync();
        while(await reader.ReadAsync()){
            
            var id = reader.GetInt32(0);
            var row = reader.GetInt32(1); 
            var column= reader.GetInt32(2); 
            
            results.Add($"{id}" ); 
            results.Add($"{row}" ); 
            results.Add($"{column}"); 
        }
        var output=string.Join(",",results); 
        
        return output; 

    }

    // - av typen 'WordRequest' som innehåller en property 'Word'
    public async Task<IResult> AddEmptyTiles(HttpContext context)
    {   
        var requestBody = await context.Request.ReadFromJsonAsync<GameInfo>();
        
        // Om requestBody är null eller om ordet är tomt så returneras en BadRequest
        if (requestBody == null || string.IsNullOrEmpty(requestBody.Game)|| string.IsNullOrEmpty(requestBody.CrossWord))
        {
            return Results.BadRequest("Invalid request");
        }

        var game = requestBody.Game;
        var crossWordId = requestBody.CrossWord; 
        
        bool exist = await GameTilesExists(game);

        // Om ordet redan finns i databasen så returneras en BadRequest
        if (exist)
        {
            return Results.BadRequest("The Game already has a game bord");
        }

        var coordinates = await GetCoordinates(crossWordId);

        bool added;
        int gameId = Int32.Parse(game); 
        for (int i = 0; i < coordinates.Count; i += 2)
        {
            added = await AddTile(gameId , coordinates[i], coordinates[i + 1]);
            if (!added)
            {
                return Results.Problem("Error in adding to database"); 
            }
        }
        return Results.Ok("Word added");
    }
    
    private async Task<bool> GameTilesExists(string game)
    {
        await using var cmd = _db.CreateCommand();
        cmd.CommandText = "Select exists(select * from placed_letters where game=$1);"; 
        cmd.Parameters.AddWithValue(Int32.Parse(game)); 
        
        return (bool)(await cmd.ExecuteScalarAsync() ?? false);
    }

    private async Task<List<int>>  GetCoordinates(string crossword)
    {
        var coordinates = new List<int>(); 
        
        await using var cmd = _db.CreateCommand();
        cmd.CommandText = "select distinct row,\"column\" from cross_word_letter_placement where cross_word=$1";
        cmd.Parameters.AddWithValue(Int32.Parse(crossword));
        
        await using var reader = await cmd.ExecuteReaderAsync(); // Här exekverar vi SQL-queryn och skapar en ny instans av klassen NpgsqlDataReader som heter reader
        while (await reader.ReadAsync()) // Här loopar vi igenom alla rader som finns i tabellen 'player' och lägger till dem i listan 'result'
        {
            coordinates.Add(reader.GetInt32(0));
            coordinates.Add(reader.GetInt32(1));
        }

        return coordinates; 
    }

    private async Task<bool> AddTile(int game, int row, int column)
    {
        await using var cmd = _db.CreateCommand(); 
        cmd.CommandText=" INSERT INTO placed_letters(game, letter, row, \"column\") values ($1,$2, $3, $4)";
        cmd.Parameters.AddWithValue(game);
        cmd.Parameters.AddWithValue(" ");
        cmd.Parameters.AddWithValue(row); 
        cmd.Parameters.AddWithValue(column);
        int rowsAffected = await cmd.ExecuteNonQueryAsync(); // Returns the number of rows affected
        if (rowsAffected > 0)
        { 
            return true; // Return true if the move was successful   
        }
        return false;
    }
    

    
}