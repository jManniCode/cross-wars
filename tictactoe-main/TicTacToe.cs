using Npgsql;
using TicTacToe.Records;

namespace TicTacToe;

public class TicTacToeGame
{
    // initialize database
    Database database = new();
    private NpgsqlDataSource db;
    
    // Routes are defined and registered for listening for network requests
    // in the constructor below
    // and then in turn processed by methods as called from the routes definitions 
    public TicTacToeGame (WebApplication app)
    {
        // Get database connection
        db = database.Connection();
        
        // Map incomming request for current game data
        app.MapGet("/api/current-game/{gamecode}", GetCurrentGame);
        
        // Map incomming request for played tiles in game
        app.MapGet("/api/played-tiles/{id}", GetPlayedTiles);
        
        // Map incomming request to check win for a game
        app.MapGet("/api/check-win/{game}", CheckWin);
        
        // Map incomming request to add a player to a game
        app.MapPost("/api/add-player", async (HttpContext context) =>
        {
            // Player, is a record that defines the post requestBody format
            var requestBody = await context.Request.ReadFromJsonAsync<Player>();
            if (requestBody?.name is null)
            {
                return Results.BadRequest("name is required.");
            }
            var player = await AddPlayer(requestBody.name, context.Request.Cookies["ClientId"]);
            return player.id > 0 ? Results.Ok(player) : Results.StatusCode(500);
        });
        
        // Map incomming request to play a tile (make a move) in a game
        app.MapPost("/api/play-tile", async (HttpContext context) =>
        {
            var requestBody = await context.Request.ReadFromJsonAsync<Move>();
            if (requestBody?.tile is null || requestBody?.player is null || requestBody?.game is null || requestBody?.value is null)
            {
                return Results.BadRequest("tile (index), player (id), game (id), and value are required.");
            }
            bool success = await PlayTile(requestBody.tile, requestBody.player, requestBody.game, requestBody.value);
            return success ? Results.Ok(true) : Results.Ok(false);
        });
    }

