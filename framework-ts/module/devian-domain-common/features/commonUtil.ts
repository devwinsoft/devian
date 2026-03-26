export function tryGetLanguageCode(language: string): string | null {
    const code = getLanguageCodeOrEmpty(language);
    return code.length > 0 ? code : null;
}

export function getLanguageCodeOrEmpty(language: string): string {
    switch (language) {
        case 'Korean':
            return 'ko';
        case 'English':
            return 'en';
        case 'Japanese':
            return 'ja';
        case 'ChineseSimplified':
            return 'zh-Hans';
        case 'ChineseTraditional':
            return 'zh-Hant';
        case 'German':
            return 'de';
        case 'French':
            return 'fr';
        case 'Spanish':
            return 'es';
        case 'Portuguese':
            return 'pt';
        case 'Russian':
            return 'ru';
        case 'Thai':
            return 'th';
        case 'Vietnamese':
            return 'vi';
        case 'Indonesian':
            return 'id';
        default:
            return '';
    }
}
