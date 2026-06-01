import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-skeleton',
  standalone: true,
  template: `
    @for (row of rowsArray; track row) {
      <span class="skeleton-line" [style.height.px]="height"></span>
    }
  `,
  styles: [
    `
      .skeleton-line {
        display: block;
        width: 100%;
        border-radius: 8px;
        background: linear-gradient(90deg, var(--surface-soft), var(--surface-muted), var(--surface-soft));
        background-size: 220% 100%;
        animation: shimmer 1.3s ease-in-out infinite;
      }

      .skeleton-line + .skeleton-line {
        margin-top: 10px;
      }

      @keyframes shimmer {
        0% {
          background-position: 100% 0;
        }
        100% {
          background-position: -100% 0;
        }
      }
    `
  ]
})
export class SkeletonComponent {
  @Input() rows = 3;
  @Input() height = 18;

  get rowsArray(): number[] {
    return Array.from({ length: this.rows }, (_, index) => index);
  }
}
