import { Injectable } from '@angular/core';
import { isTauriEnvironment } from './api-base';

export interface DesktopNotificationPayload {
  title: string;
  body: string;
  id?: number;
}

@Injectable({ providedIn: 'root' })
export class DesktopNotificationService {
  /**
   * Checks if native notifications are supported in current environment.
   */
  isSupported(): boolean {
    if (isTauriEnvironment()) return true;
    return typeof window !== 'undefined' && 'Notification' in window;
  }

  /**
   * Checks if notification permission is currently granted.
   */
  async isPermissionGranted(): Promise<boolean> {
    if (isTauriEnvironment()) {
      try {
        const { isPermissionGranted } = await import('@tauri-apps/plugin-notification');
        return await isPermissionGranted();
      } catch {
        return false;
      }
    }

    if (typeof window !== 'undefined' && 'Notification' in window) {
      return Notification.permission === 'granted';
    }

    return false;
  }

  /**
   * Requests permission to show desktop notifications.
   */
  async requestPermission(): Promise<boolean> {
    if (isTauriEnvironment()) {
      try {
        const { requestPermission, isPermissionGranted } = await import('@tauri-apps/plugin-notification');
        const res = await requestPermission();
        return res === 'granted' || (await isPermissionGranted());
      } catch {
        return false;
      }
    }

    if (typeof window !== 'undefined' && 'Notification' in window) {
      try {
        const permission = await Notification.requestPermission();
        return permission === 'granted';
      } catch {
        return false;
      }
    }

    return false;
  }

  /**
   * Dispatches a native desktop notification.
   */
  async send(payload: DesktopNotificationPayload): Promise<boolean> {
    if (!this.isSupported()) {
      return false;
    }

    if (isTauriEnvironment()) {
      try {
        const { sendNotification, isPermissionGranted, requestPermission } = await import(
          '@tauri-apps/plugin-notification'
        );
        let granted = await isPermissionGranted();
        if (!granted) {
          const res = await requestPermission();
          granted = res === 'granted' || (await isPermissionGranted());
        }
        if (granted) {
          sendNotification({
            title: payload.title,
            body: payload.body,
            id: payload.id,
          });
          return true;
        }
        return false;
      } catch {
        return false;
      }
    }

    if (typeof window !== 'undefined' && 'Notification' in window) {
      try {
        if (Notification.permission === 'default') {
          await Notification.requestPermission();
        }
        if (Notification.permission === 'granted') {
          new Notification(payload.title, {
            body: payload.body,
            icon: '/favicon.ico',
          });
          return true;
        }
      } catch {
        return false;
      }
    }

    return false;
  }
}
