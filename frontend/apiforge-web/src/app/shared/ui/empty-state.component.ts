import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  template: `
    <div class="empty-shell">
      <div class="empty-icon">{{ icon }}</div>
      <strong>{{ title }}</strong>
      <p>{{ description }}</p>
      <div class="empty-actions">
        <ng-content></ng-content>
      </div>
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
        position: relative;
        border: 1px solid rgba(99, 102, 241, 0.24);
        border-radius: 12px;
        background:
          radial-gradient(circle at 35% 30%, rgba(99, 102, 241, 0.26), transparent 42%),
          var(--surface-muted);
        color: var(--text-primary);
        font-size: 10px;
        font-weight: 900;
        box-shadow: 0 12px 30px rgba(99, 102, 241, 0.12);
      }

      .empty-icon::before,
      .empty-icon::after {
        content: '';
        position: absolute;
        border-radius: 999px;
        background: currentColor;
        opacity: 0.5;
      }

      .empty-icon::before {
        width: 5px;
        height: 5px;
        top: 9px;
        right: 9px;
      }

      .empty-icon::after {
        width: 14px;
        height: 2px;
        bottom: 10px;
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

      .empty-actions {
        display: flex;
        flex-wrap: wrap;
        justify-content: center;
        gap: 8px;
        margin-top: 4px;
      }

      .empty-actions:empty {
        display: none;
      }
    `
  ]
})
export class EmptyStateComponent {
  @Input() icon = 'AP';
  @Input({ required: true }) title = '';
  @Input() description = '';
}
