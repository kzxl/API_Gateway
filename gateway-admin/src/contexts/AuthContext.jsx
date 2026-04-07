import React, { createContext, useContext, useState, useEffect } from 'react';
import { login as apiLogin, validateToken } from '../api/gatewayApi';
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
  }, []);

  // Setup axios interceptor for automatic token refresh
  useEffect(() => {
    const interceptor = axios.interceptors.response.use(
      response => response,
      async error => {
        const originalRequest = error.config;

        // If 401 and not already retried, try to refresh token
        if (error.response?.status === 401 && !originalRequest._retry) {
          originalRequest._retry = true;

          const refreshToken = localStorage.getItem('refreshToken');
          if (refreshToken) {
            try {
              const res = await axios.post(
                `${getApiBase()}/auth/refresh`,
                { refreshToken }
              );

              const { accessToken: newAccessToken, refreshToken: newRefreshToken } = res.data;

              localStorage.setItem('accessToken', newAccessToken);
              localStorage.setItem('refreshToken', newRefreshToken);

              // Retry original request with new token
              originalRequest.headers['Authorization'] = `Bearer ${newAccessToken}`;
              return axios(originalRequest);
            } catch (refreshError) {
              // Refresh failed, logout user
              logout();
              return Promise.reject(refreshError);
            }
          }
        }

        return Promise.reject(error);
      }
    );

    return () => {
      axios.interceptors.response.eject(interceptor);
    };
  }, []);

  const login = async (username, password) => {
    const res = await apiLogin(username, password);
    const { accessToken, refreshToken, user: userData } = res.data;

    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);

    setUser(userData);
    return userData;
  };

  const logout = async () => {
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
