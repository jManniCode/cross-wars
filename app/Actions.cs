using Npgsql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
namespace app;

public class Actions
{
    private readonly NpgsqlDataSource _db;

    public Actions(WebApplication app, NpgsqlDataSource db)
    {
        _db = db;

        // Endpoint to add a new player
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

        app.MapPost("/session/", async (HttpContext context) =>
        {
            try
            {
                var requestBody = await context.Request.ReadFromJsonAsync<GameRequest>();
                if (requestBody?.Word is null)
                {
                    return Results.BadRequest("Game id is required.");
                }

                
                // Insert the game into the database
                bool success = await NewGame(requestBody.Word);
                return success
                    ? Results.Ok(new { message = "New game session created" })
                    : Results.Problem("Failed to add game to the database.", statusCode: 500);
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
            await using var connection = await _db.OpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO player (name, cookie) VALUES ($1, $2)";

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
    
   
    
    private async Task<bool> NewGame(string session)
    {
        Console.WriteLine("NewGame method called with session: " + session);
        try
        {
            await using var connection = await _db.OpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO game (session) VALUES ($1) Returning id";
            cmd.Parameters.AddWithValue(session);
            int newGameId = (int)await cmd.ExecuteScalarAsync();
            Console.WriteLine($"New Game ID: {newGameId}");
            return newGameId > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database Error: {ex.Message}");
            return false;
        }
    }
    
   
}