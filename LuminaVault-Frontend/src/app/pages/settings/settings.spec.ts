import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { SERVER_URL_STORAGE_KEY } from '../../core/api-base';
import { AuthService } from '../../core/auth.service';
import { AuthApi } from '../../core/data-access/auth-api';
import { SettingsApi } from '../../core/data-access/settings-api';
import { DesktopNotificationService } from '../../core/desktop-notification.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';
import { SettingsComponent } from './settings';

describe('SettingsComponent - Server Connection & Notifications', () => {
  let toastSuccess: any;
  let toastInfo: any;
  let sendNotificationSpy: any;
  let requestPermissionSpy: any;

  function create() {
    toastSuccess = vi.fn();
    toastInfo = vi.fn();
    sendNotificationSpy = vi.fn().mockResolvedValue(true);
    requestPermissionSpy = vi.fn().mockResolvedValue(true);

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: SettingsApi,
          useValue: {
            listAssetCategories: () => of([]),
            listFinanceCategories: () => of([]),
            listFinanceCategoryRules: () => of([]),
            listExchangeRates: () => of([]),
          },
        },
        {
          provide: AuthService,
          useValue: { changePassword: vi.fn() },
        },
        {
          provide: AuthApi,
          useValue: {
            twoFactorStatus: () => of({ enabled: false, hasAuthenticator: false, recoveryCodesRemaining: 0 }),
          },
        },
        {
          provide: ConfirmDialogService,
          useValue: { confirm: vi.fn() },
        },
        {
          provide: ToastService,
          useValue: { success: toastSuccess, info: toastInfo, error: vi.fn() },
        },
        {
          provide: DesktopNotificationService,
          useValue: {
            isSupported: () => true,
            isPermissionGranted: vi.fn().mockResolvedValue(true),
            requestPermission: requestPermissionSpy,
            send: sendNotificationSpy,
          },
        },
      ],
    });

    return TestBed.runInInjectionContext(() => new SettingsComponent());
  }

  beforeEach(() => {
    localStorage.clear();
  });

  it('switches to connection tab and shows correct title', () => {
    const component = create();
    component.setTab('connection');

    expect(component.tab()).toBe('connection');
    expect(component.title()).toBe('Server & Sync Connection');
  });

  it('saves server URL to localStorage and displays success toast', () => {
    const component = create();
    component.serverUrlInput.set('https://nas.internal:5256');
    component.saveServer();

    expect(localStorage.getItem(SERVER_URL_STORAGE_KEY)).toBe('https://nas.internal:5256');
    expect(toastSuccess).toHaveBeenCalledWith('Server connection URL updated.');
  });

  it('resets server URL override and notifies user', () => {
    localStorage.setItem(SERVER_URL_STORAGE_KEY, 'https://old.url');
    const component = create();

    component.resetServer();

    expect(localStorage.getItem(SERVER_URL_STORAGE_KEY)).toBeNull();
    expect(component.serverUrlInput()).toBe('');
    expect(toastInfo).toHaveBeenCalledWith('Server URL reset to default.');
  });

  it('requests notification permission and updates status', async () => {
    const component = create();
    await component.requestNotificationPermission();

    expect(requestPermissionSpy).toHaveBeenCalled();
    expect(component.notificationPermission()).toBe(true);
    expect(toastSuccess).toHaveBeenCalledWith('Desktop notification permission granted.');
  });

  it('sends test desktop notification', async () => {
    const component = create();
    await component.sendTestNotification();

    expect(sendNotificationSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'LuminaVault Test',
      }),
    );
    expect(toastSuccess).toHaveBeenCalledWith('Test notification sent.');
  });
});
