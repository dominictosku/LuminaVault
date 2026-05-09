import { Component, inject, signal } from '@angular/core';
import { Api } from '../../core/api';
import { OdsImportResult } from '../../core/models';

@Component({
  selector: 'app-data',
  templateUrl: './data.html',
  styleUrl: './data.scss'
})
export class DataComponent {
  private api = inject(Api);
  exporting = signal(false);
  importing = signal(false);
  error = signal<string | null>(null);
  result = signal<OdsImportResult | null>(null);

  export() {
    this.exporting.set(true);
    this.error.set(null);
    this.api.exportOds().subscribe({
      next: response => {
        this.exporting.set(false);
        const blob = response.body;
        if (!blob) return;
        const contentDisposition = response.headers.get('content-disposition') ?? '';
        const fileName = /filename="?([^"]+)"?/i.exec(contentDisposition)?.[1] ?? `LuminaVault-export-${new Date().toISOString().substring(0, 10)}.ods`;
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: e => {
        this.exporting.set(false);
        this.error.set(e?.error?.error ?? 'Export failed.');
      },
    });
  }

  import(input: HTMLInputElement) {
    const file = input.files?.[0];
    if (!file) return;
    this.importing.set(true);
    this.error.set(null);
    this.result.set(null);
    this.api.importOds(file).subscribe({
      next: result => {
        this.importing.set(false);
        this.result.set(result);
        input.value = '';
      },
      error: e => {
        this.importing.set(false);
        input.value = '';
        this.error.set(e?.error?.error ?? 'Import failed.');
      },
    });
  }
}
