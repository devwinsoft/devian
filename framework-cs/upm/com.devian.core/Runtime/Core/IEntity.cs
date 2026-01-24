namespace Devian
{
    /// <summary>
    /// Marker interface for entity types.
    /// Contract class, Key가 없는 Table Entity가 구현한다.
    /// </summary>
    public interface IEntity
    {
    }

    /// <summary>
    /// Key가 있는 Entity 인터페이스.
    /// Key가 있는 Table Entity가 구현한다.
    /// </summary>
    /// <typeparam name="T">Key 타입</typeparam>
    public interface IEntityKey<T> : IEntity
    {
        /// <summary>
        /// Primary key 반환
        /// </summary>
        T GetKey();
    }
}
