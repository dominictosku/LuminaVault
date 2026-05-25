import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ProtectedMediaService {
  private http = inject(HttpClient);

  objectUrl(url: string): Observable<string> {
    return this.http.get(url, { responseType: 'blob' }).pipe(
      map(blob => URL.createObjectURL(blob)),
    );
  }

  open(url: string) {
    const popup = window.open('', '_blank', 'noopener,noreferrer');
    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: blob => {
        const objectUrl = URL.createObjectURL(blob);
        if (popup) popup.location.href = objectUrl;
        else window.open(objectUrl, '_blank', 'noopener,noreferrer');
        window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
      },
      error: () => popup?.close(),
    });
  }
}
