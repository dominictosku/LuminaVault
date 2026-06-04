import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { DocumentAttachment } from '../models';

/// Generic document-attachment helpers shared by inventory items and finance
/// subscriptions. Uploads are owner-specific (see InventoryApi / SubscriptionsApi),
/// but deleting and resolving a stored attachment's URL is identical everywhere.
@Injectable({ providedIn: 'root' })
export class AttachmentsApi {
  private http = inject(HttpClient);

  deleteAttachment(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/attachments/${id}`);
  }

  attachmentUrl(attachment: DocumentAttachment) {
    return `${API_BASE}${attachment.url}`;
  }
}
