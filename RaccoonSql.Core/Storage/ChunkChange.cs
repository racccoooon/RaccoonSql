namespace RaccoonSql.Core.Storage;

public readonly struct ChunkChange
{
    public required ModelBase ModelBase { get; init; }
    public required bool Add { get; init; }
    public required uint Offset { get; init; }
}