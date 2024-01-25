using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using RaccoonSql.CoreRework.Internal.Persistence;

namespace RaccoonSql.CoreRework.Internal;

internal interface IModelCollection
{
    ReaderWriterLockSlim ReaderWriterLock { get; }

    void Apply(ChangeSet changeSet);

    void ValidateCheckConstraints(ChangeSet changeSet);
}

internal class ModelCollection<TModel> : IModelCollection
    where TModel : ModelBase
{
    private readonly ModelStoreOptions _options;
    private readonly string _name;

    private readonly ModelCollectionMetadata _metadata;

    public ReaderWriterLockSlim ReaderWriterLock { get; } = new();

    private readonly Dictionary<string, List<IModelValidator>> _propertyValidators = [];
    private readonly List<IModelValidator> _modelValidators = [];

    private ModelCollectionChunk<TModel>[] _collectionChunks = [];
    private int _modelCount;

    internal ModelCollection(ModelStoreOptions options)
    {
        _options = options;
        _name = typeof(TModel).Name;

        InitializeCheckConstraints();
        _metadata = LoadMetadata();
        LoadChunks();
    }

    private ModelCollectionMetadata LoadMetadata()
    {
        const int initialChunkCount = 16;
        var metadata = PersistenceEngine.Instance.ReadMetadata(
            _options.FileSystem,
            _options.DirectoryPath, 
            _name);
        
        // ReSharper disable once InvertIf
        if (metadata == null)
        {
            metadata = new ModelCollectionMetadata
            {
                ChunkCount = initialChunkCount,
            };
            
            PersistenceEngine.Instance.WriteMetadata(
                _options.FileSystem,
                _options.DirectoryPath,
                _name,
                metadata);
        }

        return metadata;
    }

    private void LoadChunks()
    {
        var chunkCount = _metadata.ChunkCount;

        _collectionChunks = new ModelCollectionChunk<TModel>[chunkCount];
        Parallel.ForEach(Enumerable.Range(0, _collectionChunks.Length), i =>
        {
            _collectionChunks[i] = PersistenceEngine.Instance.ReadChunk<TModel>(
                _options.FileSystem,
                _options.DirectoryPath,
                _name,
                i);
        });
        _modelCount = _collectionChunks.Sum(x => x.ModelCount);
    }

    private void InitializeCheckConstraints()
    {
        var modelType = typeof(TModel);

        foreach (var attribute in modelType.GetCustomAttributes<ModelCheckConstraintAttribute>())
        {
            _modelValidators.Add(attribute.GetValidator(modelType));
        }

        foreach (var propertyInfo in modelType.GetProperties())
        {
            foreach (var attribute in propertyInfo.GetCustomAttributes<PropertyCheckConstraintAttribute>())
            {
                if (!_propertyValidators.TryGetValue(propertyInfo.Name, out var propertyValidators))
                {
                    propertyValidators = _propertyValidators[propertyInfo.Name] = [];
                }

                propertyValidators.Add(attribute.GetValidator(propertyInfo));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateChunkIndex(Guid id)
    {
        return (int)(id.GetUint3() % _collectionChunks.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ModelCollectionChunk<TModel> GetChunk(Guid id)
    {
        return _collectionChunks[CalculateChunkIndex(id)];
    }

    internal TModel? Find(Guid id)
    {
        ReaderWriterLock.EnterReadLock();

        try
        {
            return GetChunk(id).Find(id);
        }
        finally
        {
            ReaderWriterLock.ExitReadLock();
        }
    }

    void IModelCollection.ValidateCheckConstraints(ChangeSet changeSet)
    {
        foreach (var addedModel in changeSet.Added)
        {
            ValidateCheckConstraints(addedModel);
        }

        foreach (var changedModel in changeSet.Changed)
        {
            ValidateCheckConstraints(changedModel);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateCheckConstraints(ModelBase addedModel)
    {
        foreach (var modelValidator in _modelValidators)
        {
            modelValidator.Check(addedModel);
        }

        foreach (var propertyValidators in _propertyValidators.Values)
        {
            foreach (var propertyValidator in propertyValidators)
            {
                propertyValidator.Check(addedModel);
            }
        }
    }

    private bool EnsureCapacity(int additionalModelCount)
    {
        const int averageChunkSize = 1024;
        const float thresholdPerChunk = 0.66f * averageChunkSize;

        if (_modelCount + additionalModelCount <= _collectionChunks.Length * thresholdPerChunk)
            return false;

        var newChunkCount = _collectionChunks.Length * 2;
        var newChunks = MakeNewChunks(newChunkCount);

        var oldChunks = _collectionChunks;
        _collectionChunks = newChunks;

        foreach (var oldChunk in oldChunks)
        {
            foreach (var model in oldChunk.Models)
            {
                GetChunk(model.Id).Add(model);
            }
        }

        _metadata.ChunkCount = newChunkCount;

        return true;
    }

    private static ModelCollectionChunk<TModel>[] MakeNewChunks(int newChunkCount)
    {
        var newChunks = new ModelCollectionChunk<TModel>[newChunkCount];

        for (var i = 0; i < newChunks.Length; i++)
        {
            newChunks[i] = new ModelCollectionChunk<TModel>();
        }

        return newChunks;
    }

    void IModelCollection.Apply(ChangeSet changeSet)
    {
        HashSet<int> modifiedChunks = [];
        
        Dictionary<int, LinkedList<TModel>> chunkedAdds = [];
        Dictionary<int, LinkedList<(TModel model, int index)>> chunkedUpdates = [];
        Dictionary<int, LinkedList<int>> chunkedDeletes = [];

        var isRehashed = false;

        foreach (var removedId in changeSet.Removed)
        {
            var chunkIndex = CalculateChunkIndex(removedId);
            var modelIndex = _collectionChunks[chunkIndex].Remove(removedId);
            modifiedChunks.Add(chunkIndex);
            GetChunkChanges(chunkedDeletes, chunkIndex).AddLast(modelIndex);
        }

        if (EnsureCapacity(changeSet.Added.Count))
        {
            _metadata.ChunkCount = _collectionChunks.Length;
            isRehashed = true;
        }

        // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
        foreach (TModel model in changeSet.Changed)
        {
            var chunkIndex = CalculateChunkIndex(model.Id);
            var chunk = _collectionChunks[chunkIndex];
            var modelIndex = chunk.ApplyChanges(model.Id, model.Changes);

            if (isRehashed) continue;
            
            modifiedChunks.Add(chunkIndex);
            Debug.Assert(chunk.ModelCount > modelIndex);
            GetChunkChanges(chunkedUpdates, chunkIndex).AddLast((model, modelIndex));
        }

        // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
        foreach (TModel model in changeSet.Added)
        {
            var chunkIndex = CalculateChunkIndex(model.Id);
            _collectionChunks[chunkIndex].Add(model);

            if (isRehashed) continue;
            
            modifiedChunks.Add(chunkIndex);
            GetChunkChanges(chunkedAdds, chunkIndex).AddLast(model);
        }

        _modelCount += changeSet.Added.Count;

        if (isRehashed)
        {
            PersistenceEngine.Instance.WriteMetadata(
                _options.FileSystem,
                _options.DirectoryPath,
                _name,
                _metadata);
            
            for (var i = 0; i < _collectionChunks.Length; i++)
            {
                modifiedChunks.Add(i);
            }
        }

        foreach (var modifiedChunk in modifiedChunks)
        {
            var chunk = _collectionChunks[modifiedChunk];
            if(isRehashed || chunk.OperationCount > 1000)
            {
                PersistenceEngine.Instance.WriteChunk(
                    _options.FileSystem,
                    _options.DirectoryPath,
                    _name,
                    modifiedChunk,
                    chunk);
                
                chunk.OperationCount = 0;
            }
            else
            {
                PersistenceEngine.Instance.WriteChunkChanges(
                    _options.FileSystem,
                    _options.DirectoryPath,
                    _name,
                    modifiedChunk,
                    GetChunkChanges(chunkedAdds, modifiedChunk),
                    GetChunkChanges(chunkedUpdates, modifiedChunk),
                    GetChunkChanges(chunkedDeletes, modifiedChunk));
            }
        }
        
        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReSharper disable once SuggestBaseTypeForParameter
        LinkedList<T> GetChunkChanges<T>(Dictionary<int, LinkedList<T>> dictionary, int chunkIndex)
        {
            if (!dictionary.TryGetValue(chunkIndex, out var result))
            {
                result = dictionary[chunkIndex] = [];
            }

            return result;
        }
    }
}