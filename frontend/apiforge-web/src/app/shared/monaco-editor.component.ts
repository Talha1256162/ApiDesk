import {
  AfterViewInit,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import loader from '@monaco-editor/loader';
import type * as Monaco from 'monaco-editor';

loader.config({ paths: { vs: '/assets/monaco/vs' } });

@Component({
  selector: 'app-monaco-editor',
  standalone: true,
  template: '<div class="monaco-host" #host></div>',
  styles: [
    `
      .monaco-host {
        min-height: 100%;
        height: 100%;
        width: 100%;
      }
    `
  ]
})
export class MonacoEditorComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('host', { static: true }) private host!: ElementRef<HTMLDivElement>;
  @Input() value = '';
  @Input() language = 'json';
  @Input() readonly = false;
  @Output() valueChange = new EventEmitter<string>();

  private editor?: Monaco.editor.IStandaloneCodeEditor;
  private monaco?: typeof Monaco;

  async ngAfterViewInit(): Promise<void> {
    this.monaco = await loader.init();
    const monacoApi = this.monaco;
    if (!monacoApi) {
      return;
    }
    this.editor = monacoApi.editor.create(this.host.nativeElement, {
      value: this.value,
      language: this.language,
      readOnly: this.readonly,
      automaticLayout: true,
      minimap: { enabled: false },
      fontSize: 13,
      lineNumbersMinChars: 3,
      theme: document.documentElement.classList.contains('light') ? 'vs' : 'vs-dark',
      scrollBeyondLastLine: false,
      padding: { top: 14, bottom: 14 }
    });

    this.editor.onDidChangeModelContent(() => this.valueChange.emit(this.editor?.getValue() ?? ''));
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.editor) {
      return;
    }

    if (changes['value'] && this.editor.getValue() !== this.value) {
      this.editor.setValue(this.value ?? '');
    }

    if (changes['language']) {
      const model = this.editor.getModel();
      if (model) {
        this.monaco?.editor.setModelLanguage(model, this.language);
      }
    }
  }

  ngOnDestroy(): void {
    this.editor?.dispose();
  }
}
