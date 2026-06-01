import { Injectable } from '@angular/core';

export interface JsonStats {
  fileSize: number;
  depth: number;
  keys: number;
  arrays: number;
  objects: number;
}

export interface JsonValidationResult {
  valid: boolean;
  error?: string;
  line?: number;
  column?: number;
}

export interface JsonDiff {
  path: string;
  left: unknown;
  right: unknown;
  type: 'added' | 'removed' | 'changed';
}

@Injectable({ providedIn: 'root' })
export class DeveloperToolsService {
  beautify(input: string, sortKeys = false): string {
    const parsed = JSON.parse(input);
    return JSON.stringify(sortKeys ? this.sortObjectKeys(parsed) : parsed, null, 2);
  }

  minify(input: string): string {
    return JSON.stringify(JSON.parse(input));
  }

  validate(input: string): JsonValidationResult {
    try {
      JSON.parse(input);
      return { valid: true };
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Invalid JSON';
      const positionMatch = /position\s+(\d+)/i.exec(message);
      if (!positionMatch) {
        return { valid: false, error: message };
      }

      const position = Number(positionMatch[1]);
      const before = input.slice(0, position);
      const lines = before.split(/\r?\n/);
      return {
        valid: false,
        error: message,
        line: lines.length,
        column: lines[lines.length - 1].length + 1
      };
    }
  }

  stats(input: string): JsonStats {
    const parsed = input.trim() ? JSON.parse(input) : {};
    const stats: JsonStats = {
      fileSize: new Blob([input]).size,
      depth: 0,
      keys: 0,
      arrays: 0,
      objects: 0
    };

    const visit = (value: unknown, depth: number): void => {
      stats.depth = Math.max(stats.depth, depth);
      if (Array.isArray(value)) {
        stats.arrays += 1;
        value.forEach((item) => visit(item, depth + 1));
        return;
      }
      if (value && typeof value === 'object') {
        stats.objects += 1;
        const entries = Object.entries(value as Record<string, unknown>);
        stats.keys += entries.length;
        entries.forEach(([, child]) => visit(child, depth + 1));
      }
    };

    visit(parsed, 1);
    return stats;
  }

  tree(input: string): string {
    return this.renderTree(JSON.parse(input));
  }

  escapeJsonString(input: string): string {
    return JSON.stringify(input).slice(1, -1);
  }

  unescapeJsonString(input: string): string {
    return JSON.parse(`"${input.replace(/"/g, '\\"')}"`);
  }

  jsonPath(input: string, path: string): unknown {
    const parsed = JSON.parse(input);
    if (!path || path === '$') {
      return parsed;
    }

    const tokens = path
      .replace(/^\$\./, '')
      .replace(/^\$/, '')
      .match(/[^.[\]]+|\[(\d+)\]/g);

    return (tokens ?? []).reduce<unknown>((current, token) => {
      if (current == null) {
        return undefined;
      }
      const arrayMatch = /^\[(\d+)\]$/.exec(token);
      const key = arrayMatch ? Number(arrayMatch[1]) : token;
      return (current as Record<string | number, unknown>)[key];
    }, parsed);
  }

  compare(left: string, right: string): JsonDiff[] {
    return this.diff(JSON.parse(left), JSON.parse(right), '$');
  }

  toTypeScript(input: string, rootName = 'ApiResponse'): string {
    const parsed = JSON.parse(input);
    const lines: string[] = [];
    this.writeTypeScriptInterface(rootName, parsed, lines, new Set<string>());
    return lines.join('\n\n');
  }

  toCSharp(input: string, rootName = 'ApiResponseDto'): string {
    const parsed = JSON.parse(input);
    const lines: string[] = ['using System.Text.Json.Serialization;', '', `public sealed class ${rootName}`, '{'];
    Object.entries(this.objectShape(parsed)).forEach(([key, value]) => {
      lines.push(`    [JsonPropertyName("${key}")]`);
      lines.push(`    public ${this.csharpType(value, this.pascal(key))} ${this.pascal(key)} { get; init; }${this.isNullable(value) ? ' = default!;' : ''}`);
      lines.push('');
    });
    lines.push('}');
    return lines.join('\n');
  }

  toSql(input: string, tableName = 'ApiPayload'): string {
    const shape = this.objectShape(JSON.parse(input));
    const lines = [`create table dbo.${tableName}`, '('];
    const columns = Object.entries(shape).map(([key, value]) => `    [${key}] ${this.sqlType(value)} null`);
    lines.push(columns.join(',\n'));
    lines.push(');');
    return lines.join('\n');
  }

  decodeJwt(token: string): string {
    const parts = token.split('.');
    if (parts.length < 2) {
      throw new Error('JWT must contain at least header and payload segments.');
    }
    const decode = (part: string) => JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/')));
    return JSON.stringify({ header: decode(parts[0]), payload: decode(parts[1]) }, null, 2);
  }

