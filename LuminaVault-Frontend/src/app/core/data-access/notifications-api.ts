import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  Notification,
  NotificationCounts,
  NotificationScanResult,
  NotificationStatus,
} from '../models';

@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private http = inject(HttpClient);

  list(opts: { status?: NotificationStatus; limit?: number } = {}) {
    let params = new HttpParams();
    if (opts.status) params = params.set('status', opts.status);
    if (opts.limit != null) params = params.set('limit', opts.limit);
    return this.http.get<Notification[]>(`${API_BASE}/api/notifications`, { params });
  }

  counts() {
    return this.http.get<NotificationCounts>(`${API_BASE}/api/notifications/counts`);
  }

  markRead(id: number) {
    return this.http.post<Notification>(`${API_BASE}/api/notifications/${id}/read`, {});
  }

  dismiss(id: number) {
    return this.http.post<Notification>(`${API_BASE}/api/notifications/${id}/dismiss`, {});
  }

  markAllRead() {
    return this.http.post<{ updated: number }>(`${API_BASE}/api/notifications/mark-all-read`, {});
  }

  remove(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/notifications/${id}`);
  }

  scan() {
    return this.http.post<NotificationScanResult>(`${API_BASE}/api/notifications/scan`, {});
  }
}
