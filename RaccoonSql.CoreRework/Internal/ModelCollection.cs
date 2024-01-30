using System.Reflection;
using System.Runtime.CompilerServices;
using RaccoonSql.CoreRework.Internal.Persistence;
using RaccoonSql.CoreRework.Internal.Utils;

namespace RaccoonSql.CoreRework.Internal;

internal interface IModelCollection
{
    ReaderWriterLockSlim ReaderWriterLock { get; }

    void Apply(ChangeSet changeSet);

    void ValidateCheckConstraints(ChangeSet changeSet);
    
    void Persist();
}

internal class ModelCollection<TModel> : IModelCollection
    where TModel : ModelBase
{
    private readonly ModelStoreOptions _options;
    private readonly string _name;

    private bool _isDirty;

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
        var metadata = PersistenceEngine.Instance.ReadCollectionMetadata(
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
            
            PersistenceEngine.Instance.WriteCollectionMetadata(
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

    public void Persist()
    {
        if (!_isDirty) return;

        for (var index = 0; index < _collectionChunks.Length; index++)
        {
            var chunk = _collectionChunks[index];
            chunk.Persist(_options.FileSystem, _options.DirectoryPath, _name, index);
        }

        _isDirty = false;
    }

    void IModelCollection.ValidateCheckConstraints(ChangeSet changeSet)
    {
        //TODO: reimplement later
        // foreach (var addedModel in changeSet.Added)
        // {
        //     ValidateCheckConstraints(addedModel);
        // }
        //
        // foreach (var changedModel in changeSet.Changed)
        // {
        //     ValidateCheckConstraints(id, changes);
        // }
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
        foreach (var removedId in changeSet.Removed)
        {
            var chunkIndex = CalculateChunkIndex(removedId);
            _collectionChunks[chunkIndex].Remove(removedId);
        }

        if (EnsureCapacity(changeSet.Added.Count))
        {
            _metadata.ChunkCount = _collectionChunks.Length;
            
            PersistenceEngine.Instance.WriteCollectionMetadata(
                _options.FileSystem,
                _options.DirectoryPath,
                _name,
                _metadata);
        }

        // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
        foreach (var (id, changes) in changeSet.Changed)
        {
            var chunkIndex = CalculateChunkIndex(id);
            var chunk = _collectionChunks[chunkIndex];
            chunk.ApplyChanges(id, changes);
        }

        // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
        foreach (TModel model in changeSet.Added)
        {
            var chunkIndex = CalculateChunkIndex(model.Id);
            _collectionChunks[chunkIndex].Add(model);
        }

        _modelCount += changeSet.Added.Count - changeSet.Removed.Count;
        _isDirty = true;
    }
}