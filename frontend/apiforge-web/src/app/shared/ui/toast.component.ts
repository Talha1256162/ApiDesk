import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-toast',
  standalone: true,
  template: `
    @if (message) {
      <div class="toast" [class]="tone">
        <strong>{{ title }}</strong>
        <span>{{ message }}</span>
      </div>
    }
  `,
  styles: [
    `
      .toast {
        position: fixed;
        right: 22px;
        bottom: 22px;
        z-index: 50;
        width: min(380px, calc(100vw - 32px));
        display: grid;
        gap: 4px;
        padding: 14px 16px;
        border: 1px solid var(--border-strong);
        border-radius: 12px;
        background: var(--surface-elevated);
        box-shadow: var(--shadow-popover);
      }

      strong {
        color: var(--text-primary);
        font-size: 13px;
      }

      span {
        color: var(--text-secondary);
        font-size: 13px;
      }

      .success {
        border-color: var(--success-border);
      }

      .danger {
        border-color: var(--danger-border);
      }
    `
  ]
})
export class ToastComponent {
  @Input() title = 'Notice';
  @Input() message = '';
  @Input() tone: 'default' | 'success' | 'danger' = 'default';
}
