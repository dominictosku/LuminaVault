import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DataTransferApi } from '../../core/data-access/data-transfer-api';
import { OdsImportResult, OdsPreviewResult, OdsPreviewSheet } from '../../core/models';

@Component({
  selector: 'app-data',
  imports: [FormsModule],
  templateUrl: './data.html',
  styleUrl: './data.scss'
})
export class DataComponent {
  private api = inject(DataTransferApi);
  exporting = signal(false);
  importing = signal(false);
  previewing = signal(false);
  error = signal<string | null>(null);
  result = signal<OdsImportResult | null>(null);
  preview = signal<OdsPreviewResult | null>(null);
  selectedFile = signal<File | null>(null);
  selectedSheetName = '';
  target = '';
  columns: Record<string, string> = {};

  targets = ['Accounts', 'Transactions', 'Monthly summaries', 'Subscriptions', 'Assets', 'Finance categories', 'Asset categories'];
  targetFields: Record<string, string[]> = {
    Accounts: ['Name', 'Institution', 'Type', 'Currency', 'Starting balance', 'Balance', 'Color', 'Notes', 'Archived'],
    Transactions: ['Date', 'Kind', 'Account', 'Transfer account', 'Payee', 'Category', 'Amount', 'Status', 'Description', 'Notes', 'Tags'],
    'Monthly summaries': ['Month', 'Account', 'Income', 'Expenses', 'Opening balance', 'Closing balance', 'Notes'],
    Subscriptions: ['Name', 'Category', 'Provider', 'Account', 'Amount', 'Currency', 'Interval days', 'Started on', 'Next due', 'Auto renew', 'Status', 'Notes'],
    Assets: ['Name', 'Category', 'Description', 'Brand', 'Model', 'Serial number', 'Value', 'Purchase date', 'Warranty until', 'Quantity', 'Notes', 'Tags'],
    'Finance categories': ['Name', 'Color', 'Sort order'],
    'Asset categories': ['Name', 'Color', 'Sort order'],
  };

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

  previewImport(input: HTMLInputElement) {
    const file = input.files?.[0];
    if (!file) return;
    this.selectedFile.set(file);
    this.previewing.set(true);
    this.error.set(null);
    this.result.set(null);
    this.preview.set(null);
    this.api.previewOds(file).subscribe({
      next: result => {
        this.previewing.set(false);
        this.preview.set(result);
        const first = result.sheets[0];
        if (first) this.selectSheet(first);
        input.value = '';
      },
      error: e => {
        this.previewing.set(false);
        input.value = '';
        this.error.set(e?.error?.error ?? 'Preview failed.');
      },
    });
  }

  selectSheet(sheet: OdsPreviewSheet) {
    this.selectedSheetName = sheet.name;
    this.target = sheet.suggestedTarget || this.targets[0];
    this.autoMap();
  }

  selectedSheet() {
    return this.preview()?.sheets.find(s => s.name === this.selectedSheetName) ?? null;
  }

  fields() {
    return this.targetFields[this.target] ?? [];
  }

  autoMap() {
    const sheet = this.selectedSheet();
    if (!sheet) return;
    this.columns = {};
    for (const field of this.fields()) {
      const match = sheet.headers.find(h => this.normalize(h) === this.normalize(field));
      this.columns[field] = match ?? '';
    }
  }

  importMapped() {
    const file = this.selectedFile();
    if (!file || !this.selectedSheetName || !this.target) return;
    this.importing.set(true);
    this.error.set(null);
    this.api.importMappedOds(file, {
      sheetName: this.selectedSheetName,
      target: this.target,
      columns: this.columns,
    }).subscribe({
      next: result => {
        this.importing.set(false);
        this.result.set(result);
      },
      error: e => {
        this.importing.set(false);
        this.error.set(e?.error?.error ?? 'Mapped import failed.');
      },
    });
  }

  private normalize(value: string) {
    return value.toLowerCase().replace(/[^a-z0-9]/g, '');
  }
}
