using MemoryPack;

namespace RaccoonSql.CoreRework.Internal; 

[MemoryPackable]
public partial class ModelCollectionMetadata
{
    public int ChunkCount { get; set; }
}