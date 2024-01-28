using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using RaccoonSql.CoreRework.Querying;

namespace RaccoonSql.CoreRework.Indexes;

public enum IndexScanOrder
{
    Ascending,
    Descending,
}

public class IndexDoesNotSupportOrderedScan : Exception;

public interface IIndex
{
    public bool SupportsOrderedScan { get; }
    public bool TryConvertExpression(Expression expression, ParameterExpression modelParam, [NotNullWhen(true)] out IndexQueryExpression? result);

    /// <summary>
    /// Scan the index according to the given queries, and optionally in the given order.
    /// </summary>
    /// <param name="query">The queries to scan the index for</param>
    /// <param name="order">The order to scan the index by</param>
    /// <exception cref="IndexDoesNotSupportOrderedScan"> If <see cref="order"/> is not <see langword="null" /> and this index does not support ordered scan</exception>
    /// <returns>The results of the scan</returns>
    public IEnumerable<ModelBase> Scan(List<IndexQueryExpression> query, IndexScanOrder? order);
}


/// <summary>
/// A <see cref="QueryExpression"/> node that supports an operation on an <see cref="IIndex"/>. 
/// </summary>
public abstract class IndexQueryExpression : QueryExpression
{
    public required QueryExpressionModelField Field { get; init; }
    
    /// <summary>
    /// <see langword="true" /> if this node is trivially true and can be optimised away
    /// </summary>
    public abstract bool IsTriviallyTrue { get; }
    
    /// <summary>
    /// <see langword="true" /> if this node is trivially false and can be optimised away
    /// </summary>
    public abstract bool IsTriviallyFalse { get; }
    
    /// <summary>
    /// Tries to calculate the intersection with another <see cref="IndexQueryExpression"/>
    /// </summary>
    /// <param name="other">The other <see cref="IndexQueryExpression"/> to intersect with</param>
    /// <param name="result">The resulting intersection, or <see langword="null" /> if no there is no intersection</param>
    /// <returns><see langword="true" />if the intersection is not empty, <see langword="false" /> if it is empty</returns>
    /// <exception cref="InvalidOperationException">if the other parameter is of a different index type or has a different field</exception>
    public abstract bool TryIntersect(IndexQueryExpression other, [NotNullWhen(true)] out QueryExpression? result);

    /// <summary>
    /// Tries to calculate the union with another <see cref="IndexQueryExpression"/>
    /// </summary>
    /// <param name="other">The other <see cref="IndexQueryExpression"/> to union with</param>
    /// <param name="result">The resulting union, or <see langword="null" /> if the union is empty</param>
    /// <returns><see langword="true" />if the union is not empty, <see langword="false" /> if it is empty</returns>
    /// <exception cref="InvalidOperationException">if the other parameter is of a different index type or has a different field</exception>
    public abstract bool TryUnion(IndexQueryExpression other, [NotNullWhen(true)] out QueryExpression? result);


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
