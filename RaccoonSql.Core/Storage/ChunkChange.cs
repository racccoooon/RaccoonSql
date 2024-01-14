namespace RaccoonSql.Core.Storage;

public readonly record struct ChunkChange
{
    public required IModel Model { get; init; }
    public required bool Add { get; init; }
    public required uint Offset { get; init; }
}