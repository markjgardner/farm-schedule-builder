import { useCallback, useEffect, useState } from 'react';
import type { ClientPrincipal } from '../types';
import { registerWorker } from '../services/api';

interface AuthState {
  user: ClientPrincipal | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (provider: string) => void;
  logout: () => void;
}

export function useAuth(): AuthState {
  const [user, setUser] = useState<ClientPrincipal | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    fetch('/.auth/me')
      .then((res) => res.json())
      .then((data: { clientPrincipal: ClientPrincipal | null }) => {
        setUser(data.clientPrincipal);
        // Auto-register as worker on first login (fire-and-forget)
        if (data.clientPrincipal) {
          registerWorker().catch(() => {
            // Ignore errors — registration is best-effort
          });
        }
      })
      .catch(() => {
        setUser(null);
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, []);

  const login = useCallback((provider: string) => {
    window.location.href = `/.auth/login/${provider}?post_login_redirect_uri=/`;
  }, []);

  const logout = useCallback(() => {
    window.location.href = '/.auth/logout';
  }, []);

  return {
    user,
    isAuthenticated: user !== null,
    isLoading,
    login,
    logout,
  };
}
