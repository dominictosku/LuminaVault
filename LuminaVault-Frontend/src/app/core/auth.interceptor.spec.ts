import { HttpErrorResponse, HttpHandlerFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { ToastService } from '../shared/toast/toast.service';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  function setup(token: string | null) {
    const logout = vi.fn();
    const navigate = vi.fn();
    const error = vi.fn();
    const warning = vi.fn();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { token: () => token, logout } },
        { provide: Router, useValue: { navigate } },
        { provide: ToastService, useValue: { error, warning } },
      ],
    });
    return { logout, navigate, error, warning };
  }

  function run(req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<unknown> {
    return TestBed.runInInjectionContext(() => authInterceptor(req, next));
  }

  // A handler that fails with the given HTTP error; mirrors the server rejecting the call.
  const failWith = (init: { status: number; url?: string; error?: unknown }): HttpHandlerFn =>
    () => throwError(() => new HttpErrorResponse(init));

  it('attaches the bearer token to outgoing requests', () => {
    setup('jwt-123');
    let forwarded: HttpRequest<unknown> | undefined;
    const next: HttpHandlerFn = req => {
      forwarded = req;
      return of(new HttpResponse({ status: 200 }));
    };

    run(new HttpRequest('GET', '/api/items'), next).subscribe();

    expect(forwarded!.headers.get('Authorization')).toBe('Bearer jwt-123');
  });

  it('leaves the request unauthenticated when there is no token', () => {
    setup(null);
    let forwarded: HttpRequest<unknown> | undefined;
    const next: HttpHandlerFn = req => {
      forwarded = req;
      return of(new HttpResponse({ status: 200 }));
    };

    run(new HttpRequest('GET', '/api/items'), next).subscribe();

    expect(forwarded!.headers.has('Authorization')).toBe(false);
  });

  it('logs out and redirects to /login on 401 from a non-auth route', () => {
    const { logout, navigate } = setup('jwt-123');

    run(new HttpRequest('GET', '/api/finance/accounts'),
      failWith({ status: 401, url: '/api/finance/accounts' }))
      .subscribe({ error: () => {} });

    expect(logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('does not log out on 401 from the auth endpoints (a bad login is shown inline)', () => {
    const { logout, navigate } = setup(null);

    run(new HttpRequest('POST', '/api/auth/login', {}),
      failWith({ status: 401, url: '/api/auth/login' }))
      .subscribe({ error: () => {} });

    expect(logout).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('toasts a hint when the server is unreachable (status 0)', () => {
    const { error } = setup(null);

    run(new HttpRequest('GET', '/api/items'), failWith({ status: 0 }))
      .subscribe({ error: () => {} });

    expect(error).toHaveBeenCalledOnce();
  });

  it('surfaces the server-provided message on 5xx', () => {
    const { error } = setup('jwt-123');

    run(new HttpRequest('GET', '/api/items'),
      failWith({ status: 500, url: '/api/items', error: { error: 'boom' } }))
      .subscribe({ error: () => {} });

    expect(error).toHaveBeenCalledWith('boom');
  });

  it('warns on a request timeout (504)', () => {
    const { warning } = setup('jwt-123');

    run(new HttpRequest('GET', '/api/items'), failWith({ status: 504, url: '/api/items' }))
      .subscribe({ error: () => {} });

    expect(warning).toHaveBeenCalledOnce();
  });

  it('re-throws so the calling page can still handle the error', () => {
    setup('jwt-123');
    const onError = vi.fn();

    run(new HttpRequest('GET', '/api/items'), failWith({ status: 400, url: '/api/items' }))
      .subscribe({ error: onError });

    expect(onError).toHaveBeenCalledOnce();
  });
});
