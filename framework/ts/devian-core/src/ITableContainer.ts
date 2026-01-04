import { LoadMode } from './LoadMode';

/**
 * 테이블 컨테이너 인터페이스.
 * 모든 생성된 TB_{TableName} 타입이 구현한다.
 * 
 * @remarks
 * - JSON은 NDJSON string (1 row = 1 line). Root array 금지.
 * - Base64는 내부 binary(delimited) + base64 wrapper.
 * 
 * @see Devian.Core.ITableContainer (C#)
 */
export interface ITableContainer<T> {
    /**
     * 캐시된 Row 개수
     */
    readonly count: number;

    /**
     * 캐시된 모든 Row를 제거한다.
     */
    clear(): void;

    /**
     * 캐시된 모든 Row를 반환한다.
     */
    getAll(): readonly T[];

    /**
     * NDJSON string에서 Row들을 로드한다.
     * @param json NDJSON string (1 row = 1 line)
     * @param mode Merge(기본): key 충돌 시 덮어씀. Replace: Clear 후 적재.
     */
    loadFromJson(json: string, mode?: LoadMode): void;

    /**
     * 캐시된 Row들을 NDJSON string으로 반환한다.
     * @returns NDJSON string (1 row = 1 line)
     */
    saveToJson(): string;

    /**
     * Base64 인코딩된 binary에서 Row들을 로드한다.
     * @param base64 Base64 인코딩된 delimited binary
     * @param mode Merge(기본): key 충돌 시 덮어씀. Replace: Clear 후 적재.
     */
    loadFromBase64?(base64: string, mode?: LoadMode): void;

    /**
     * 캐시된 Row들을 Base64 인코딩된 binary로 반환한다.
     * @returns Base64 인코딩된 delimited binary
     */
    saveToBase64?(): string;
}

/**
 * Key가 있는 테이블 컨테이너 인터페이스.
 */
export interface IKeyedTableContainer<T, K> extends ITableContainer<T> {
    /**
     * Key로 Row를 조회한다.
     * @param key Primary key
     * @returns Row 또는 undefined
     */
    get(key: K): T | undefined;

    /**
     * Key로 Row 존재 여부를 확인한다.
     * @param key Primary key
     * @returns 존재 여부
     */
    has(key: K): boolean;
}
