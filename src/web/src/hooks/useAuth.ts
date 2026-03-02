import { useCallback, useEffect, useState } from 'react';
import type { ClientPrincipal } from '../types';
import { checkRegistration } from '../services/api';

interface AuthState {
  user: ClientPrincipal | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  isAdmin: boolean;
  isActive: boolean;
  isRegistered: boolean;
  login: (provider: string) => void;
  logout: () => void;
}

export function useAuth(): AuthState {
  const [user, setUser] = useState<ClientPrincipal | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isAdmin, setIsAdmin] = useState(false);
  const [isActive, setIsActive] = useState(true);
  const [isRegistered, setIsRegistered] = useState(false);

  useEffect(() => {
    fetch('/.auth/me')
      .then((res) => res.json())
      .then(async (data: { clientPrincipal: ClientPrincipal | null }) => {
        setUser(data.clientPrincipal);
        if (data.clientPrincipal) {
          // Check if user is a registered worker
          try {
            const worker = await checkRegistration();
            if (worker) {
              setIsRegistered(true);
              setIsAdmin(worker.isAdmin);
              setIsActive(worker.isActive);
            } else {
              setIsRegistered(false);
            }
          } catch {
            setIsRegistered(false);
          }
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
    isAdmin,
    isActive,
    isRegistered,
    login,
    logout,
  };
}
