import { DffValue, DffArray, DffObject } from './DffValue';

/**
 * DFF 변환 옵션
 */
export interface DffOptions {
    /**
     * 배열 구분자 (기본: ',')
     */
    arraySeparator?: string;

    /**
     * 객체 필드 구분자 (기본: ';')
     */
    fieldSeparator?: string;

    /**
     * Key-Value 구분자 (기본: '=')
     */
    kvSeparator?: string;
}

const DEFAULT_OPTIONS: Required<DffOptions> = {
    arraySeparator: ',',
    fieldSeparator: ';',
    kvSeparator: '=',
};

/**
 * DFF(Devian Friendly Format) 변환기.
 * Excel 셀 문자열을 Row2 타입 기반으로 DffValue로 정규화한다.
 * 
 * 타입별 허용 문법:
 * - Scalar: value (배열 금지)
 * - Scalar[]: a,b,c / {a,b,c} / [a,b,c]
 * - Enum: RARE (배열 금지)
 * - Enum[]: A,B,C / {A,B,C} / [A,B,C]
 * - Class: k=v; a=b ({...} 금지)
 * - Class[]: [k=v; a=b, k=v; a=b] ({...} 금지)
 * 
 * @see Devian.Protobuf.DffConverter (C#)
 */
export class DffConverter {
    private options: Required<DffOptions>;

    constructor(options?: DffOptions) {
        this.options = { ...DEFAULT_OPTIONS, ...options };
    }

    /**
     * 셀 문자열을 Row2 타입 기반으로 DffValue로 정규화한다.
     * 
     * @param raw 셀 원본 문자열
     * @param row2Type Row2 타입 (예: "int", "enum:UserType", "class:UserProfile[]")
     * @returns 정규화된 DffValue
     */
    normalize(raw: string | null | undefined, row2Type: string): DffValue {
        if (raw === null || raw === undefined || raw === '') {
            return this.getDefaultValue(row2Type);
        }

        const trimmed = raw.trim();
        const isArray = row2Type.endsWith('[]');
        const baseType = isArray ? row2Type.slice(0, -2) : row2Type;

        if (isArray) {
            return this.parseArray(trimmed, baseType);
        }

        return this.parseScalar(trimmed, baseType);
    }

    private parseScalar(raw: string, baseType: string): DffValue {
        // enum:TypeName
        if (baseType.startsWith('enum:')) {
            return raw; // enum은 문자열 그대로
        }

        // class:TypeName
        if (baseType.startsWith('class:')) {
            return this.parseObject(raw);
        }

        // Primitive types
        switch (baseType) {
            case 'bool':
                return raw.toLowerCase() === 'true' || raw === '1';
            case 'int':
            case 'uint':
            case 'short':
            case 'ushort':
            case 'byte':
            case 'ubyte':
                return parseInt(raw, 10);
            case 'long':
            case 'ulong':
                return BigInt(raw).toString(); // JSON 호환을 위해 문자열로
            case 'float':
            case 'double':
                return parseFloat(raw);
            case 'string':
            default:
                return raw;
        }
    }

    private parseArray(raw: string, baseType: string): DffArray {
        // 괄호 제거: {a,b,c} → a,b,c, [a,b,c] → a,b,c
        let content = raw;
        if ((content.startsWith('{') && content.endsWith('}')) ||
            (content.startsWith('[') && content.endsWith(']'))) {
            content = content.slice(1, -1);
        }

        if (content === '') {
            return [];
        }

        // Class 배열의 경우 특별 처리
        if (baseType.startsWith('class:')) {
            return this.parseClassArray(content);
        }

        // 일반 배열
        const parts = content.split(this.options.arraySeparator);
        return parts.map(part => this.parseScalar(part.trim(), baseType));
    }

    private parseObject(raw: string): DffObject {
        const result: DffObject = {};
        const pairs = raw.split(this.options.fieldSeparator);

        for (const pair of pairs) {
            const trimmed = pair.trim();
            if (!trimmed) continue;

            const idx = trimmed.indexOf(this.options.kvSeparator);
            if (idx === -1) continue;

            const key = trimmed.slice(0, idx).trim();
            const value = trimmed.slice(idx + 1).trim();
            result[key] = value; // 값 타입 추론은 스키마 기반으로 해야 함
        }

        return result;
    }

    private parseClassArray(raw: string): DffArray {
        // Class 배열: k=v; a=b, k=v; a=b
        const result: DffArray = [];
        const items = raw.split(this.options.arraySeparator);

        for (const item of items) {
            const trimmed = item.trim();
            if (!trimmed) continue;
            result.push(this.parseObject(trimmed));
        }

        return result;
    }

    private getDefaultValue(row2Type: string): DffValue {
        if (row2Type.endsWith('[]')) {
            return [];
        }

        if (row2Type.startsWith('class:')) {
            return {};
        }

        switch (row2Type) {
            case 'bool':
                return false;
            case 'int':
            case 'uint':
            case 'short':
            case 'ushort':
            case 'byte':
            case 'ubyte':
            case 'float':
            case 'double':
                return 0;
            case 'long':
            case 'ulong':
                return '0';
            case 'string':
            default:
                return '';
        }
    }
}

/**
 * 기본 DffConverter 인스턴스
 */
export const defaultConverter = new DffConverter();
