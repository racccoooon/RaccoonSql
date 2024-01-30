using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using RaccoonSql.CoreRework.Internal;
using RaccoonSql.CoreRework.Internal.Persistence;

namespace RaccoonSql.CoreRework;

/// <summary>
/// The options for setting up the <see cref="IModelStore"/>.
/// </summary>
public record ModelStoreOptions
{
    public required string DirectoryPath { get; init; }
    public required IFileSystem FileSystem { get; init; }
    private readonly Dictionary<string, Type> _modelTypes = [];
    internal Dictionary<string, Type> ModelTypes => _modelTypes;

    public ModelStoreOptions RegisterType<TModel>()
        where TModel : ModelBase
    {
        _modelTypes[typeof(TModel).FullName!] = typeof(TModel);
        return this;
    }
}

/// <summary>
/// The central access point for the database.
/// Allows access to the database via transactions.
/// </summary>
[PublicAPI]
public interface IModelStore
{
    /// <summary>
    /// Creates an <see cref="ITransaction"/> with the provided <see cref="TransactionOptions"/>.
    /// </summary>
    /// <param name="transactionOptions">The options for the <see cref="ITransaction"/>.</param>
    /// <returns>A new <see cref="ITransaction>"/>.</returns>
    ITransaction Transaction(TransactionOptions transactionOptions = default);
}

/// <inheritdoc cref="IModelStore"/>
public sealed class ModelStore : IModelStore, IDisposable
{
    private readonly ConcurrentDictionary<Type, IModelCollection> _collections = [];
    private readonly ModelStoreOptions _options;

    private readonly ModelStoreMetadata _metadata;
    private readonly ReaderWriterLockSlim _dbLock = new();
    private readonly object _walLock = new();
    
    private ulong _versionId;
    private int _pendingTransactionCount;
    
    /// <inheritdoc cref="IModelStore"/>
    /// <param name="options">The options for the database.</param>
    public ModelStore(ModelStoreOptions options)
    {
        _options = options;
        _metadata = LoadMetadata();
        _versionId = _metadata.Version;

        ApplyWal();
        Persist();
    }

    private void ApplyWal()
    {
        var missingCommits = PersistenceEngine.Instance.ReadWal(
                _options.FileSystem,
                _options.DirectoryPath,
                _options.ModelTypes)
            .Where(x => x.Version > _metadata.Version)
            .ToList();

        if (missingCommits.Count == 0) return;
        
        //TODO: load snapshot
        
        foreach (var missingCommit in missingCommits)
        {
            ApplyCommit(missingCommit);
        }
    }

    private ModelStoreMetadata LoadMetadata()
    {
        const long initialVersion = 0;
        var metadata = PersistenceEngine.Instance.ReadStoreMetadata(_options.FileSystem, _options.DirectoryPath);
        
        // ReSharper disable once InvertIf
        if (metadata is null)
        {
            metadata = new ModelStoreMetadata
            {
                Version = initialVersion,
            };
            
            PersistenceEngine.Instance.WriteStoreMetadata(_options.FileSystem, _options.DirectoryPath, metadata);
        }

        return metadata;
    }

    public ITransaction Transaction(TransactionOptions transactionOptions = default)
    {
        return new Transaction(this, transactionOptions);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ModelCollection<TModel> GetCollection<TModel>()
        where TModel : ModelBase
    {
        return (ModelCollection<TModel>) GetCollection(typeof(TModel));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IModelCollection GetCollection(Type modelType)
    {
        return _collections.GetOrAdd(
            modelType,
            // ReSharper disable once SuspiciousTypeConversion.Global
            _ => (IModelCollection)typeof(ModelCollection<>)
                .MakeGenericType(modelType)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0]
                .Invoke([_options]));
    }

    internal void Commit(List<ChangeSet> changes)
    {
        ValidateCheckConstraints(changes);
        
        changes.Sort((a, b) 
            => string.Compare(a.ModelType.FullName, b.ModelType.FullName, StringComparison.Ordinal));

        AcquireCommitLock(changes);

        try
        {
            CommitChanges commit;
            lock (_walLock)
            {
                commit = new CommitChanges
                {
                    Version = _versionId++,
                    Changes = changes,
                };
                _pendingTransactionCount++;
                
                PersistenceEngine.Instance.WriteWal(
                    _options.FileSystem,
                    _options.DirectoryPath,
                    commit);
            }

            ApplyCommit(commit);
        }
        finally
        {
            ReleaseCommitLock(changes);
        }

        if (_pendingTransactionCount > 8)
        {
            Persist();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateCheckConstraints(List<ChangeSet> commit)
    {
        foreach (var changeSet in commit)
        {
            GetCollection(changeSet.ModelType).ValidateCheckConstraints(changeSet);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AcquireCommitLock(List<ChangeSet> commit)
    {
        _dbLock.EnterReadLock();
        foreach (var changeSet in commit)
        {
            GetCollection(changeSet.ModelType).ReaderWriterLock.EnterWriteLock();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyCommit(CommitChanges commit)
    {
        foreach (var changeSet in commit.Changes)
        {
            GetCollection(changeSet.ModelType).Apply(changeSet);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReleaseCommitLock(List<ChangeSet> commit)
    {
        foreach (var changeSet in commit)
        {
            GetCollection(changeSet.ModelType).ReaderWriterLock.ExitWriteLock();
        }
        
        _dbLock.ExitReadLock();
    }

    private void Persist()
    {
        _dbLock.EnterWriteLock();
        try
        {
            foreach (var collection in _collections.Values)
            {
                collection.Persist();
            }

            _metadata.Version = _versionId;

            PersistenceEngine.Instance.WriteStoreMetadata(
                _options.FileSystem,
                _options.DirectoryPath,
                _metadata);

            PersistenceEngine.Instance.DeleteWal(
                _options.FileSystem,
                _options.DirectoryPath);

        }
        finally
        {
            _dbLock.ExitWriteLock();
        }
    }
    
    public void Dispose()
    {
        Persist();
    }
}