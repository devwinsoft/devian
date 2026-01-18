// SSOT: skills/devian-common/12-feature-logger/SKILL.md

/**
 * Log level for filtering and categorization.
 */
export enum LogLevel {
    Debug = 10,
    Info = 20,
    Warn = 30,
    Error = 40,
}

/**
 * Interface for log output sinks.
 * Implementations can write to console, file, network, etc.
 */
export interface LogSink {
    write(level: LogLevel, tag: string, message: string, err?: unknown): void;
}

/**
 * Default console log sink.
 * Format: [{LEVEL}] {tag} - {message}
 */
export class ConsoleLogSink implements LogSink {
    write(level: LogLevel, tag: string, message: string, err?: unknown): void {
        const levelStr = LogLevel[level] ?? 'UNKNOWN';
        const formatted = `[${levelStr.toUpperCase()}] ${tag} - ${message}`;

        switch (level) {
            case LogLevel.Debug:
                console.debug(formatted);
                break;
            case LogLevel.Info:
                console.info(formatted);
                break;
            case LogLevel.Warn:
                console.warn(formatted);
                break;
            case LogLevel.Error:
                console.error(formatted);
                break;
            default:
                console.log(formatted);
        }

        if (err !== undefined) {
            console.error(err);
        }
    }
}

// Module state
let _level: LogLevel = LogLevel.Debug;
let _sink: LogSink = new ConsoleLogSink();

// Configuration

export function setLevel(level: LogLevel): void {
    _level = level;
}

export function getLevel(): LogLevel {
    return _level;
}

export function setSink(sink: LogSink): void {
    if (!sink) throw new Error('sink is required');
    _sink = sink;
}

export function getSink(): LogSink {
    return _sink;
}

// Output

export function debug(tag: string, message: string): void {
    log(LogLevel.Debug, tag, message);
}

export function info(tag: string, message: string): void {
    log(LogLevel.Info, tag, message);
}

export function warn(tag: string, message: string): void {
    log(LogLevel.Warn, tag, message);
}

export function error(tag: string, message: string, err?: unknown): void {
    log(LogLevel.Error, tag, message, err);
}

function log(level: LogLevel, tag: string, message: string, err?: unknown): void {
    if (level < _level) return;
    _sink.write(level, tag, message, err);
}