  parseCurl(command: string): string {
    const method = /-X\s+([A-Z]+)/i.exec(command)?.[1] ?? (command.includes('-d ') || command.includes('--data') ? 'POST' : 'GET');
    const url = /curl\s+['"]?([^'"\s]+)['"]?/i.exec(command)?.[1] ?? '';
    const headers = [...command.matchAll(/-H\s+['"]([^:]+):\s*([^'"]+)['"]/gi)].map((match) => ({ key: match[1], value: match[2] }));
    const body = /(?:-d|--data(?:-raw)?)\s+['"]([\s\S]+?)['"](?:\s|$)/i.exec(command)?.[1] ?? '';
    return JSON.stringify({ method, url, headers, body }, null, 2);
  }

  private sortObjectKeys(value: unknown): unknown {
    if (Array.isArray(value)) {
      return value.map((item) => this.sortObjectKeys(item));
    }
    if (value && typeof value === 'object') {
      return Object.keys(value as Record<string, unknown>)
        .sort((a, b) => a.localeCompare(b))
        .reduce<Record<string, unknown>>((acc, key) => {
          acc[key] = this.sortObjectKeys((value as Record<string, unknown>)[key]);
          return acc;
        }, {});
    }
    return value;
  }

  private renderTree(value: unknown, indent = 0): string {
    const pad = '  '.repeat(indent);
    if (Array.isArray(value)) {
      return ['[', ...value.map((item, index) => `${pad}  ${index}: ${this.renderTree(item, indent + 1)}`), `${pad}]`].join('\n');
    }
    if (value && typeof value === 'object') {
      return ['{', ...Object.entries(value).map(([key, child]) => `${pad}  ${key}: ${this.renderTree(child, indent + 1)}`), `${pad}}`].join('\n');
    }
    return JSON.stringify(value);
  }

  private diff(left: unknown, right: unknown, path: string): JsonDiff[] {
    if (Object.is(left, right)) {
      return [];
    }

    if (!this.isPlainObject(left) || !this.isPlainObject(right)) {
      return [{ path, left, right, type: left === undefined ? 'added' : right === undefined ? 'removed' : 'changed' }];
    }

    const keys = new Set([...Object.keys(left as object), ...Object.keys(right as object)]);
    return [...keys].flatMap((key) =>
      this.diff(
        (left as Record<string, unknown>)[key],
        (right as Record<string, unknown>)[key],
        `${path}.${key}`
      )
    );
  }

  private objectShape(value: unknown): Record<string, unknown> {
    if (Array.isArray(value)) {
      return this.objectShape(value[0] ?? {});
    }
    return value && typeof value === 'object' ? (value as Record<string, unknown>) : {};
  }

  private writeTypeScriptInterface(name: string, value: unknown, lines: string[], written: Set<string>): string {
    const cleanName = this.pascal(name);
    if (written.has(cleanName)) {
      return cleanName;
    }
    written.add(cleanName);
    const shape = this.objectShape(value);
    const body = Object.entries(shape).map(([key, child]) => `  ${key}: ${this.tsType(key, child, lines, written)};`);
    lines.push(`export interface ${cleanName} {\n${body.join('\n')}\n}`);
    return cleanName;
  }

  private tsType(key: string, value: unknown, lines: string[], written: Set<string>): string {
    if (Array.isArray(value)) {
      return `${this.tsType(key, value[0] ?? '', lines, written)}[]`;
    }
    if (value && typeof value === 'object') {
      return this.writeTypeScriptInterface(key, value, lines, written);
    }
    return typeof value === 'number' ? 'number' : typeof value === 'boolean' ? 'boolean' : 'string';
  }

  private csharpType(value: unknown, name: string): string {
    if (Array.isArray(value)) {
      return `IReadOnlyList<${this.csharpType(value[0] ?? '', name)}>`;
    }
    if (value && typeof value === 'object') {
      return name;
    }
    return typeof value === 'number' ? 'decimal' : typeof value === 'boolean' ? 'bool' : 'string';
  }

  private sqlType(value: unknown): string {
    if (typeof value === 'number') {
      return Number.isInteger(value) ? 'int' : 'decimal(18,4)';
    }
    if (typeof value === 'boolean') {
      return 'bit';
    }
    if (value && typeof value === 'object') {
      return 'nvarchar(max)';
    }
    return 'nvarchar(max)';
  }

  private isNullable(value: unknown): boolean {
    return value === null || value === undefined || typeof value === 'object';
  }

  private pascal(value: string): string {
    return value
      .replace(/[^a-zA-Z0-9]+/g, ' ')
      .split(' ')
      .filter(Boolean)
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join('');
  }

  private isPlainObject(value: unknown): boolean {
    return !!value && typeof value === 'object' && !Array.isArray(value);
  }
}
