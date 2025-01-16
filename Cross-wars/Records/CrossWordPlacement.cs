namespace CrossWars.Records;

public record CrossWordPlacement
{
    public int Word { get; init; }
    public string Letter { get; init; }
    public int Row { get; init; }
    public int Column { get; init; }
}