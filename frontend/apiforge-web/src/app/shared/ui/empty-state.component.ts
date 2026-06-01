import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  template: `
    <div class="empty-shell">
      <div class="empty-icon">{{ icon }}</div>
      <strong>{{ title }}</strong>
      <p>{{ description }}</p>
    </div>
  `,
  styles: [
    `
      .empty-shell {
        min-height: 148px;
        display: grid;
        place-items: center;
        gap: 8px;
        padding: 22px;
        border: 1px dashed var(--border-strong);
        border-radius: 12px;
        color: var(--text-secondary);
        text-align: center;
        background: var(--surface-soft);
      }

      .empty-icon {
        width: 36px;
        height: 36px;
        display: grid;
        place-items: center;
        border-radius: 10px;
        background: var(--surface-muted);
        color: var(--text-primary);
        font-weight: 800;
      }

      strong {
        color: var(--text-primary);
        font-size: 14px;
      }

      p {
        max-width: 420px;
        margin: 0;
        color: var(--text-tertiary);
        font-size: 13px;
        line-height: 1.5;
      }
    `
  ]
})
export class EmptyStateComponent {
  @Input() icon = 'AF';
  @Input({ required: true }) title = '';
  @Input() description = '';
}
