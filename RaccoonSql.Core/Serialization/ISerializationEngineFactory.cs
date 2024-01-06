using JetBrains.Annotations;

namespace RaccoonSql.Core.Serialization;

[PublicAPI]
public interface ISerializationEngineFactory
{
    ISerializationEngine Create();
}