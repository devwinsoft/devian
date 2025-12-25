namespace Devian.Core
{
    /// <summary>
    /// Entity serialization/deserialization interface.
    /// </summary>
    public interface IEntityCodec
    {
        byte[] Serialize<T>(T entity);
        T Deserialize<T>(byte[] data);
    }
}
