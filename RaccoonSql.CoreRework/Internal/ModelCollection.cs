using System.Reflection;
using System.Runtime.CompilerServices;

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
    public ReaderWriterLockSlim ReaderWriterLock { get; } = new();

    private readonly Dictionary<string, List<IModelValidator>> _propertyValidators = [];
    private readonly List<IModelValidator> _modelValidators = [];

    private ModelCollectionChunk<TModel>[] _collectionChunks = [];
    private int _modelCount = 0;

    internal ModelCollection()
    {
        InitializeCheckConstraints();
        LoadChunks();
    }

    private void LoadChunks()
    {
        _collectionChunks = new ModelCollectionChunk<TModel>[16];
        for (var i = 0; i < _collectionChunks.Length; i++)
        {
            _collectionChunks[i] = new ModelCollectionChunk<TModel>();
        }
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

    private void EnsureCapacity(int additionalModelCount)
    {
        const int averageChunkSize = 1024;
        const float thresholdPerChunk = 0.66f * averageChunkSize;

        if (_modelCount + additionalModelCount <= _collectionChunks.Length * thresholdPerChunk) 
            return;

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
    }

    private static ModelCollectionChunk<TModel>[] MakeNewChunks(int newChunkCount)
    {
        var newChunks =new ModelCollectionChunk<TModel>[newChunkCount];

        for (var i = 0; i < newChunks.Length; i++)
        {
            newChunks[i] = new ModelCollectionChunk<TModel>();
        }

        return newChunks;
    }

    void IModelCollection.Apply(ChangeSet changeSet)
    {
        foreach (var id in changeSet.Removed)
        {
            GetChunk(id).Remove(id);
        }

        foreach (var model in changeSet.Changed)
        {
            GetChunk(model.Id).ApplyChanges(model.Id, model.Changes);
        }

        EnsureCapacity(changeSet.Added.Count);
        
        // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
        foreach (TModel model in changeSet.Added)
        {
            GetChunk(model.Id).Add(model);
        }

        _modelCount += changeSet.Added.Count;
    }
}