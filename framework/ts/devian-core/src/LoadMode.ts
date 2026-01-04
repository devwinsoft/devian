/**
 * Table loading mode.
 * @see Devian.Core.LoadMode (C#)
 */
export enum LoadMode {
    /**
     * 기존 캐시 유지 + 새 데이터 병합. key 충돌 시 overwrite.
     */
    Merge = 'merge',

    /**
     * 기존 캐시 Clear 후 로드.
     */
    Replace = 'replace',
}
