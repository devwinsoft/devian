// SSOT: skills/devian-protocol/44-protocolgen-implementation/SKILL.md
// Protocol Type Mapper - Complex type aliases support
// "추측 금지" 정책: 명시된 별칭만 처리, 나머지는 null 반환

/**
 * Complex type aliases
 */
const COMPLEX_ALIASES = new Set(['cint', 'cfloat', 'cstring']);

/**
 * Check if baseType is a complex alias
 * @param {string} baseType 
 * @returns {boolean}
 */
export function isComplexAlias(baseType) {
    return COMPLEX_ALIASES.has(baseType);
}

/**
 * Map complex alias to C# type
 * @param {string} baseType 
 * @returns {string | null} - Returns mapped C# type or null if not an alias
 */
export function mapCSharpBaseType(baseType) {
    switch (baseType) {
        case 'cint': return 'CInt';
        case 'cfloat': return 'CFloat';
        case 'cstring': return 'CString';
        default: return null;
    }
}

/**
 * Map complex alias to TypeScript shape type
 * @param {string} baseType 
 * @returns {string | null} - Returns shape type string or null if not an alias
 */
export function mapTsType(baseType) {
    switch (baseType) {
        case 'cint':
        case 'cfloat':
            return '{ save1: number; save2: number }';
        case 'cstring':
            return '{ data: string }';
        default:
            return null;
    }
}

/**
 * Extract base type from possibly wrapped type (array, map, etc.)
 * @param {string} typeStr 
 * @returns {string}
 */
function extractBaseType(typeStr) {
    if (!typeStr) return '';
    
    // Handle array types: "int[]" -> "int"
    if (typeStr.endsWith('[]')) {
        return typeStr.slice(0, -2);
    }
    
    // Handle map types: "map<string,int>" -> check both key and value
    const mapMatch = typeStr.match(/^map<([^,]+),([^>]+)>$/);
    if (mapMatch) {
        // For map, we need to check value type (key is usually string)
        return mapMatch[2].trim();
    }
    
    return typeStr;
}

/**
 * Scan protocol JSON to check if any field uses complex aliases
 * @param {object} protocolJson - The protocol spec JSON
 * @returns {boolean}
 */
export function scanProtocolUsesComplexAliases(protocolJson) {
    if (!protocolJson || !protocolJson.messages) {
        return false;
    }

    for (const message of protocolJson.messages) {
        if (!message.fields) continue;
        
        for (const field of message.fields) {
            const baseType = extractBaseType(field.type);
            if (isComplexAlias(baseType)) {
                return true;
            }
        }
    }

    return false;
}

/**
 * Get all complex aliases used in a protocol
 * @param {object} protocolJson 
 * @returns {Set<string>}
 */
export function getUsedComplexAliases(protocolJson) {
    const used = new Set();
    
    if (!protocolJson || !protocolJson.messages) {
        return used;
    }

    for (const message of protocolJson.messages) {
        if (!message.fields) continue;
        
        for (const field of message.fields) {
            const baseType = extractBaseType(field.type);
            if (isComplexAlias(baseType)) {
                used.add(baseType);
            }
        }
    }

    return used;
}
