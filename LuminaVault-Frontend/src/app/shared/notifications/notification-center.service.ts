import { Injectable, inject, signal } from '@angular/core';
import { NotificationsApi } from '../../core/data-access/notifications-api';
import { DesktopNotificationService } from '../../core/desktop-notification.service';

/// Singleton signal store so the sidebar bell and the notifications page stay
/// in sync without polling. Any mutation (mark-read, dismiss, scan) calls
/// `refresh()` to repull the counts.
@Injectable({ providedIn: 'root' })
export class NotificationCenterService {
  private api = inject(NotificationsApi);
  private desktopNotification = inject(DesktopNotificationService);

  readonly unread = signal(0);
  readonly total = signal(0);
  private lastKnownUnread: number | null = null;

  refresh() {
    this.api.counts().subscribe({
      next: c => {
        if (this.lastKnownUnread !== null && c.unread > this.lastKnownUnread) {
          const diff = c.unread - this.lastKnownUnread;
          this.desktopNotification.send({
            title: 'LuminaVault Alert',
            body: diff === 1 ? 'You have 1 new unread alert.' : `You have ${diff} new unread alerts.`,
          });
        }
        this.lastKnownUnread = c.unread;
        this.unread.set(c.unread);
        this.total.set(c.total);
      },
      error: () => {/* swallow — sidebar should never break on a transient network blip */},
    });
  }
}
