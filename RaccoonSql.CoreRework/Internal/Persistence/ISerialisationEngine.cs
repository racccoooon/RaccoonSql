using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MemoryPack;
using RaccoonSql.CoreRework.Internal.Utils;
using RaccoonSql.CoreRework.Serializer;

namespace RaccoonSql.CoreRework.Internal.Persistence;

internal class ChunkData<TModel> where TModel : ModelBase
{
    public required Dictionary<Guid, int> ModelIndexes { get; init; }
    public required List<TModel> Models { get; init; }
}

internal interface ISerialisationEngine
{
    ModelStoreMetadata DeserializeStoreMetadata(Stream stream);
    ModelCollectionMetadata DeserializeCollectionMetadata(Stream stream);
    ChunkData<TModel> DeserializeChunkData<TModel>(Stream stream) where TModel : ModelBase;
    TModel DeserializeModel<TModel>(Stream stream) where TModel : ModelBase;
    bool TryDeserializeWal(
        Stream stream, 
        Dictionary<string, Type> modelTypes,
        ref byte[] lengthBuffer, 
        ref byte[] commitBuffer, 
        [NotNullWhen(true)] out CommitChanges? commitChanges);

    void SerializeStoreMetadata(Stream stream, ModelStoreMetadata metadata);
    void SerializeCollectionMetadata(Stream stream, ModelCollectionMetadata metadata);
    void SerializeChunkData<TModel>(Stream stream, ChunkData<TModel> getData) where TModel : ModelBase;
    void SerializeModel<TModel>(Stream stream, TModel model) where TModel : ModelBase;
    void SerializeWal(Stream stream, CommitChanges commitChanges);
}

internal class SerialisationEngine : ISerialisationEngine
{
    public static readonly ISerialisationEngine Instance = new SerialisationEngine();

    public ModelStoreMetadata DeserializeStoreMetadata(Stream stream)
    {
        return MemoryPackSerializer.Deserialize<ModelStoreMetadata>(stream.ToArray())!;
    }

    public ModelCollectionMetadata DeserializeCollectionMetadata(Stream stream)
    {
        return MemoryPackSerializer.Deserialize<ModelCollectionMetadata>(stream.ToArray())!;
    }

    public ChunkData<TModel> DeserializeChunkData<TModel>(Stream stream) where TModel : ModelBase
    {
        return RaccSerializer.Deserialize<ChunkData<TModel>>(stream);
    }

    public TModel DeserializeModel<TModel>(Stream stream) where TModel : ModelBase
    {
        return RaccSerializer.Deserialize<TModel>(stream);
    }

    public void SerializeStoreMetadata(Stream stream, ModelStoreMetadata metadata)
    {
        var serialized = MemoryPackSerializer.Serialize(metadata);
        stream.Write(serialized, 0, serialized.Length);
    }

    public void SerializeCollectionMetadata(Stream stream, ModelCollectionMetadata metadata)
    {
        var serialized = MemoryPackSerializer.Serialize(metadata);
        stream.Write(serialized, 0, serialized.Length);
    }

    public void SerializeChunkData<TModel>(Stream stream, ChunkData<TModel> getData) where TModel : ModelBase
    {
        RaccSerializer.Serialize(stream, getData);
    }

    public void SerializeModel<TModel>(Stream stream, TModel model) where TModel : ModelBase
    {
        RaccSerializer.Serialize(stream, model);
    }

    public void SerializeWal(Stream stream, CommitChanges commitChanges)
    {
        RaccSerializer.Serialize(stream, commitChanges.Version);
        RaccSerializer.Serialize(stream, commitChanges.Changes.Count);
        foreach (var changeSet in commitChanges.Changes)
        {
            RaccSerializer.Serialize(stream, changeSet.ModelName);
            var listSerializer = RaccSerializer.GetListSerializer(changeSet.ModelType, typeof(ModelBase));
            listSerializer.Serialize(stream, changeSet.Added);
            RaccSerializer.Serialize(stream, changeSet.Removed);
            var changesSerializer = RaccSerializer.GetTypedDictionarySerializer(changeSet.ModelType);
            var guidSerializer = RaccSerializer.GetValueSerializer<Guid>();
            var intSerializer = RaccSerializer.GetValueSerializer<int>();
            intSerializer.Serialize(stream, changeSet.Changed.Count);
            foreach (var (id, changes) in changeSet.Changed)
            {
                guidSerializer.Serialize(stream, id);
                changesSerializer.Serialize(stream, changes);
            }
        }
        stream.Flush();
    }

    public bool TryDeserializeWal(
        Stream stream, 
        Dictionary<string, Type> modelTypes,
        ref byte[] lengthBuffer, 
        ref byte[] commitBuffer, 
        [NotNullWhen(true)] out CommitChanges? commitChanges)
    {
        try
        {
            var version = RaccSerializer.Deserialize<ulong>(stream);
            var changeSetCount = RaccSerializer.Deserialize<int>(stream);
            var changes = new List<ChangeSet>(changeSetCount);
            
            for (var i = 0; i < changeSetCount; i++)
            {
                var changeSetModelName = RaccSerializer.Deserialize<string>(stream);
                var modelType = modelTypes[changeSetModelName];
                var listSerializer = RaccSerializer.GetListSerializer(modelType, typeof(ModelBase));
                var added = listSerializer.Deserialize(stream);
                var removed = RaccSerializer.Deserialize<HashSet<Guid>>(stream);
                var changesSerializer = RaccSerializer.GetTypedDictionarySerializer(modelType);
                var guidSerializer = RaccSerializer.GetValueSerializer<Guid>();
                var intSerializer = RaccSerializer.GetValueSerializer<int>();
                var changeCount = intSerializer.Deserialize(stream);
                List<(Guid, Dictionary<PropertyInfo, object?>)> changed = new(changeCount);
                for (var j = 0; j < changeCount; j++)
                {
                    var id = guidSerializer.Deserialize(stream);
                    var dict = (Dictionary<PropertyInfo, object?>)changesSerializer.Deserialize(stream);
                    changed.Add((id, dict));
                }
                
                changes.Add(new ChangeSet(modelType, (List<ModelBase>)added, removed, changed));
            }

            commitChanges = new CommitChanges
            {
                Changes = changes,
                Version = version,
            };
            return true;

        }
        catch (EndOfStreamException)
        {
            commitChanges = null;
            return false;
        }
    }
}