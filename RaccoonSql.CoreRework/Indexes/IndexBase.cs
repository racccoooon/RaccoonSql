using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using RaccoonSql.CoreRework.Querying;

namespace RaccoonSql.CoreRework.Indexes;

[PublicAPI]
public enum IndexScanOrder
{
    Ascending,
    Descending,
}

[PublicAPI]
public class IndexDoesNotSupportOrderedScan : Exception;

/// <summary>
/// The base class for indexes.
/// </summary>
/// <remarks>An index class must have a public constructor with exactly one <see cref="PropertyInfo"/> as its parameter.</remarks>
/// <param name="propertyInfo"></param>
[PublicAPI]
public abstract class IndexBase(PropertyInfo propertyInfo)
{
    protected readonly PropertyInfo PropertyInfo = propertyInfo;

    public abstract bool SupportsOrderedScan { get; }

    public abstract bool TryConvertExpression(
        Expression expression,
        ParameterExpression modelParam,
        [NotNullWhen(true)] out IndexQueryExpression? result);

    /// <summary>
    /// Scan the index according to the given queries, and optionally in the given order.
    /// </summary>
    /// <param name="query">The queries to scan the index for</param>
    /// <param name="order">The order to scan the index by</param>
    /// <exception cref="IndexDoesNotSupportOrderedScan"> If <see cref="order"/> is not <see langword="null" /> and this index does not support ordered scan</exception>
    /// <returns>The results of the scan</returns>
    public abstract IEnumerable<ModelBase> Scan(
        List<IndexQueryExpression> query,
        IndexScanOrder? order);
}

/// <summary>
/// A <see cref="QueryExpression"/> node that supports an operation on an <see cref="IndexBase"/>. 
/// </summary>
public abstract class IndexQueryExpression : QueryExpression
{
    public required QueryExpressionModelField Field { get; init; }


    public abstract bool TrySimplify([NotNullWhen(true)] out QueryExpression? result);
    
    /// <summary>
    /// Tries to calculate the intersection with another <see cref="IndexQueryExpression"/>
    /// </summary>
    /// <param name="other">The other <see cref="IndexQueryExpression"/> to intersect with</param>
    /// <param name="result">The resulting intersection, or <see langword="null" /> if no there is no intersection</param>
    /// <returns><see langword="true" />if the intersection is not empty, <see langword="false" /> if it is empty</returns>
    /// <exception cref="InvalidOperationException">if the other parameter is of a different index type or has a different field</exception>
    public abstract bool TryIntersect(
        IndexQueryExpression other,
        [NotNullWhen(true)] out QueryExpression? result);

    /// <summary>
    /// Tries to calculate the union with another <see cref="IndexQueryExpression"/>
    /// </summary>
    /// <param name="other">The other <see cref="IndexQueryExpression"/> to union with</param>
    /// <param name="result">The resulting union, or <see langword="null" /> if the union is empty</param>
    /// <returns><see langword="true" />if the union is not empty, <see langword="false" /> if it is empty</returns>
    /// <exception cref="InvalidOperationException">if the other parameter is of a different index type or has a different field</exception>
    public abstract bool TryUnion(
        IndexQueryExpression other,
        [NotNullWhen(true)] out QueryExpression? result);


    /// <summary>
    /// Tries to logically invert this query expression 
    /// </summary>
    /// <param name="result">the logical inversion of this query expression</param>
    /// <returns><see langword="true" /> if the operation was successful, <see langword="false" /> otherwise</returns>
    public abstract bool TryInvert([NotNullWhen(true)] out QueryExpression? result);

    /// <summary>
    /// Convert this node to an equivalent <see cref="Expression"/>.
    /// </summary>
    /// <param name="propertyParameter">The parameter to refer to the field that this filter applies to</param>
    /// <returns>An <see cref="Expression"/> that is equivalent to this node</returns>
    public abstract Expression ToExpression(ParameterExpression propertyParameter);

    // TODO: EstimateCost and EstimateCount
}
