import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { authApi } from '../api/auth';
import { TOKEN_KEY } from '../api/client';
import type { AuthUser } from '../types/api';

interface AuthContextValue { user: AuthUser | null; login: (email: string, password: string) => Promise<void>; logout: () => void }
const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const stored = localStorage.getItem('supportdesk.user');
    return stored ? (JSON.parse(stored) as AuthUser) : null;
  });
  const logout = () => { localStorage.removeItem(TOKEN_KEY); localStorage.removeItem('supportdesk.user'); setUser(null); };
  useEffect(() => { const handler = () => logout(); window.addEventListener('supportdesk:unauthorized', handler); return () => window.removeEventListener('supportdesk:unauthorized', handler); }, []);
  const value = useMemo<AuthContextValue>(() => ({ user, logout, login: async (email, password) => { const result = await authApi.login(email, password); localStorage.setItem(TOKEN_KEY, result.token); localStorage.setItem('supportdesk.user', JSON.stringify(result.user)); setUser(result.user); } }), [user]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() { const context = useContext(AuthContext); if (!context) throw new Error('useAuth must be used inside AuthProvider.'); return context; }
