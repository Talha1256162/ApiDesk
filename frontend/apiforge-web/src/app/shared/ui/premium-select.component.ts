import { CommonModule } from '@angular/common';
import { Component, ElementRef, EventEmitter, HostListener, Input, Output, computed, signal } from '@angular/core';

export interface PremiumSelectOption {
  value: string;
  label: string;
  meta?: string;
}

@Component({
  selector: 'app-premium-select',
  imports: [CommonModule],
  template: `
    <div class="select-shell" [class.open]="open()" [class.disabled]="disabled">
      @if (label) {
        <span class="select-label">{{ label }}</span>
      }
      <button class="select-trigger" type="button" [disabled]="disabled" (click)="toggle()">
        <span>
          <strong>{{ selectedOption()?.label || placeholder }}</strong>
          @if (selectedOption()?.meta) {
            <small>{{ selectedOption()?.meta }}</small>
          }
        </span>
        <i></i>
      </button>
      @if (open()) {
        <div class="select-menu">
          @for (option of options; track option.value) {
            <button type="button" [class.selected]="option.value === value" (click)="choose(option.value)">
              <span>
                <strong>{{ option.label }}</strong>
                @if (option.meta) {
                  <small>{{ option.meta }}</small>
                }
              </span>
              @if (option.value === value) {
                <b></b>
              }
            </button>
          } @empty {
            <div class="select-empty">No options</div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
      min-width: 0;
    }

    .select-shell {
      position: relative;
      display: grid;
      gap: 7px;
      min-width: 0;
    }

    .select-label {
      color: var(--text-secondary);
      font-size: 12px;
      font-weight: 800;
    }

    .select-trigger {
      width: 100%;
      min-height: 44px;
      display: grid;
      grid-template-columns: minmax(0, 1fr) 18px;
      align-items: center;
      gap: 10px;
      border: 1px solid var(--border-strong);
      border-radius: 9px;
      background:
        linear-gradient(180deg, rgba(255, 255, 255, 0.025), rgba(255, 255, 255, 0)),
        var(--surface-muted);
      color: var(--text-primary);
      padding: 0 12px 0 14px;
      text-align: left;
      box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.03);
      transition: border-color 150ms ease, background 150ms ease, box-shadow 150ms ease;
    }

    .select-trigger:hover,
    .open .select-trigger {
      border-color: var(--accent-border);
      background: var(--surface-elevated);
    }

    .open .select-trigger {
      box-shadow: 0 0 0 3px var(--accent-muted);
    }

    .select-trigger span,
    .select-menu button span {
      min-width: 0;
      display: grid;
      gap: 2px;
    }

    strong {
      min-width: 0;
      overflow: hidden;
      color: var(--text-primary);
      font-size: 13px;
      font-weight: 850;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    small {
      min-width: 0;
      overflow: hidden;
      color: var(--text-tertiary);
      font-size: 11px;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    i {
      width: 16px;
      height: 16px;
      position: relative;
      border-radius: 6px;
      background: rgba(255, 255, 255, 0.04);
    }

    i::before {
      content: '';
      position: absolute;
      top: 5px;
      left: 4px;
      width: 7px;
      height: 7px;
      border-right: 1.5px solid var(--text-secondary);
      border-bottom: 1.5px solid var(--text-secondary);
      transform: rotate(45deg);
      transition: transform 150ms ease, top 150ms ease;
    }

    .open i::before {
      top: 7px;
      transform: rotate(225deg);
    }

    .select-menu {
      position: absolute;
      z-index: 40;
      top: calc(100% + 8px);
      left: 0;
      right: 0;
      max-height: 292px;
      overflow: auto;
      border: 1px solid var(--border-strong);
      border-radius: 10px;
      background: color-mix(in srgb, var(--surface-card) 94%, #000 6%);
      padding: 6px;
      box-shadow: var(--shadow-popover);
    }

    .select-menu button {
      width: 100%;
      min-height: 42px;
      display: grid;
      grid-template-columns: minmax(0, 1fr) 16px;
      align-items: center;
      gap: 10px;
      border-radius: 7px;
      background: transparent;
      padding: 8px 9px;
      text-align: left;
    }

    .select-menu button:hover,
    .select-menu button.selected {
      background: var(--accent-muted);
    }

    b {
      width: 7px;
      height: 7px;
      justify-self: center;
      border-radius: 999px;
      background: var(--accent-strong);
      box-shadow: 0 0 0 4px var(--accent-muted);
    }

    .select-empty {
      padding: 12px;
      color: var(--text-tertiary);
      font-size: 12px;
    }

    .disabled {
      opacity: 0.6;
    }
  `]
})
export class PremiumSelectComponent {
  @Input() label = '';
  @Input() placeholder = 'Select';
  @Input() value = '';
  @Input() options: PremiumSelectOption[] = [];
  @Input() disabled = false;
  @Output() valueChange = new EventEmitter<string>();

  readonly open = signal(false);
  readonly selectedOption = computed(() => this.options.find((option) => option.value === this.value));

  constructor(private readonly elementRef: ElementRef<HTMLElement>) {}

  toggle(): void {
    if (!this.disabled) {
      this.open.update((value) => !value);
    }
  }

  choose(value: string): void {
    this.valueChange.emit(value);
    this.open.set(false);
  }

  @HostListener('document:click', ['$event'])
  closeFromOutside(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }
}
