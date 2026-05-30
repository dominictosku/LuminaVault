import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth.service';
import { AuthApi } from '../../core/data-access/auth-api';
import { SettingsApi } from '../../core/data-access/settings-api';
import {
  AssetCategory,
  AssetCategoryInput,
  FinanceCategory,
  FinanceCategoryInput,
  FinanceCategoryRule,
  FinanceCategoryRuleInput,
  ExchangeRate,
  ExchangeRateInput,
  TwoFactorSetup,
  TwoFactorStatus,
} from '../../core/models';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';

type SettingsTab = 'assets' | 'finance' | 'rules' | 'rates' | 'security';
type EditableCategory = AssetCategory | FinanceCategory;
type CategoryInput = AssetCategoryInput | FinanceCategoryInput;

@Component({
  selector: 'app-settings',
  imports: [FormsModule, DatePipe],
  templateUrl: './settings.html',
  styleUrl: './settings.scss'
})
export class SettingsComponent {
  private api = inject(SettingsApi);
  private auth = inject(AuthService);
  private authApi = inject(AuthApi);
  private confirmDialog = inject(ConfirmDialogService);
  private toast = inject(ToastService);
  tab = signal<SettingsTab>('assets');
  assetCategories = signal<AssetCategory[]>([]);
  financeCategories = signal<FinanceCategory[]>([]);
  financeCategoryRules = signal<FinanceCategoryRule[]>([]);
  exchangeRates = signal<ExchangeRate[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);
  editingRuleId = signal<number | null>(null);
  editingRateId = signal<number | null>(null);
  passwordChanged = signal(false);
  colors = ['#7c3aed', '#ec4899', '#06b6d4', '#22c55e', '#f59e0b', '#ef4444', '#94a3b8'];

  // Two-factor authentication enrolment state.
  twoFa = signal<TwoFactorStatus | null>(null);
  twoFaSetup = signal<TwoFactorSetup | null>(null);
  twoFaQr = signal<string | null>(null);
  twoFaRecoveryCodes = signal<string[] | null>(null);
  twoFaBusy = signal(false);
  twoFaError = signal<string | null>(null);
  twoFaCode = '';
  twoFaPassword = '';

  title = computed(() => {
    if (this.tab() === 'assets') return 'Asset categories';
    if (this.tab() === 'rules') return 'Auto-categorization rules';
    if (this.tab() === 'rates') return 'Exchange rates';
    if (this.tab() === 'security') return 'Security';
    return 'Transaction & subscription categories';
  });

  currentCategories = computed<EditableCategory[]>(() =>
    this.tab() === 'assets' ? this.assetCategories() : this.financeCategories());

  model: CategoryInput = this.defaultModel();
  ruleModel: FinanceCategoryRuleInput = this.defaultRuleModel();
  rateModel: ExchangeRateInput = this.defaultRateModel();
  rateEffectiveDate = new Date().toISOString().substring(0, 10);
  passwordModel = { currentPassword: '', newPassword: '', confirmPassword: '' };

  constructor() {
    this.fetch();
    this.loadTwoFa();
  }

  setTab(tab: SettingsTab) {
    this.tab.set(tab);
    this.reset();
  }

  fetch() {
    this.loading.set(true);
    const asset$ = this.api.listAssetCategories();
    const finance$ = this.api.listFinanceCategories();
    const rules$ = this.api.listFinanceCategoryRules();
    const rates$ = this.api.listExchangeRates();
    asset$.subscribe({
      next: categories => {
        this.assetCategories.set(categories);
        finance$.subscribe({
          next: financeCategories => {
            this.financeCategories.set(financeCategories);
            rules$.subscribe({
              next: rules => {
                this.financeCategoryRules.set(rules);
                rates$.subscribe({
                  next: rates => {
                    this.exchangeRates.set(rates);
                    this.loading.set(false);
                  },
                  error: () => this.loading.set(false),
                });
              },
              error: () => this.loading.set(false),
            });
          },
          error: () => this.loading.set(false),
        });
      },
      error: () => this.loading.set(false),
    });
  }

  newCategory() {
    this.reset();
  }

  editCategory(category: EditableCategory) {
    this.editingId.set(category.id);
    this.error.set(null);
    this.model = { name: category.name, color: category.color, sortOrder: category.sortOrder };
  }

