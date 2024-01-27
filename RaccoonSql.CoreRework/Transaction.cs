using System.ComponentModel;
using JetBrains.Annotations;
using RaccoonSql.CoreRework.Internal;

namespace RaccoonSql.CoreRework;

/// <summary>
/// Defines the commit mode of a transaction.
/// </summary>
[PublicAPI]
public enum CommitMode
{
    /// <summary>
    /// Commits changes when <see cref="ITransaction.Commit"/> is called on the transaction.
    /// </summary>
    Manual,

    /// <summary>
    /// Commits changes when <see cref="ITransaction.Commit"/> is called on the transaction or automatically also when it is disposed.
    /// </summary>
    Auto,
}

/// <summary>
/// Represents the options that a transaction uses.
/// </summary>
[PublicAPI]
public readonly struct TransactionOptions()
{
    /// <summary>
    /// The commit mode for the transaction.
    /// </summary>
    [DefaultValue(CommitMode.Manual)]
    public CommitMode CommitMode { get; init; } = CommitMode.Manual;
}

/// <summary>
/// Represents a unit of work akin to a regular transaction in an RDBMS.
/// Allows adding, accessing, updating and deleting models.
/// All changes in a transaction will be atomically saved when it is committed.
/// </summary>
[PublicAPI]
public interface ITransaction : IDisposable
{
    /// <summary>
    /// True if the transaction is already completed (committed, rolled back or disposed).
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Returns an <see cref="IModelSet{TModel}"/> for accessing all models from the specified type.
    /// </summary>
    /// <typeparam name="TModel">A model type.</typeparam>
    /// <returns>The <see cref="IModelSet{TModel}"/>.</returns>
    IModelSet<TModel> Set<TModel>()
        where TModel : ModelBase;

    /// <summary>
    /// Commits the transaction.
    /// All changes will be saved atomically.
    /// The transaction will be marked as completed.
    /// </summary>
    /// <exception cref="TransactionCompletedException">thrown if the transaction is already completed.</exception>
    void Commit();

    /// <summary>
    /// Rolls the transaction back. No changes will be saved.
    /// The transaction will be marked as completed.
    /// </summary>
    /// <exception cref="TransactionCompletedException">thrown if the transaction is already completed.</exception>
    void Rollback();

    /// <summary>
    /// Adds the given model to the set of its type.
    /// </summary>
    /// <param name="model">The model to be added in this transaction.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    void Add<TModel>(TModel model) where TModel : ModelBase;

    /// <summary>
    /// Adds the given models to the set of their type.
    /// </summary>
    /// <param name="models">The models to be added in this transaction.</param>
    /// <typeparam name="TModel">The type of the models.</typeparam>
    void Add<TModel>(params TModel[] models) where TModel : ModelBase;

    /// <summary>
    /// Adds the given models to the set of their type.
    /// </summary>
    /// <param name="models">The models to be added in this transaction.</param>
    /// <typeparam name="TModel">The type of the models.</typeparam>
    void Add<TModel>(IEnumerable<TModel> models) where TModel : ModelBase;

    /// <summary>
    /// Finds a model by its id.
    /// </summary>
    /// <param name="id">The id of the model that is being searched.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns>The model with the provided id or null if no such model was found.</returns>
    TModel? Find<TModel>(Guid id) where TModel : ModelBase;

    /// <summary>
    /// Marks a model for removal in the change tracker.
    /// Models that are marked for removal will be removed from the database when the transaction is committed.
    /// </summary>
    /// <param name="model">The model that is to be removed.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    void Remove<TModel>(TModel model) where TModel : ModelBase;

    /// <summary>
    /// Marks the provided models for removal in the change tracker.
    /// Models that are marked for removal will be removed from the database when the transaction is committed.
    /// </summary>
    /// <param name="models">The models that is to be removed.</param>
    /// <typeparam name="TModel">The type of the models.</typeparam>
    void Remove<TModel>(IEnumerable<TModel> models) where TModel : ModelBase;

    /// <summary>
    /// Marks the provided models for removal in the change tracker.
    /// Models that are marked for removal will be removed from the database when the transaction is committed.
    /// </summary>
    /// <param name="models">The models that is to be removed.</param>
    /// <typeparam name="TModel">The type of the models.</typeparam>
    void Remove<TModel>(params TModel[] models) where TModel : ModelBase;
}

/// <inheritdoc cref="ITransaction"/>
public sealed class Transaction : ITransaction
{
    private readonly ModelStore _store;
    private readonly TransactionOptions _transactionOptions;

    public bool IsCompleted { get; private set; }

    private readonly Dictionary<Type, IModelSet> _modelSets = [];

    internal Transaction(ModelStore store,
        TransactionOptions transactionOptions)
    {
        _store = store;
        _transactionOptions = transactionOptions;
    }

    public IModelSet<TModel> Set<TModel>()
        where TModel : ModelBase
    {
        if (IsCompleted) throw new TransactionCompletedException();

        if (!_modelSets.TryGetValue(typeof(TModel), out var set))
        {
            set = _modelSets[typeof(TModel)] = new ModelSet<TModel>(_store.GetCollection<TModel>(), this);
        }

        return (IModelSet<TModel>)set;
    }

    public void Commit()
    {
        if (IsCompleted) throw new TransactionCompletedException();
        IsCompleted = true;

        List<ChangeSet> commit = [];
        foreach (var (_, set) in _modelSets)
        {
            var changeSet = set.GetChanges();
            if (!changeSet.HasChanges)
                continue;

            commit.Add(changeSet);
        }

        _store.Commit(commit);
    }

    public void Rollback()
    {
        if (IsCompleted) throw new TransactionCompletedException();
        IsCompleted = true;
    }

    public void Add<TModel>(TModel model) where TModel : ModelBase
    {
        Set<TModel>().Add(model);
    }

    public void Add<TModel>(params TModel[] models) where TModel : ModelBase
    {
        Set<TModel>().Add(models);
    }

    public void Add<TModel>(IEnumerable<TModel> models) where TModel : ModelBase
    {
        Set<TModel>().Add(models);
    }

    public TModel? Find<TModel>(Guid id) where TModel : ModelBase
    {
        return Set<TModel>().Find(id);
    }

    public void Remove<TModel>(TModel model) where TModel : ModelBase
    {
        Set<TModel>().Remove(model);
    }

    public void Remove<TModel>(IEnumerable<TModel> models) where TModel : ModelBase
    {
        Set<TModel>().Remove(models);
    }

    public void Remove<TModel>(params TModel[] models) where TModel : ModelBase
    {
        Set<TModel>().Remove(models);
    }

    public void Dispose()
    {
        if (IsCompleted) return;

        switch (_transactionOptions.CommitMode)
        {
            case CommitMode.Auto:
                Commit();
                break;

            case CommitMode.Manual:
                Rollback();
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

/// <summary>
/// This exception is thrown when trying to commit or roll back an already completed transaction. 
/// </summary>
public class TransactionCompletedException : Exception;