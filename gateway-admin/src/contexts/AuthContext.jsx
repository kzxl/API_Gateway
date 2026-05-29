import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { App as AntApp } from 'antd';
import { login as apiLogin, validateToken, setAuthFailureHandler } from '../api/gatewayApi';
import axios from 'axios';

const AuthContext = createContext(null);

// Get dynamic API base URL
const getApiBase = () => {
  return localStorage.getItem('apiBaseUrl') || 'http://localhost:8887';
};

/**
 * Auth Provider with JWT + Refresh Token support.
 * UArch: Zero-allocation state management, optimistic updates.
 */
export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const { message } = AntApp.useApp();

  const logout = useCallback(async () => {
    const refreshToken = localStorage.getItem('refreshToken');

    // Call logout endpoint (fire-and-forget)
    if (refreshToken) {
      try {
        await axios.post(
          `${getApiBase()}/auth/logout`,
          { refreshToken },
          {
            headers: {
              Authorization: `Bearer ${localStorage.getItem('accessToken')}`
            }
          }
        );
      } catch {
        // Ignore errors
      }
    }

    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    setUser(null);
  }, []);

  // Let the API layer tell us when auth is unrecoverable (refresh failed).
  useEffect(() => {
    setAuthFailureHandler(() => {
      // Only notify if the user was actually logged in (avoid noise on the
      // login screen where there's no session to expire).
      if (localStorage.getItem('accessToken')) {
        message.warning('Your session has expired. Please sign in again.');
      }
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
      setUser(null);
    });
    return () => setAuthFailureHandler(null);
  }, [message]);

  useEffect(() => {
    // Validate token on mount
    const accessToken = localStorage.getItem('accessToken');
    if (accessToken) {
      validateToken(accessToken)
        .then(res => {
          if (res.data.valid) {
            const claims = res.data.claims;
            const userId = claims.find(c => c.type.includes('nameidentifier'))?.value;
            const username = claims.find(c => c.type.includes('name') && !c.type.includes('identifier'))?.value;
            const role = claims.find(c => c.type.includes('role'))?.value;

            setUser({ id: parseInt(userId), username, role });
          } else {
            logout();
          }
        })
        .catch(() => logout())
        .finally(() => setLoading(false));
    } else {
      setLoading(false);
    }
  }, [logout]);

  const login = async (username, password) => {
    const res = await apiLogin(username, password);
    const { accessToken, refreshToken, user: userData } = res.data;

    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);

    setUser(userData);
    return userData;
  };

  return (
    <AuthContext.Provider value={{
      user,
      loading,
      login,
      logout,
      isAuthenticated: !!user
    }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
};
