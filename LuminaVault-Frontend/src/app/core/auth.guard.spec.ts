import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  function setup(isAuthenticated: boolean) {
    const navigate = vi.fn();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { isAuthenticated: () => isAuthenticated } },
        { provide: Router, useValue: { navigate } },
      ],
    });
    return { navigate };
  }

  const run = () =>
    TestBed.runInInjectionContext(() =>
      (authGuard as CanActivateFn)({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));

  it('allows navigation when authenticated', () => {
    const { navigate } = setup(true);
    expect(run()).toBe(true);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('blocks and redirects to /login when not authenticated', () => {
    const { navigate } = setup(false);
    expect(run()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
});