  save() {
    if (!this.model.name.trim()) {
      this.error.set('Category name is required.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const input = {
      ...this.model,
      name: this.model.name.trim(),
      color: this.model.color || '#7c3aed',
      sortOrder: Number(this.model.sortOrder) || 0,
    };
    const isUpdate = this.editingId() != null;
    const subject = this.tab() === 'assets' ? 'Asset category' : 'Finance category';
    const op = this.tab() === 'assets'
      ? (isUpdate
        ? this.api.updateAssetCategory(this.editingId()!, input)
        : this.api.createAssetCategory(input))
      : (isUpdate
        ? this.api.updateFinanceCategory(this.editingId()!, input)
        : this.api.createFinanceCategory(input));
    op.subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(`${subject} ${isUpdate ? 'updated' : 'created'}.`);
        this.reset();
        this.fetch();
      },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  async remove() {
    if (!this.editingId()) return;
    const label = this.tab() === 'assets' ? 'asset' : 'finance';
    const confirmed = await this.confirmDialog.confirm({
      title: `Delete ${label} category?`,
      message: `This ${label} category will be removed from the predefined dropdowns.`,
      detail: 'Existing records keep their category text.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    const subject = this.tab() === 'assets' ? 'Asset category' : 'Finance category';
    const op = this.tab() === 'assets'
      ? this.api.deleteAssetCategory(this.editingId()!)
      : this.api.deleteFinanceCategory(this.editingId()!);
    op.subscribe(() => {
      this.toast.success(`${subject} deleted.`);
      this.reset();
      this.fetch();
    });
  }

  reset() {
    this.editingId.set(null);
    this.error.set(null);
    this.model = this.defaultModel();
    this.editingRuleId.set(null);
    this.ruleModel = this.defaultRuleModel();
    this.editingRateId.set(null);
    this.rateModel = this.defaultRateModel();
    this.rateEffectiveDate = new Date().toISOString().substring(0, 10);
    this.passwordChanged.set(false);
    this.cancelTwoFaSetup();
    this.twoFaRecoveryCodes.set(null);
    this.twoFaPassword = '';
  }

  loadTwoFa() {
    this.authApi.twoFactorStatus().subscribe({
      next: status => this.twoFa.set(status),
      error: () => {},
    });
  }

  startTwoFaSetup() {
    this.twoFaBusy.set(true);
    this.twoFaError.set(null);
    this.authApi.twoFactorSetup().subscribe({
      next: async setup => {
        this.twoFaSetup.set(setup);
        try {
          const mod: any = await import('qrcode');
          const QRCode = mod.default ?? mod;
          this.twoFaQr.set(await QRCode.toDataURL(setup.otpauthUri, { margin: 1, width: 208 }));
        } catch {
          this.twoFaQr.set(null); // fall back to the manual secret if QR rendering fails
        }
        this.twoFaBusy.set(false);
      },
      error: e => { this.twoFaBusy.set(false); this.twoFaError.set(e?.error?.error ?? 'Could not start setup.'); },
    });
  }

  confirmTwoFa() {
    if (!this.twoFaCode.trim()) { this.twoFaError.set('Enter the 6-digit code.'); return; }
    this.twoFaBusy.set(true);
    this.twoFaError.set(null);
    this.authApi.twoFactorEnable(this.twoFaCode.trim()).subscribe({
      next: result => {
        this.twoFaBusy.set(false);
        this.twoFaSetup.set(null);
        this.twoFaQr.set(null);
        this.twoFaCode = '';
        this.twoFaRecoveryCodes.set(result.recoveryCodes);
        this.toast.success('Two-factor authentication enabled.');
        this.loadTwoFa();
      },
      error: e => { this.twoFaBusy.set(false); this.twoFaError.set(e?.error?.error ?? 'That code did not match.'); },
    });
  }

  cancelTwoFaSetup() {
    this.twoFaSetup.set(null);
    this.twoFaQr.set(null);
    this.twoFaCode = '';
    this.twoFaError.set(null);
  }

  disableTwoFa() {
    if (!this.twoFaPassword) { this.twoFaError.set('Enter your password to confirm.'); return; }
    this.twoFaBusy.set(true);
    this.twoFaError.set(null);
    this.authApi.twoFactorDisable(this.twoFaPassword).subscribe({
      next: status => {
        this.twoFaBusy.set(false);
        this.twoFaPassword = '';
        this.twoFa.set(status);
        this.toast.success('Two-factor authentication disabled.');
      },
      error: e => { this.twoFaBusy.set(false); this.twoFaError.set(e?.error?.error ?? 'Could not disable.'); },
    });
  }

  copyRecoveryCodes(codes: string[]) {
    navigator.clipboard?.writeText(codes.join('\n')).then(
      () => this.toast.success('Recovery codes copied.'),
      () => this.toast.error('Could not copy to clipboard.'),
    );
  }

  editRule(rule: FinanceCategoryRule) {
    this.editingRuleId.set(rule.id);
    this.error.set(null);
    this.ruleModel = {
      pattern: rule.pattern,
      category: rule.category,
      matchPayee: rule.matchPayee,
      matchDescription: rule.matchDescription,
      isActive: rule.isActive,
      priority: rule.priority,
    };
  }

  saveRule() {
    if (!this.ruleModel.pattern.trim() || !this.ruleModel.category.trim()) {
      this.error.set('Pattern and category are required.');
      return;
    }
    if (!this.ruleModel.matchPayee && !this.ruleModel.matchDescription) {
      this.error.set('Choose at least one field to match.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const input: FinanceCategoryRuleInput = {
      ...this.ruleModel,
      pattern: this.ruleModel.pattern.trim(),
      category: this.ruleModel.category.trim(),
      priority: Number(this.ruleModel.priority) || 0,
    };
    const isUpdate = this.editingRuleId() != null;
    const op = isUpdate
      ? this.api.updateFinanceCategoryRule(this.editingRuleId()!, input)
      : this.api.createFinanceCategoryRule(input);
    op.subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(isUpdate ? 'Rule updated.' : 'Rule created.');
        this.reset();
        this.fetch();
      },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  async removeRule() {
    if (!this.editingRuleId()) return;
    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete category rule?',
      message: 'This rule will stop auto-categorizing matching transactions.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.api.deleteFinanceCategoryRule(this.editingRuleId()!).subscribe(() => {
      this.toast.success('Rule deleted.');
      this.reset();
      this.fetch();
    });
  }

  editRate(rate: ExchangeRate) {
    this.editingRateId.set(rate.id);
    this.error.set(null);
    this.rateEffectiveDate = rate.effectiveDate.substring(0, 10);
    this.rateModel = { currency: rate.currency, effectiveDate: rate.effectiveDate, rateToBase: rate.rateToBase };
  }

  saveRate() {
    if (!this.rateModel.currency.trim() || Number(this.rateModel.rateToBase) <= 0) {
      this.error.set('Currency and a positive rate are required.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const input: ExchangeRateInput = {
      currency: this.rateModel.currency.trim().toUpperCase(),
      effectiveDate: new Date(this.rateEffectiveDate).toISOString(),
      rateToBase: Number(this.rateModel.rateToBase),
    };
    const isUpdate = this.editingRateId() != null;
    const op = isUpdate
      ? this.api.updateExchangeRate(this.editingRateId()!, input)
      : this.api.createExchangeRate(input);
    op.subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(isUpdate ? 'Exchange rate updated.' : 'Exchange rate added.');
        this.reset();
        this.fetch();
      },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  async removeRate() {
    if (!this.editingRateId()) return;
    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete exchange rate?',
      message: 'Amounts in this currency will fall back to 1:1 in aggregate CHF totals until a rate is added again.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.api.deleteExchangeRate(this.editingRateId()!).subscribe(() => {
      this.toast.success('Exchange rate deleted.');
      this.reset();
      this.fetch();
    });
  }

  changePassword() {
    if (this.passwordModel.newPassword.length < 8) {
      this.error.set('New password must be at least 8 characters.');
      return;
    }
    if (this.passwordModel.newPassword !== this.passwordModel.confirmPassword) {
      this.error.set('New password confirmation does not match.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.passwordChanged.set(false);
    this.auth.changePassword(this.passwordModel.currentPassword, this.passwordModel.newPassword).subscribe({
      next: () => {
        this.saving.set(false);
        this.passwordModel = { currentPassword: '', newPassword: '', confirmPassword: '' };
        this.passwordChanged.set(true);
        this.toast.success('Password changed.');
      },
      error: e => {
        this.saving.set(false);
        this.error.set(e?.error?.error ?? 'Password change failed.');
      },
    });
  }

  private defaultModel(): CategoryInput {
    return {
      name: '',
      color: '#7c3aed',
      sortOrder: this.tab() === 'assets' ? this.assetCategories().length : this.financeCategories().length,
    };
  }

  private defaultRuleModel(): FinanceCategoryRuleInput {
    return {
      pattern: '',
      category: this.financeCategories()[0]?.name ?? '',
      matchPayee: true,
      matchDescription: true,
      isActive: true,
      priority: this.financeCategoryRules().length,
    };
  }

  private defaultRateModel(): ExchangeRateInput {
    return { currency: '', effectiveDate: new Date().toISOString(), rateToBase: 1 };
  }
}
