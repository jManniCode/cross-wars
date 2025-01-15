using Npgsql;
using CrossWars.Records;

namespace CrossWars;

public class CrossWarsActions
{
    Database database = new();
    private NpgsqlDataSource db;
    
    public CrossWarsActions (WebApplication app)
    {
        db = database.Connection();
        
        app.MapGet("/api/current-game/{gamecode}", GetCurrentGame);
        app.MapGet("/api/played-tiles/{id}", GetPlayedTiles);
        app.MapPost("/api/validate-move", async (HttpContext context) =>
        {
            var requestBody = await context.Request.ReadFromJsonAsync<Move>();
            if (requestBody?.tile is null || requestBody?.value is null || requestBody?.game is null)
            {
                return Results.BadRequest("tile, value, and game are required.");
            }

            bool isValid = await ValidateMove(requestBody.tile, requestBody.value, requestBody.game);
            return Results.Ok(isValid);
        });
        
        app.MapGet("/api/played-tiles-status/{gameId}", async (int gameId) =>
        {
            var playedTilesWithStatus = await GetPlayedTilesWithStatus(gameId);
            return Results.Ok(playedTilesWithStatus);
        });
        
        app.MapGet("/api/cross-word-placements", async (HttpContext context) =>
        {
            var placements = await GetCrossWordPlacements();
            return Results.Ok(placements);
        });
        
        app.MapGet("/api/check-win/{game}", CheckWin);
        app.MapPost("/api/add-player", async (HttpContext context) =>
        {
            var requestBody = await context.Request.ReadFromJsonAsync<Player>();
            if (requestBody?.name is null)
            {
                return Results.BadRequest("name is required.");
            }
            var player = await AddPlayer(requestBody.name, context.Request.Cookies["ClientId"]);
            return player.id > 0 ? Results.Ok(player) : Results.StatusCode(500);
        });
        
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
        
        app.MapGet("/api/game-scores/{gameId}", async (int gameId) =>
        {
            await using var cmd = db.CreateCommand(
                "SELECT player_1_score, player_2_score FROM games WHERE id = $1");
            cmd.Parameters.AddWithValue(gameId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Results.Ok(new
                {
                    Player1Score = reader.GetInt32(0),
                    Player2Score = reader.GetInt32(1)
                });
            }

            return Results.NotFound();
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
                    reader.IsDBNull(3) ? null : reader.GetString(3) // value
                ));
            }
        }
        return playedTiles;
    }
    async Task<Player> AddPlayer(string name, string clientId)
    {
        // check if player already exists
        await using var cmd = db.CreateCommand("SELECT * FROM players WHERE name = $1");
        cmd.Parameters.AddWithValue(name);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var dbClientId = reader.GetString(1);
                if (clientId.Equals(dbClientId) == false)
                {
                    await using var cmd2 = db.CreateCommand("UPDATE players SET clientid = $1 WHERE id = $2");
                    cmd2.Parameters.AddWithValue(clientId);
                    cmd2.Parameters.AddWithValue(reader.GetInt32(0));
                    await cmd2.ExecuteNonQueryAsync();
                }
                return new Player(reader.GetInt32(0), reader.GetString(1), clientId);
            }
        }
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
    
    async Task<bool> PlayTile(int tile, int player, int game, string value)
{
    // Konvertera tile till rad och kolumn
    int row = tile / 10;
    int column = tile % 10;

    // Kontrollera om bokstaven är korrekt
    await using var validationCmd = db.CreateCommand(
        @"SELECT letter 
          FROM cross_word_letter_placement 
          WHERE row = $1 AND ""column"" = $2");
    validationCmd.Parameters.AddWithValue(row);
    validationCmd.Parameters.AddWithValue(column);

    string correctLetter = null;
    await using (var reader = await validationCmd.ExecuteReaderAsync())
    {
        if (await reader.ReadAsync())
        {
            correctLetter = reader.GetString(0);
        }
    }

    if (correctLetter == null)
    {
        Console.WriteLine($"No letter found at tile {tile}.");
        return false;
    }

    bool isCorrect = string.Equals(correctLetter, value, StringComparison.OrdinalIgnoreCase);

    // Spara draget i `moves`-tabellen
    await using var moveCmd = db.CreateCommand(
        "INSERT INTO moves (tile, player, game, value) VALUES ($1, $2, $3, $4)");
    moveCmd.Parameters.AddWithValue(tile);
    moveCmd.Parameters.AddWithValue(player);
    moveCmd.Parameters.AddWithValue(game);
    moveCmd.Parameters.AddWithValue(value);
    await moveCmd.ExecuteNonQueryAsync();

    // Uppdatera poäng om bokstaven är korrekt
    if (isCorrect)
    {
        string scoreColumn = player == GetPlayer1Id(game) ? "player_1_score" : "player_2_score";
        await using var scoreCmd = db.CreateCommand(
            $@"UPDATE games 
               SET {scoreColumn} = {scoreColumn} + 10 
               WHERE id = $1");
        scoreCmd.Parameters.AddWithValue(game);
        await scoreCmd.ExecuteNonQueryAsync();

        Console.WriteLine($"{player} scored 10 points");
    }

    return true;
}

int GetPlayer1Id(int gameId)
{
    // Hämtar spelare 1:s ID från tabellen games
    using var cmd = db.CreateCommand("SELECT player_1 FROM games WHERE id = $1");
    cmd.Parameters.AddWithValue(gameId);
    return (int)cmd.ExecuteScalar()!;
}
    
    
    async Task<bool> ValidateMove(int tile, string value, int gameId)
    {
        int row = tile / 10;
        int column = tile % 10;
        
        await using var cmd = db.CreateCommand(
            @"SELECT letter 
      FROM cross_word_letter_placement 
      WHERE row = $1 AND ""column"" = $2"
        );
        cmd.Parameters.AddWithValue(row);
        cmd.Parameters.AddWithValue(column);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var correctLetter = reader.GetString(0);
            return string.Equals(correctLetter, value, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
    
    async Task<List<dynamic>> GetPlayedTilesWithStatus(int gameId)
    {
        var playedTilesWithStatus = new List<dynamic>();

        await using var cmd = db.CreateCommand(
            @"SELECT m.tile, m.value, cwl.letter, 
                 CASE 
                   WHEN m.value = cwl.letter THEN 'correct'
                   ELSE 'incorrect'
                 END AS status
          FROM moves m
          LEFT JOIN cross_word_letter_placement cwl
          ON m.tile = (cwl.row * 10 + cwl.""column"")
          WHERE m.game = $1"
        );
        cmd.Parameters.AddWithValue(gameId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            playedTilesWithStatus.Add(new
            {
                Tile = reader.GetInt32(0),
                Value = reader.GetString(1),
                Status = reader.GetString(3)
            });
        }
        return playedTilesWithStatus;
    }
    
    async Task<List<int>?> CheckWin(int game)
    {
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
    private async Task<List<CrossWordPlacement>> GetCrossWordPlacements()
    {
        var placements = new List<CrossWordPlacement>();
        await using var cmd = db.CreateCommand("SELECT word, letter, row, \"column\" FROM cross_word_letter_placement");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            placements.Add(new CrossWordPlacement
            {
                Word = reader.GetInt32(0),
                Letter = reader.GetString(1),
                Row = reader.GetInt32(2),
                Column = reader.GetInt32(3)
            });
        }
        return placements;
    }
}