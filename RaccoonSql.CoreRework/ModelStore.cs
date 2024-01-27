using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using RaccoonSql.CoreRework.Internal;

namespace RaccoonSql.CoreRework;

/// <summary>
/// The options for setting up the <see cref="IModelStore"/>.
/// </summary>
public record ModelStoreOptions
{
    public required string DirectoryPath { get; init; }
    public required IFileSystem FileSystem { get; init; }
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
/// <param name="options">The options for the database.</param>
public sealed class ModelStore(ModelStoreOptions options) : IModelStore
{
    private readonly ConcurrentDictionary<Type, IModelCollection> _collections = [];

    public ITransaction Transaction(TransactionOptions transactionOptions = default)
    {
        return new Transaction(this, transactionOptions);
    }

    internal ModelCollection<TModel> GetCollection<TModel>()
        where TModel : ModelBase
    {
        return (ModelCollection<TModel>)_collections.GetOrAdd(
            typeof(TModel),
            _ => new ModelCollection<TModel>(options));
    }

    internal void Commit(List<ChangeSet> commit)
    {
        ValidateCheckConstraints(commit);
        
        commit.Sort((a, b) 
            => string.Compare(a.ModelType.FullName, b.ModelType.FullName, StringComparison.Ordinal));

        AcquireCommitLock(commit);
        
        try
        {
            ApplyCommit(commit);
        }
        finally
        {
            ReleaseCommitLock(commit);
        }

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateCheckConstraints(List<ChangeSet> commit)
    {
        foreach (var changeSet in commit)
        {
            _collections[changeSet.ModelType].ValidateCheckConstraints(changeSet);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AcquireCommitLock(List<ChangeSet> commit)
    {
        foreach (var changeSet in commit)
        {
            _collections[changeSet.ModelType].ReaderWriterLock.EnterWriteLock();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyCommit(List<ChangeSet> commit)
    {
        foreach (var changeSet in commit)
        {
            _collections[changeSet.ModelType].Apply(changeSet);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReleaseCommitLock(List<ChangeSet> commit)
    {
        foreach (var changeSet in commit)
        {
            _collections[changeSet.ModelType].ReaderWriterLock.ExitWriteLock();
        }
    }

}