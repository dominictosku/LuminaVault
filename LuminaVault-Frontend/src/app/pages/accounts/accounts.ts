import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccountsApi } from '../../core/data-access/accounts-api';
import { BalanceSnapshotsApi } from '../../core/data-access/balance-snapshots-api';
import {
  AccountBalanceSnapshot,
  AccountBalanceSnapshotInput,
  FINANCE_ACCOUNT_TYPES,
  FinanceAccount,
  FinanceAccountInput,
  FinanceAccountType,
} from '../../core/models';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { CrudFormController } from '../../shared/crud-form/crud-form.controller';
import { ToastService } from '../../shared/toast/toast.service';

@Component({
  selector: 'app-accounts',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './accounts.html',
  styleUrl: './accounts.scss',
  providers: [CrudFormController],
})
export class AccountsComponent {
  private accountsApi = inject(AccountsApi);
  private snapshotsApi = inject(BalanceSnapshotsApi);
  private confirmDialog = inject(ConfirmDialogService);
  private toast = inject(ToastService);
  protected crud = inject<CrudFormController<FinanceAccountInput, FinanceAccount>>(CrudFormController);
  // Pass-through signals so the template keeps reading editingId()/saving()/error() unchanged.
  protected editingId = this.crud.editingId;
  protected saving = this.crud.saving;
  protected error = this.crud.error;
  accounts = signal<FinanceAccount[]>([]);
  snapshots = signal<AccountBalanceSnapshot[]>([]);
  loading = signal(true);

  accountTypes = FINANCE_ACCOUNT_TYPES;
  colors = ['#14b8a6', '#38bdf8', '#34d399', '#f59e0b', '#818cf8', '#fb7185'];

  model: FinanceAccountInput = this.defaultModel();
  snapshotDate = new Date().toISOString().substring(0, 10);
  snapshotActualBalance = 0;
  snapshotNotes = '';

  constructor() {
    this.crud.configure({
      create: input => this.accountsApi.createFinanceAccount(input),
      update: (id, input) => this.accountsApi.updateFinanceAccount(id, input),
      delete: id => this.accountsApi.deleteFinanceAccount(id),
      toastSubject: 'Account',
      saveErrorFallback: 'Save failed.',
      onSaved: () => { this.reset(); this.fetch(); },
      onRemoved: () => { this.reset(); this.fetch(); },
    });
    this.fetch();
  }

  fetch() {
    this.loading.set(true);
    this.accountsApi.listFinanceAccounts().subscribe({
      next: accounts => {
        this.accounts.set(accounts);
        this.loading.set(false);
        this.fetchSnapshots();
      },
      error: () => this.loading.set(false),
    });
  }

  fetchSnapshots() {
    this.snapshotsApi.listBalanceSnapshots(this.editingId() ?? undefined).subscribe(s => this.snapshots.set(s));
  }

  newAccount() {
    this.reset();
  }

  editAccount(account: FinanceAccount) {
    this.crud.startEdit(account.id);
    this.model = {
      name: account.name,
      institution: account.institution ?? '',
      type: account.type,
      currency: account.currency,
      startingBalance: account.startingBalance,
      balance: account.balance,
      color: account.color,
      notes: account.notes ?? '',
      isArchived: account.isArchived,
    };
    this.snapshotActualBalance = account.balance;
    this.fetchSnapshots();
  }

  save() {
    if (!this.model.name.trim()) {
      this.error.set('Name is required.');
      return;
    }
    this.crud.save({
      ...this.model,
      currency: (this.model.currency || 'CHF').toUpperCase(),
      startingBalance: Number(this.model.startingBalance) || 0,
      balance: Number(this.model.balance) || 0,
    });
  }

  remove() {
    this.crud.remove({
      title: 'Delete account?',
      message: 'Archive or delete this account?',
      detail: 'Accounts with financial history may be archived by the backend instead of removed.',
      confirmText: 'Delete',
    });
  }

  reset() {
    this.crud.cancel();
    this.model = this.defaultModel();
    this.snapshots.set([]);
    this.snapshotActualBalance = 0;
    this.snapshotNotes = '';
  }

  saveSnapshot() {
    if (!this.editingId()) return;
    const input: AccountBalanceSnapshotInput = {
      accountId: this.editingId()!,
      snapshotDate: new Date(this.snapshotDate).toISOString(),
      actualBalance: Number(this.snapshotActualBalance) || 0,
      isReconciled: true,
      notes: this.snapshotNotes,
    };
    this.snapshotsApi.createBalanceSnapshot(input).subscribe({
      next: () => {
        this.snapshotNotes = '';
        this.toast.success('Balance snapshot saved.');
        this.fetch();
      },
      error: e => this.error.set(e?.error?.error ?? 'Could not save balance snapshot.'),
    });
  }

  async deleteSnapshot(snapshot: AccountBalanceSnapshot) {
    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete balance snapshot?',
      message: `Remove the reconciliation snapshot from ${new Date(snapshot.snapshotDate).toLocaleDateString()}?`,
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.snapshotsApi.deleteBalanceSnapshot(snapshot.id).subscribe(() => {
      this.toast.success('Snapshot deleted.');
      this.fetch();
    });
  }

  private defaultModel(): FinanceAccountInput {
    return {
      name: '',
      institution: '',
      type: 'Checking' as FinanceAccountType,
      currency: 'CHF',
      startingBalance: 0,
      balance: 0,
      color: '#14b8a6',
      notes: '',
      isArchived: false,
    };
  }
}
