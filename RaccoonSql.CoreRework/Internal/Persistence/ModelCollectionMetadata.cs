using MemoryPack;

namespace RaccoonSql.CoreRework.Internal.Persistence; 

[MemoryPackable]
public partial class ModelCollectionMetadata
{
    public int ChunkCount { get; set; }
}