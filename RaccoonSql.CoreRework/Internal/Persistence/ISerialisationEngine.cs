using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using MemoryPack;
using RaccoonSql.CoreRework.Internal.Utils;

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
        return (ChunkData<TModel>)JsonSerializer.Deserialize(stream, typeof(ChunkData<TModel>))!;
    }

    public TModel DeserializeModel<TModel>(Stream stream) where TModel : ModelBase
    {
        return (TModel)JsonSerializer.Deserialize(stream, typeof(TModel))!;
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
        var utf8JsonWriter = new Utf8JsonWriter(stream);
        JsonSerializer.Serialize(utf8JsonWriter, getData);
    }

    public void SerializeModel<TModel>(Stream stream, TModel model) where TModel : ModelBase
    {
        var utf8JsonWriter = new Utf8JsonWriter(stream);
        JsonSerializer.Serialize(utf8JsonWriter, model);
    }

    public void SerializeWal(Stream stream, CommitChanges commitChanges)
    {
        var commitJson = new JsonObject();
        commitJson.Add(nameof(CommitChanges.Version), commitChanges.Version);

        var changeSets = new JsonArray();
        foreach (var changeSet in commitChanges.Changes)
        {
            var changeSetJson = new JsonObject();
            
            changeSetJson.Add(nameof(ChangeSet.ModelName), changeSet.ModelName);

            var added = new JsonArray();        
            var removed = new JsonArray();
            var changed = new JsonArray();
            
            foreach (var model in changeSet.Added)
            {
                var modelJson = JsonSerializer.SerializeToNode(model, changeSet.ModelType)!;
                added.Add(modelJson);
            }
            
            foreach (var guid in changeSet.Removed)
            {
                removed.Add(guid);
            }
            
            foreach (var model in changeSet.Changed)
            {
                var modelJson = JsonSerializer.SerializeToNode(model, changeSet.ModelType)!.AsObject();
                var changes = JsonSerializer.SerializeToNode(model.Changes);
                modelJson.Add(nameof(ModelBase.Changes), changes);
                changed.Add(modelJson);
            }
            
            changeSetJson.Add(nameof(ChangeSet.Added), added);
            changeSetJson.Add(nameof(ChangeSet.Removed), removed);
            changeSetJson.Add(nameof(ChangeSet.Changed), changed);
            
            changeSets.Add(changeSetJson);
        }
        
        commitJson.Add(nameof(CommitChanges.Changes), changeSets);

        const int dummyLength = 0;
        stream.Write(BitConverter.GetBytes(dummyLength));
        stream.Flush();

        var startPos = stream.Position;
        using var utf8JsonWriter = new Utf8JsonWriter(stream);
        commitJson.WriteTo(utf8JsonWriter);
        utf8JsonWriter.Flush();
        
        var actualLength = stream.Position - startPos;
        stream.Seek(-(actualLength + sizeof(int)), SeekOrigin.Current);
        stream.Write(BitConverter.GetBytes((int)actualLength));
        stream.Seek(0, SeekOrigin.End);
        stream.Flush();
    }

    private static Dictionary<Type, Dictionary<string, Type>> propertyTypes = [];
    
    public bool TryDeserializeWal(
        Stream stream, 
        Dictionary<string, Type> modelTypes,
        ref byte[] lengthBuffer, 
        ref byte[] commitBuffer, 
        [NotNullWhen(true)] out CommitChanges? commitChanges)
    {
        if (lengthBuffer.Length != sizeof(int))
        {
            lengthBuffer = new byte[sizeof(int)];
        }
        
        try
        {
            stream.ReadExactly(lengthBuffer);
        }
        catch (EndOfStreamException)
        {        
            commitChanges = null;
            return false;
        }

        var length = BitConverter.ToInt32(lengthBuffer);
        if (commitBuffer.Length < length)
        {
            commitBuffer = new byte[length + length / 2];
        }
        
        stream.ReadExactly(commitBuffer, 0, length);

        var commitJson = JsonSerializer.Deserialize<JsonObject>(new ReadOnlySpan<byte>(commitBuffer, 0, length));
        if (commitJson is null)
            throw new UnreachableException();

        if (!commitJson.TryGetPropertyValue(nameof(CommitChanges.Version), out var versionJson))
            throw new UnreachableException();

        if (!commitJson.TryGetPropertyValue(nameof(CommitChanges.Changes), out var changesJson))
            throw new UnreachableException();

        List<ChangeSet> changes = [];
        foreach (var jsonNode in changesJson!.AsArray())
        {
            var jsonObject = jsonNode!.AsObject();
            if (!jsonObject.TryGetPropertyValue(nameof(ChangeSet.ModelName), out var modelName))
                throw new UnreachableException();

            var modelType = modelTypes[modelName!.GetValue<string>()];

            List<ModelBase> added = [];
            HashSet<Guid> removed = [];
            List<ModelBase> changed = [];

            if (!jsonObject.TryGetPropertyValue(nameof(ChangeSet.Added), out var addedJson))
                throw new UnreachableException();
            
            foreach (var node in addedJson!.AsArray())
            {
                var model = (ModelBase)node.Deserialize(modelType)!;
                
                if (!node!.AsObject().TryGetPropertyValue(nameof(ModelBase.Id), out var id))
                    throw new UnreachableException();

                model.Id = id!.GetValue<Guid>();
                added.Add(model);
            }
            
            if (!jsonObject.TryGetPropertyValue(nameof(ChangeSet.Removed), out var removedJson))
                throw new UnreachableException();
            
            foreach (var node in removedJson!.AsArray())
            {
                removed.Add(node!.GetValue<Guid>());
            }

            if (!jsonObject.TryGetPropertyValue(nameof(ChangeSet.Changed), out var changedJson))
                throw new UnreachableException();
            
            foreach (var node in changedJson!.AsArray())
            {
                var model = (ModelBase)node.Deserialize(modelType)!;
                
                if (!node!.AsObject().TryGetPropertyValue(nameof(ModelBase.Id), out var id))
                    throw new UnreachableException();

                model.Id = id!.GetValue<Guid>();
                
                if (!node.AsObject().TryGetPropertyValue(nameof(ModelBase.Changes), out var changeDictJson))
                    throw new UnreachableException();

                Dictionary<string, object?> changeDict = [];

                if (!propertyTypes.TryGetValue(modelType, out var properties))
                {
                    properties = [];

                    foreach (var propertyInfo in modelType.GetProperties())
                    {
                        properties[propertyInfo.Name] = propertyInfo.PropertyType;
                    }
                }
                
                foreach (var (key, value) in changeDictJson!.AsObject())
                {
                    changeDict[key] = value.Deserialize(properties[key]);
                }

                model.Changes = changeDict;
                
                changed.Add(model);
            }

            var change = new ChangeSet(modelType, added, removed, changed);
            
            changes.Add(change);
        }
        commitChanges = new CommitChanges
        {
            Changes = changes,
            Version = versionJson!.GetValue<ulong>(),
        };

        return true;
    }
}