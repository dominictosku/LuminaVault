export type NotificationSeverity = 'Info' | 'Warning' | 'Critical';
export type NotificationStatus = 'Unread' | 'Read' | 'Dismissed';

export interface Notification {
  id: number;
  type: string;
  severity: NotificationSeverity;
  title: string;
  message: string;
  link?: string | null;
  source: string;
  status: NotificationStatus;
  createdAt: string;
  readAt?: string | null;
  dismissedAt?: string | null;
}

export interface NotificationCounts {
  unread: number;
  total: number;
}

export interface NotificationScanResult {
  touched: number;
  scannedAt: string;
}
