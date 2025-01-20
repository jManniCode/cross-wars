using System.Xml;
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
        app.MapGet("/api/get-hints/{crossWordId}",getHints);  
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
        
        app.MapPost("/api/create-game/{gamecode}", async (string gamecode) =>
        {
            Console.WriteLine($"Received gamecode: {gamecode}");
            var success = await NewGame(gamecode);
            if (success)
            {
                Console.WriteLine($"Game created with code: {gamecode}");
                return Results.Json(new { gamecode });
            }
            Console.WriteLine("Failed to create game.");
            return Results.BadRequest("Failed to create game.");
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

private int? playerOneId = null; // ID för första spelaren
private int? playerTwoId = null; // ID för andra spelaren

async Task<Player> AddPlayer(string name, string clientId)
{
    // Kontrollera om spelaren redan finns
    await using var cmd = db.CreateCommand("SELECT id, clientid FROM players WHERE name = $1");
    cmd.Parameters.AddWithValue(name);
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
        if (await reader.ReadAsync())
        {
            var playerId = reader.GetInt32(0);
            var dbClientId = reader.GetString(1);

            // Uppdatera clientId om det behövs
            if (!clientId.Equals(dbClientId))
            {
                await using var updateCmd = db.CreateCommand("UPDATE players SET clientid = $1 WHERE id = $2");
                updateCmd.Parameters.AddWithValue(clientId);
                updateCmd.Parameters.AddWithValue(playerId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            // Spara spelar-ID till backend-variabler
            AssignPlayerId(name, playerId);

            return new Player(playerId, name, clientId);
        }
    }

    // Lägg till ny spelare om ingen hittades
    await using var insertCmd = db.CreateCommand("INSERT INTO players (name, clientid) VALUES ($1, $2) RETURNING id");
    insertCmd.Parameters.AddWithValue(name);
    insertCmd.Parameters.AddWithValue(clientId);
    var result = await insertCmd.ExecuteScalarAsync();

    if (result != null && int.TryParse(result.ToString(), out int lastInsertedId))
    {
        AssignPlayerId(name, lastInsertedId);
        return new Player(lastInsertedId, name, clientId);
    }

    throw new Exception("Failed to add player.");
}

// Metod för att tilldela spelar-ID
private void AssignPlayerId(string name, int playerId)
{
    
    
    if (playerOneId == null)
    {
        playerOneId = playerId;
    }
    else if (playerTwoId == null)
    {
        playerTwoId = playerId;
    }
}


    
private async Task<bool> NewGame(string gamecode)
{
    if (playerOneId == null || playerTwoId == null)
    {
        Console.WriteLine("Players not initialized for game creation.");
        return false;
    }

    try
    {
        Console.WriteLine($"Attempting to create game with gamecode: {gamecode}");
        await using var cmd = db.CreateCommand(
            "INSERT INTO games (gamecode, player_1, player_2) VALUES ($1, $2, $3) RETURNING id"
        );
        cmd.Parameters.AddWithValue(gamecode);
        cmd.Parameters.AddWithValue(playerOneId);
        cmd.Parameters.AddWithValue(playerTwoId);
        var result = await cmd.ExecuteScalarAsync();

        if (result != null)
        {
            Console.WriteLine($"Game created with ID: {result}");
            ResetPlayerIds(); // Återställ spelarna efter spel skapas
            return true;
        }

        Console.WriteLine("Failed to insert game into database.");
        return false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in NewGame: {ex.Message}");
        return false;
    }
}

// Återställ spelarvariabler
private void ResetPlayerIds()
{
    playerOneId = null;
    playerTwoId = null;
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



    private async Task<List<Hints>> getHints(int crossWordId)
    {
        var wordIds = new List<int>();
        var hintRowsPositions = new List<int>();  
        var hintColumnsPositions = new List<int>();
        var hintList = new List<Hints>(); 
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "select distinct word from cross_word_letter_placement where cross_word=$1";
        cmd.Parameters.AddWithValue(crossWordId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            wordIds.Add(reader.GetInt32(0));
        }

        foreach (var wordId in wordIds)
        {   await using var  cmd2 = db.CreateCommand();
            cmd2.CommandText = "select row, \"column\"  from cross_word_letter_placement where cross_word=$1 AND word=$2 order by row,\"column\" limit 2";
            cmd2.Parameters.AddWithValue(crossWordId);
            cmd2.Parameters.AddWithValue(wordId);
            await using var reader2 = await cmd2.ExecuteReaderAsync();
            int row = 0;
            int column = 0; 
            while (await reader2.ReadAsync())
            {
                row = Math.Abs(2 * row - reader2.GetInt32(0) );  //The query gathers the rows and column of the two first letters in the word
                column = Math.Abs(2 * column - reader2.GetInt32(1) ); 
                //Since the two letters are only diffrent by one step we can get the tile before
                //the word by (position of first letter 1)- (position of second letter - poisition of first letter )
                // <=> 2*(position of first letter)-(position of second letter)
                //By starting at zero and using absolut value we set row, column to the position of the first letter 
                // in the first iteration and in the second we execture the equation above to obtain the position of the hint
                
            }
            hintRowsPositions.Add(row);
            hintColumnsPositions.Add(column);
        }

        for (int i = 0; i < wordIds.Count; i++)
        { await using var  cmd3 = db.CreateCommand(); 
            cmd3.CommandText =" select hint from words where id = $1";
            cmd3.Parameters.AddWithValue(wordIds[i]);
            await using var reader3 = await cmd3.ExecuteReaderAsync();
            while (await reader3.ReadAsync())
            {
                hintList.Add(new Hints(hintRowsPositions[i], 
                    hintColumnsPositions[i], reader3.GetString(0)) );
                
            }
        }


        return hintList;
    }

}