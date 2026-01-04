/**
 * DFF(Devian Friendly Format) 값 타입.
 * Excel 셀에서 파싱된 정규화된 값을 표현한다.
 * 
 * @see Devian.Protobuf.DffValue (C#)
 */
export type DffValue =
    | DffScalar
    | DffArray
    | DffObject;

/**
 * 스칼라 값 (string, number, boolean, null)
 */
export type DffScalar = string | number | boolean | null;

/**
 * 배열 값
 */
export type DffArray = DffValue[];

/**
 * 객체 값 (key-value pairs)
 */
export type DffObject = { [key: string]: DffValue };

/**
 * DffValue 유틸리티 함수들
 */
export const DffValueUtils = {
    isScalar(value: DffValue): value is DffScalar {
        return value === null || ['string', 'number', 'boolean'].includes(typeof value);
    },

    isArray(value: DffValue): value is DffArray {
        return Array.isArray(value);
    },

    isObject(value: DffValue): value is DffObject {
        return typeof value === 'object' && value !== null && !Array.isArray(value);
    },

    /**
     * DffValue를 JSON 문자열로 변환
     */
    toJson(value: DffValue): string {
        return JSON.stringify(value);
    },

    /**
     * JSON 문자열을 DffValue로 파싱
     */
    fromJson(json: string): DffValue {
        return JSON.parse(json) as DffValue;
    },
};
