/**
 * Marker interface for entity types.
 * Contract class, Key가 없는 Table Entity가 구현한다.
 * @see Devian.Core.IEntity (C#)
 */
export interface IEntity {
}

/**
 * Key가 있는 Entity 인터페이스.
 * Key가 있는 Table Entity가 구현한다.
 * @see Devian.Core.IEntityKey<T> (C#)
 */
export interface IEntityKey<T> extends IEntity {
    /**
     * Primary key 반환
     */
    getKey(): T;
}
