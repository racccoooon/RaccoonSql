using System.Reflection;
using JetBrains.Annotations;

namespace RaccoonSql.CoreRework;

/// <summary>
/// An exception that signals that the validation of a model has failed.
/// </summary>
[PublicAPI]
public abstract class CheckConstraintException : Exception;

/// <summary>
/// A check constraint on a single property of a model.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Property)]
public abstract class PropertyCheckConstraintAttribute : Attribute
{
    /// <summary>
    /// Returns a validator for the property it is defined on.
    /// </summary>
    /// <param name="propertyInfo">The property info that this attribute is set on.</param>
    /// <returns>The validator for the property.</returns>
    public abstract IModelValidator GetValidator(PropertyInfo propertyInfo);
}

/// <summary>
/// A check constraint on the whole model.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class)]
public abstract class ModelCheckConstraintAttribute : Attribute
{
    /// <summary>
    /// Returns a validator to validate the whole model.
    /// </summary>
    /// <param name="modelType">The type of model this validator is on.</param>
    /// <returns>A validator for the model.</returns>
    public abstract IModelValidator GetValidator(Type modelType);
}

/// <summary>
/// A validator for a model.
/// </summary>
/// <remarks>A validator may not modify a model.</remarks>
public interface IModelValidator
{
    /// <summary>
    /// Validates a model.
    /// </summary>
    /// <remarks>All validation exceptions thrown should implement <see cref="CheckConstraintException"/></remarks>
    /// <exception cref="CheckConstraintException">If the value is not valid.</exception>
    /// <param name="model">The model to validate.</param>
    void Check(ModelBase model);
}

/// <summary>
/// A typed validator for a model.
/// </summary>
/// <remarks>A validator may not modify a model.</remarks>
/// <typeparam name="TModel">The model type.</typeparam>
public interface IModelValidator<in TModel> : IModelValidator
    where TModel : ModelBase
{
    /// <summary>
    /// Validates a model.
    /// </summary>
    /// <remarks>All validation exceptions thrown should implement <see cref="CheckConstraintException"/></remarks>
    /// <exception cref="CheckConstraintException">If the value is not valid.</exception>
    /// <param name="model">The model to validate.</param>
    void Check(TModel model);

    /// <inheritdoc cref="IModelValidator.Check"/>
    void IModelValidator.Check(ModelBase model) => Check((TModel)model);
}