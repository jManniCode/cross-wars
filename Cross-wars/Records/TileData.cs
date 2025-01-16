namespace CrossWars.Records;

public class TileData
{
    public int Tile { get; set; }           // The tile index
    public string? Value { get; set; }     // The submitted letter (can be null if not played)
    public int Player { get; set; }        // The player who played this tile
    public string ColorStatus { get; set; } // Either "green" or "red"
}
