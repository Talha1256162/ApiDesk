import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-badge',
  standalone: true,
  template: '<span class="ui-badge" [class]="tone">{{ label }}</span>',
  styles: [
    `
      .ui-badge {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        min-height: 22px;
        padding: 0 8px;
        border-radius: 999px;
        border: 1px solid var(--border-subtle);
        background: var(--surface-muted);
        color: var(--text-secondary);
        font-size: 11px;
        font-weight: 700;
        line-height: 1;
      }

      .success {
        color: var(--success);
        background: var(--success-muted);
        border-color: var(--success-border);
      }

      .danger {
        color: var(--danger);
        background: var(--danger-muted);
        border-color: var(--danger-border);
      }

      .accent {
        color: var(--accent-strong);
        background: var(--accent-muted);
        border-color: var(--accent-border);
      }
    `
  ]
})
export class BadgeComponent {
  @Input({ required: true }) label = '';
  @Input() tone: 'default' | 'success' | 'danger' | 'accent' = 'default';
}
