import { Injectable, inject, signal } from '@angular/core';
import { NotificationsApi } from '../../core/data-access/notifications-api';

/// Singleton signal store so the sidebar bell and the notifications page stay
/// in sync without polling. Any mutation (mark-read, dismiss, scan) calls
/// `refresh()` to repull the counts.
@Injectable({ providedIn: 'root' })
export class NotificationCenterService {
  private api = inject(NotificationsApi);

  readonly unread = signal(0);
  readonly total = signal(0);

  refresh() {
    this.api.counts().subscribe({
      next: c => {
        this.unread.set(c.unread);
        this.total.set(c.total);
      },
      error: () => {/* swallow — sidebar should never break on a transient network blip */},
    });
  }
}
