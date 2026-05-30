export interface AuthResponse { token: string; username: string; }
export interface AuthStatus { hasUser: boolean; requiresSetupSecret: boolean; }

export interface TwoFactorStatus { enabled: boolean; recoveryCodesRemaining: number; }
export interface TwoFactorSetup { secret: string; otpauthUri: string; }
export interface TwoFactorEnableResult { recoveryCodes: string[]; }
