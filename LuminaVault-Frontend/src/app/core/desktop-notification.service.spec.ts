import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { DesktopNotificationService } from './desktop-notification.service';

describe('DesktopNotificationService', () => {
  let service: DesktopNotificationService;

  beforeEach(() => {
    service = new DesktopNotificationService();
    delete (window as any).__TAURI_INTERNALS__;
    delete (window as any).__TAURI__;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('checks support based on browser Notification or Tauri environment', () => {
    // In jsdom, Notification is defined or can be defined
    expect(typeof service.isSupported()).toBe('boolean');

    (window as any).__TAURI_INTERNALS__ = {};
    expect(service.isSupported()).toBe(true);
  });

  it('reports granted status in browser when Notification.permission is granted', async () => {
    Object.defineProperty(window, 'Notification', {
      writable: true,
      value: {
        permission: 'granted',
        requestPermission: vi.fn().mockResolvedValue('granted'),
      },
    });

    const granted = await service.isPermissionGranted();
    expect(granted).toBe(true);
  });

  it('requests permission using Notification.requestPermission in browser', async () => {
    const requestSpy = vi.fn().mockResolvedValue('granted');
    Object.defineProperty(window, 'Notification', {
      writable: true,
      value: {
        permission: 'default',
        requestPermission: requestSpy,
      },
    });

    const granted = await service.requestPermission();
    expect(granted).toBe(true);
    expect(requestSpy).toHaveBeenCalled();
  });

  it('sends browser Notification when permission is granted', async () => {
    const notificationConstructor = vi.fn();
    Object.defineProperty(window, 'Notification', {
      writable: true,
      value: Object.assign(notificationConstructor, {
        permission: 'granted',
        requestPermission: vi.fn().mockResolvedValue('granted'),
      }),
    });

    const sent = await service.send({
      title: 'Battery Check',
      body: 'Item needs charging',
    });

    expect(sent).toBe(true);
    expect(notificationConstructor).toHaveBeenCalledWith('Battery Check', {
      body: 'Item needs charging',
      icon: '/favicon.ico',
    });
  });
});
