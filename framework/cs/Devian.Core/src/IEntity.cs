namespace Devian.Core
{
    /// <summary>
    /// Marker interface for entity types.
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// Primary key 반환
        /// </summary>
        object GetKey();
    }
}
