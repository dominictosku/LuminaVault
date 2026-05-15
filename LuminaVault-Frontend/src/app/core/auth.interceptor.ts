import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { ToastService } from '../shared/toast/toast.service';
import { AuthService } from './auth.service';

/// Attaches the JWT, then translates HTTP failure modes into user-facing toasts
/// while still letting the call site handle its own errors (we re-throw):
///   - 401 on non-/auth routes → boot to login (token expired or revoked)
///   - 5xx → toast the server message if present, else a generic one
///   - status 0 → network unreachable / CORS / aborted; toast a hint
///   - 408 / 504 → request timed out
/// 4xx other than 401 (validation, conflicts, not-found) are left for the page
/// to render inline — those are part of the normal flow, not surprises.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const toasts = inject(ToastService);
  const token = auth.token();

  const authReq = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(authReq).pipe(
    catchError(err => {
      if (err instanceof HttpErrorResponse) {
        if (err.status === 401 && !req.url.includes('/auth/')) {
          auth.logout();
          router.navigate(['/login']);
        } else if (err.status === 0) {
          toasts.error('Cannot reach the server. Check that the backend is running.');
        } else if (err.status === 408 || err.status === 504) {
          toasts.warning('The request took too long. Try again or with smaller input.');
        } else if (err.status >= 500) {
          const detail = typeof err.error?.error === 'string' ? err.error.error : null;
          toasts.error(detail ?? `Server error (${err.status}). Check the logs.`);
        }
      }
      return throwError(() => err);
    })
  );
};