    async Task<Game>? GetCurrentGame(string gamecode)
    {
        await using var cmd = db.CreateCommand("SELECT * FROM games WHERE gamecode = $1");
        cmd.Parameters.AddWithValue(gamecode);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                return new Game(reader.GetInt32(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetString(3));
            }
        }
        return null;
    }
    
    async Task<List<Move>> GetPlayedTiles(int id)
    {
        var playedTiles = new List<Move>();
        await using var cmd = db.CreateCommand("SELECT tile, player, game, value FROM moves WHERE game = $1");
        cmd.Parameters.AddWithValue(id);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                playedTiles.Add(new Move(
                    reader.GetInt32(0), // tile
                    reader.GetInt32(1), // player
                    reader.GetInt32(2), // game
                    reader.IsDBNull(3) ? null : reader.GetString(3) // value (kontrollera om det är NULL)
                ));
            }
        }
        return playedTiles;
    }
    
    // Add player, by player name. If player by that name don't exist in the database, a new player with that name is created, 
    // otherwise the existing player is updated by the clientid, should it have changed.
    async Task<Player> AddPlayer(string name, string clientId)
    {
        // check if player already exists
        await using var cmd = db.CreateCommand("SELECT * FROM players WHERE name = $1"); // check if player exists
        cmd.Parameters.AddWithValue(name);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var dbClientId = reader.GetString(1);
                if (clientId.Equals(dbClientId) == false)
                {
                    // if same name but different session, save new clientId to db
                    await using var cmd2 = db.CreateCommand("UPDATE players SET clientid = $1 WHERE id = $2");
                    cmd2.Parameters.AddWithValue(clientId);
                    cmd2.Parameters.AddWithValue(reader.GetInt32(0));
                    await cmd2.ExecuteNonQueryAsync(); // Perform update
                }
                return new Player(reader.GetInt32(0), reader.GetString(1), clientId);
            }
        }
        // if player did not exist we create a new player in the db
        await using var cmd3 = db.CreateCommand("INSERT INTO players (name, clientid) VALUES ($1, $2) RETURNING id");
        cmd3.Parameters.AddWithValue(name);
        cmd3.Parameters.AddWithValue(clientId);
        var result = await cmd3.ExecuteScalarAsync();
        if (result != null && int.TryParse(result.ToString(), out int lastInsertedId))
        {
            return new Player(lastInsertedId, name, clientId);
        }
        return null;
    }
    
    // Process incomming PlayTile from client
    async Task<bool> PlayTile(int tile, int player, int game, string value)
    {
        // Kontrollera om en tile redan är spelad
        await using var cmd1 = db.CreateCommand("SELECT EXISTS (SELECT 1 FROM moves WHERE tile = $1 AND game = $2)");
        cmd1.Parameters.AddWithValue(tile);
        cmd1.Parameters.AddWithValue(game);
        bool result = (bool)(await cmd1.ExecuteScalarAsync() ?? false);
        Console.WriteLine($"Player {player} played at {tile} in game {game} with result {result}");
        if (result)
        {
            return false; // Om tile redan är spelad, returnera false
        }

        // Infoga draget i databasen, inklusive value
        await using var cmd = db.CreateCommand("INSERT INTO moves (tile, player, game, value) VALUES ($1, $2, $3, $4)");
        cmd.Parameters.AddWithValue(tile);
        cmd.Parameters.AddWithValue(player);
        cmd.Parameters.AddWithValue(game);
        cmd.Parameters.AddWithValue(value); // Lägg till value här
        int rowsAffected = await cmd.ExecuteNonQueryAsync();

        Console.WriteLine($"Rows affected: {rowsAffected}"); // Logga antalet rader som påverkas
        return rowsAffected > 0; // Returnera true om insättningen lyckades
    }

    async Task<List<int>?> CheckWin(int game)
    {
        
        // Defining wins, using a list of Tuples with indices. A Tuple is a read only, fixed size, list-type structure.
        // The indices are a serialization of the tiles in our tictactoe game with the top left index being 0 and the bottom right being 8.
        // Serializing game boards like this is a common and practical solution. 
        var winningVectors = new List<Tuple<int, int, int>>
        {
            // Horizontal wins 
            Tuple.Create(0, 1, 2),
            Tuple.Create(3, 4, 5),
            Tuple.Create(6, 7, 8),
            
            // Vertical wins
            Tuple.Create(0, 3, 6),
            Tuple.Create(1, 4, 7),
            Tuple.Create(2, 5, 8),
            
            // Diagonal wins
            Tuple.Create(0, 4, 8),
            Tuple.Create(2, 4, 6)
        };
        
        // Get the tiles for each player
        var player1tiles = new List<int>();
        var player2tiles = new List<int>();
        int? player_1 = null;
        int? player_2 = null;
        await using var cmd = db.CreateCommand("SELECT moves.tile, moves.player, games.player_1, games.player_2 FROM moves, games WHERE moves.game = games.id AND games.id = $1");
        cmd.Parameters.AddWithValue(game);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var tile = reader.GetInt32(0);
                var player = reader.GetInt32(1);
                player_1 = reader.GetInt32(2);
                player_2 = reader.GetInt32(3);
                if (player == player_1)
                {
                    player1tiles.Add(tile);  
                }
                else
                {
                    player2tiles.Add(tile);
                }
            }
        }
        
        // Now lets see if a player has a win
        int? winningPlayer = null;
        foreach (var vector in winningVectors)
        {
            if (player1tiles.Contains(vector.Item1) && player1tiles.Contains(vector.Item2) &&
                player1tiles.Contains(vector.Item3))
            {
                winningPlayer = player_1;
            }else if (player2tiles.Contains(vector.Item1) && player2tiles.Contains(vector.Item2) &&
                      player2tiles.Contains(vector.Item3))
            {
                winningPlayer = player_2; // we are not reporting who won.. that ends here, but we should
            }
            if(winningPlayer is not null){
                Console.WriteLine($"Winning vector: {vector.Item1}, {vector.Item2}, {vector.Item3}");
                // if we have a match, return the winning vector as a confirmation of the win
                var winningVector = new List<int>();
                winningVector.Add(vector.Item1);
                winningVector.Add(vector.Item2);
                winningVector.Add(vector.Item3);
                return winningVector;
            }
        }
        // if we don't have a match, return null
        return null;
    }
    
}