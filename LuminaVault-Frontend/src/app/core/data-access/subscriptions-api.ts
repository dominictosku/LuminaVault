import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  DocumentAttachment,
  Subscription,
  SubscriptionGenerateDueResult,
  SubscriptionGenerateTransactionInput,
  SubscriptionGenerateTransactionResult,
  SubscriptionInput,
} from '../models';

@Injectable({ providedIn: 'root' })
export class SubscriptionsApi {
  private http = inject(HttpClient);

  listSubscriptions(includeInactive = false) {
    return this.http.get<Subscription[]>(`${API_BASE}/api/finance/subscriptions`, {
      params: includeInactive ? new HttpParams().set('includeInactive', true) : undefined,
    });
  }

  createSubscription(input: SubscriptionInput) {
    return this.http.post<Subscription>(`${API_BASE}/api/finance/subscriptions`, input);
  }

  updateSubscription(id: number, input: SubscriptionInput) {
    return this.http.put<Subscription>(`${API_BASE}/api/finance/subscriptions/${id}`, input);
  }

  deleteSubscription(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/subscriptions/${id}`);
  }

  generateSubscriptionTransaction(id: number, input: SubscriptionGenerateTransactionInput) {
    return this.http.post<SubscriptionGenerateTransactionResult>(
      `${API_BASE}/api/finance/subscriptions/${id}/generate-transaction`,
      input,
    );
  }

  generateDueSubscriptions(lookAheadDays?: number) {
    const params = lookAheadDays != null ? new HttpParams().set('lookAheadDays', lookAheadDays) : undefined;
    return this.http.post<SubscriptionGenerateDueResult>(
      `${API_BASE}/api/finance/subscriptions/generate-due`,
      {},
      { params },
    );
  }

  uploadSubscriptionAttachment(subscriptionId: number, file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<DocumentAttachment>(`${API_BASE}/api/finance/subscriptions/${subscriptionId}/attachments`, fd);
  }
}
