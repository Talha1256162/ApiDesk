import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

type TreeEntry = { key: string; value: unknown };

@Component({
  selector: 'app-json-tree',
  imports: [CommonModule],
  template: `
    <div class="json-tree">
      <ng-container *ngTemplateOutlet="node; context: { $implicit: value, key: rootLabel, depth: 0 }"></ng-container>
    </div>

    <ng-template #node let-data let-key="key" let-depth="depth">
      <div class="tree-row" [style.padding-left.px]="depth * 14">
        <ng-container *ngIf="isExpandable(data); else primitive">
          <details [open]="depth < 2">
            <summary>
              <span class="tree-key">{{ key }}</span>
              <span class="tree-kind">{{ kindLabel(data) }}</span>
              <span class="tree-count">{{ entryCount(data) }}</span>
            </summary>
            <ng-container *ngFor="let entry of entries(data); trackBy: trackEntry">
              <ng-container *ngTemplateOutlet="node; context: { $implicit: entry.value, key: entry.key, depth: depth + 1 }"></ng-container>
            </ng-container>
          </details>
        </ng-container>
        <ng-template #primitive>
          <span class="tree-key">{{ key }}</span>
          <span class="tree-value" [class.string-value]="isString(data)">{{ displayValue(key, data) }}</span>
        </ng-template>
      </div>
    </ng-template>
  `,
  styles: [`
    .json-tree {
      height: 100%;
      overflow: auto;
      border: 1px solid var(--border-subtle);
      border-radius: 10px;
      background: var(--surface-soft);
      padding: 10px;
      font: 12px/1.55 "SFMono-Regular", Consolas, "Liberation Mono", monospace;
    }

    .tree-row {
      min-height: 26px;
      color: var(--text-secondary);
    }

    details {
      min-width: 0;
    }

    summary {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      cursor: pointer;
      list-style: none;
      border-radius: 7px;
      padding: 3px 6px;
    }

    summary::-webkit-details-marker {
      display: none;
    }

    summary::before {
      content: ">";
      color: var(--text-tertiary);
      font-weight: 900;
      transition: transform 140ms ease;
    }

    details[open] > summary::before {
      transform: rotate(90deg);
    }

    summary:hover {
      background: var(--surface-muted);
    }

    .tree-key {
      color: var(--accent-strong);
      font-weight: 850;
    }

    .tree-kind,
    .tree-count {
      color: var(--text-tertiary);
      font-size: 11px;
    }

    .tree-value {
      margin-left: 8px;
      color: var(--text-primary);
      word-break: break-word;
    }

    .string-value {
      color: var(--success);
    }
  `]
})
export class JsonTreeComponent {
  @Input() value: unknown;
  @Input() rootLabel = 'response';

  isExpandable(value: unknown): boolean {
    return Array.isArray(value) || (!!value && typeof value === 'object');
  }

  isString(value: unknown): boolean {
    return typeof value === 'string';
  }

  entries(value: unknown): TreeEntry[] {
    if (Array.isArray(value)) {
      return value.map((item, index) => ({ key: `[${index}]`, value: item }));
    }

    if (value && typeof value === 'object') {
      return Object.entries(value as Record<string, unknown>).map(([key, child]) => ({ key, value: child }));
    }

    return [];
  }

  entryCount(value: unknown): string {
    return `${this.entries(value).length} items`;
  }

  kindLabel(value: unknown): string {
    return Array.isArray(value) ? 'array' : 'object';
  }

  displayValue(key: string, value: unknown): string {
    if (this.isSensitiveKey(key) && value !== null && value !== undefined && value !== '') {
      return '"********"';
    }
    return JSON.stringify(value);
  }

  trackEntry(_: number, entry: TreeEntry): string {
    return entry.key;
  }

  private isSensitiveKey(key: string): boolean {
    return /(password|token|secret|api[-_]?key|authorization|cookie)/i.test(key);
  }
}
