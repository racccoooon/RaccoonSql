using System.IO.Abstractions;
using System.Text.Json;
using MemoryPack;
using RaccoonSql.CoreRework.Internal.Utils;

namespace RaccoonSql.CoreRework.Internal.Persistence;

internal class ChunkData<TModel> where TModel : ModelBase
{
    public required Dictionary<Guid, int> ModelIndexes { get; set; }
    public required List<TModel> Models { get; set; }
}

internal interface ISerialisationEngine
{
    ModelCollectionMetadata DeserializeMetadata(Stream stream);
    ChunkData<TModel> DeserializeChunkData<TModel>(Stream stream) where TModel : ModelBase;
    TModel DeserializeModel<TModel>(Stream stream) where TModel : ModelBase;

    void SerializeMetadata(Stream stream, ModelCollectionMetadata metadata);
    void SerializeChunkData<TModel>(Stream stream, ChunkData<TModel> getData) where TModel : ModelBase;
    void SerializeModel<TModel>(Stream stream, TModel model) where TModel : ModelBase;
}

internal class SerialisationEngine : ISerialisationEngine
{
    public static readonly ISerialisationEngine Instance = new SerialisationEngine();
    
    public ModelCollectionMetadata DeserializeMetadata(Stream stream)
    {
        return MemoryPackSerializer.Deserialize<ModelCollectionMetadata>(stream.ToArray())!;
    }

    public ChunkData<TModel> DeserializeChunkData<TModel>(Stream stream) where TModel : ModelBase
    {
        return (ChunkData<TModel>)JsonSerializer.Deserialize(stream, typeof(ChunkData<TModel>))!;
    }

    public TModel DeserializeModel<TModel>(Stream stream) where TModel : ModelBase
    {
        return (TModel)JsonSerializer.Deserialize(stream, typeof(TModel))!;
    }

    public void SerializeMetadata(Stream stream, ModelCollectionMetadata metadata)
    {
        var serialized = MemoryPackSerializer.Serialize(metadata);
        stream.Write(serialized, 0, serialized.Length);
    }

    public void SerializeChunkData<TModel>(Stream stream, ChunkData<TModel> getData) where TModel : ModelBase
    {
        var utf8JsonWriter = new Utf8JsonWriter(stream);
        JsonSerializer.Serialize(utf8JsonWriter, getData);
    }

    public void SerializeModel<TModel>(Stream stream, TModel model) where TModel : ModelBase
    {
        var utf8JsonWriter = new Utf8JsonWriter(stream);
        JsonSerializer.Serialize(utf8JsonWriter, model);
    }
}