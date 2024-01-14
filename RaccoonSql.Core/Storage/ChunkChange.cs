namespace RaccoonSql.Core.Storage;

public record struct ChunkChange
{
    public required IModel Model { get; init; }
    public required bool Add { get; init; }
    public required int Offset { get; init; }
}