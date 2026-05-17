import { DatePipe, NgClass } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NotificationsApi } from '../../core/data-access/notifications-api';
import { Notification, NotificationSeverity, NotificationStatus } from '../../core/models';
import { NotificationCenterService } from '../../shared/notifications/notification-center.service';
import { ToastService } from '../../shared/toast/toast.service';

type Filter = 'unread' | 'all' | 'dismissed';

@Component({
  selector: 'app-notifications',
  imports: [DatePipe, NgClass],
  templateUrl: './notifications.html',
})
export class NotificationsComponent {
  private api = inject(NotificationsApi);
  private center = inject(NotificationCenterService);
  private router = inject(Router);
  private toast = inject(ToastService);

  items = signal<Notification[]>([]);
  loading = signal(true);
  scanning = signal(false);
  filter = signal<Filter>('unread');

  filteredItems = computed(() => {
    const f = this.filter();
    const list = this.items();
    if (f === 'dismissed') return list.filter(n => n.status === 'Dismissed');
    if (f === 'unread') return list.filter(n => n.status === 'Unread');
    return list.filter(n => n.status !== 'Dismissed');
  });

  unreadCount = computed(() => this.items().filter(n => n.status === 'Unread').length);

  constructor() {
    this.fetch();
  }

  fetch() {
    this.loading.set(true);
    // Pull both active and dismissed so the filter pills can switch without re-fetching.
    this.api.list({ limit: 500 }).subscribe({
      next: rows => {
        this.api.list({ status: 'Dismissed', limit: 200 }).subscribe({
          next: dismissed => {
            const merged = [...rows, ...dismissed];
            // De-dup if the unfiltered call ever started returning dismissed too.
            const seen = new Set<number>();
            this.items.set(merged.filter(n => !seen.has(n.id) && seen.add(n.id)));
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
      },
      error: () => this.loading.set(false),
    });
  }

  setFilter(f: Filter) {
    this.filter.set(f);
  }

  open(n: Notification) {
    if (n.status === 'Unread') {
      this.api.markRead(n.id).subscribe(updated => {
        this.replace(updated);
        this.center.refresh();
      });
    }
    if (n.link) this.router.navigateByUrl(n.link);
  }

  dismiss(n: Notification, event: MouseEvent) {
    event.stopPropagation();
    this.api.dismiss(n.id).subscribe(updated => {
      this.replace(updated);
      this.center.refresh();
    });
  }

  remove(n: Notification, event: MouseEvent) {
    event.stopPropagation();
    this.api.remove(n.id).subscribe(() => {
      this.items.update(list => list.filter(item => item.id !== n.id));
      this.toast.success('Notification removed.');
      this.center.refresh();
    });
  }

  markAllRead() {
    this.api.markAllRead().subscribe(() => {
      this.toast.success('All notifications marked as read.');
      this.fetch();
      this.center.refresh();
    });
  }

  scan() {
    this.scanning.set(true);
    this.api.scan().subscribe({
      next: () => {
        this.scanning.set(false);
        this.toast.success('Rescan complete.');
        this.fetch();
        this.center.refresh();
      },
      error: () => this.scanning.set(false),
    });
  }

  toneClasses(severity: NotificationSeverity): Record<string, boolean> {
    return {
      'border-sky-500/30 bg-sky-500/5': severity === 'Info',
      'border-amber-500/40 bg-amber-500/5': severity === 'Warning',
      'border-red-500/40 bg-red-500/5': severity === 'Critical',
    };
  }

  severityIcon(severity: NotificationSeverity): string {
    return severity === 'Critical' ? 'pi-exclamation-triangle text-red-300'
         : severity === 'Warning' ? 'pi-exclamation-circle text-amber-300'
         : 'pi-info-circle text-sky-300';
  }

  isUnread(s: NotificationStatus) { return s === 'Unread'; }

  private replace(updated: Notification) {
    this.items.update(list => list.map(n => n.id === updated.id ? updated : n));
  }
}
