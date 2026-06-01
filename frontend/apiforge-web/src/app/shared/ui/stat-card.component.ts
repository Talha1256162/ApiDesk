import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-stat-card',
  standalone: true,
  template: `
    <article class="stat-card">
      <div class="stat-top">
        <span>{{ label }}</span>
        <small>{{ hint }}</small>
      </div>
      <strong>{{ value }}</strong>
      <p>{{ description }}</p>
    </article>
  `,
  styles: [
    `
      .stat-card {
        min-height: 132px;
        display: grid;
        align-content: space-between;
        gap: 12px;
        padding: 18px;
        border: 1px solid var(--border-subtle);
        border-radius: 12px;
        background: var(--surface-card);
        box-shadow: var(--shadow-card);
      }

      .stat-top {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 12px;
      }

      span,
      small,
      p {
        color: var(--text-tertiary);
      }

      span {
        font-size: 13px;
        font-weight: 700;
      }

      small {
        font-size: 11px;
      }

      strong {
        color: var(--text-primary);
        font-size: 30px;
        letter-spacing: 0;
      }

      p {
        min-height: 18px;
        margin: 0;
        font-size: 12px;
      }
    `
  ]
})
export class StatCardComponent {
  @Input({ required: true }) label = '';
  @Input({ required: true }) value: string | number = 0;
  @Input() hint = '';
  @Input() description = '';
}
