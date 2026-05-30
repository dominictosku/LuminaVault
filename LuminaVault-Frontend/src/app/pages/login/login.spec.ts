import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth.service';
import { AuthApi } from '../../core/data-access/auth-api';
import { LoginComponent } from './login';

describe('LoginComponent two-factor challenge', () => {
  function create() {
    const login = vi.fn();
    const navigate = vi.fn();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { login, register: vi.fn() } },
        { provide: AuthApi, useValue: { authStatus: () => of({ hasUser: true, requiresSetupSecret: false }) } },
        { provide: Router, useValue: { navigate } },
      ],
    });
    // runInInjectionContext exercises the component's logic without rendering its template.
    const component = TestBed.runInInjectionContext(() => new LoginComponent());
    return { component, login, navigate };
  }

  const challenge = () =>
    throwError(() => new HttpErrorResponse({ status: 401, error: { twoFactorRequired: true } }));

  it('switches to the code step when the backend reports twoFactorRequired', () => {
    const { component, login, navigate } = create();
    login.mockReturnValue(challenge());
    component.username = 'tester';
    component.password = 'password123';

    component.submit();

    expect(component.step()).toBe('twoFactor');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('submits the entered TOTP code on the second attempt and navigates on success', () => {
    const { component, login, navigate } = create();
    component.username = 'tester';
    component.password = 'password123';

    login.mockReturnValueOnce(challenge());
    component.submit(); // password step -> challenge

    login.mockReturnValueOnce(of({ token: 't', username: 'tester' }));
    component.totpCode = '123456';
    component.submit(); // code step -> success

    expect(login).toHaveBeenLastCalledWith('tester', 'password123', '123456', undefined);
    expect(navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('sends a recovery code instead when that option is chosen', () => {
    const { component, login } = create();
    component.username = 'tester';
    component.password = 'password123';
    component.step.set('twoFactor');
    component.useRecovery.set(true);
    component.recoveryCode = 'ABCDE-FGHIJ';
    login.mockReturnValue(of({ token: 't', username: 'tester' }));

    component.submit();

    expect(login).toHaveBeenLastCalledWith('tester', 'password123', undefined, 'ABCDE-FGHIJ');
  });

  it('shows an invalid-credentials error for a non-2FA 401', () => {
    const { component, login } = create();
    login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401, error: {} })));
    component.username = 'tester';
    component.password = 'wrong';

    component.submit();

    expect(component.step()).toBe('credentials');
    expect(component.error()).toBe('Invalid credentials.');
  });
});
