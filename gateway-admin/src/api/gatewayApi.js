import axios from "axios";

// Get API base URL from localStorage or use default
const getApiBase = () => {
  return localStorage.getItem('apiBaseUrl') || "http://localhost:8887";
};

const API_KEY = import.meta.env.VITE_API_KEY || "gw-admin-key-change-me";

// Optional callback invoked when authentication can no longer be recovered
// (refresh failed / no refresh token). AuthContext registers this to log out.
let onAuthFailure = null;
export const setAuthFailureHandler = (fn) => {
  onAuthFailure = fn;
};

// ── Single-flight refresh ──
// While a refresh is in progress, queued requests wait on this promise instead
// of each firing their own /auth/refresh call (which would cause a thundering
// herd and rotate the refresh token multiple times).
let refreshPromise = null;

const doRefresh = async () => {
  const storedRefresh = localStorage.getItem('refreshToken');
  if (!storedRefresh) {
    throw new Error('No refresh token');
  }

  // Use a bare axios call (no interceptors) to avoid recursion.
  const res = await axios.post(`${getApiBase()}/auth/refresh`, {
    refreshToken: storedRefresh,
  });

  const { accessToken, refreshToken: newRefresh } = res.data;
  if (!accessToken) {
    throw new Error('Refresh response missing accessToken');
  }

  localStorage.setItem('accessToken', accessToken);
  if (newRefresh) {
    localStorage.setItem('refreshToken', newRefresh);
  }
  return accessToken;
};

// Create axios instance with dynamic baseURL
const createApiInstance = () => {
  const instance = axios.create({
    baseURL: getApiBase(),
    headers: { "X-Api-Key": API_KEY },
  });

  // Add Authorization header with access token
  instance.interceptors.request.use((config) => {
    const token = localStorage.getItem('accessToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  });

  // Auto-refresh on expired/unauthorized responses (401 or 403), once per request.
  instance.interceptors.response.use(
    (response) => response,
    async (error) => {
      const originalRequest = error.config;
      const status = error.response?.status;

      const isAuthError = status === 401 || status === 403;
      // Never try to refresh the refresh call itself.
      const isRefreshCall = originalRequest?.url?.includes('/auth/refresh');

      if (!isAuthError || isRefreshCall || originalRequest?._retry) {
        return Promise.reject(error);
      }

      if (!localStorage.getItem('refreshToken')) {
        if (onAuthFailure) onAuthFailure();
        return Promise.reject(error);
      }

      originalRequest._retry = true;

      try {
        // Share one in-flight refresh across all queued requests.
        if (!refreshPromise) {
          refreshPromise = doRefresh().finally(() => {
            refreshPromise = null;
          });
        }
        const newAccessToken = await refreshPromise;

        originalRequest.headers = originalRequest.headers || {};
        originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
        return instance(originalRequest);
      } catch (refreshError) {
        if (onAuthFailure) onAuthFailure();
        return Promise.reject(refreshError);
      }
    }
  );

  return instance;
};

let api = createApiInstance();
let publicApi = axios.create({ baseURL: getApiBase() });

// Function to update API base URL
export const setApiBaseUrl = (url) => {
  localStorage.setItem('apiBaseUrl', url);
  api = createApiInstance();
  publicApi = axios.create({ baseURL: url });
};

// ── Auth (public, no API key needed) ──
export const login = (username, password) =>
  publicApi.post("/auth/login", { username, password });
export const validateToken = (token) =>
  publicApi.post("/auth/validate", { token });
export const refreshToken = (refreshTokenValue) =>
  publicApi.post("/auth/refresh", { refreshToken: refreshTokenValue });
export const logout = (refreshTokenValue) =>
  api.post("/auth/logout", { refreshToken: refreshTokenValue });

// ── Routes ──
export const getRoutes = () => api.get("/admin/routes");
export const getRouteById = (id) => api.get(`/admin/routes/${id}`);
export const createRoute = (data) => api.post("/admin/routes", data);
export const updateRoute = (id, data) => api.put(`/admin/routes/${id}`, data);
export const deleteRoute = (id) => api.delete(`/admin/routes/${id}`);

// ── Clusters ──
export const getClusters = () => api.get("/admin/clusters");
export const getClusterById = (id) => api.get(`/admin/clusters/${id}`);
export const createCluster = (data) => api.post("/admin/clusters", data);
export const updateCluster = (id, data) =>
  api.put(`/admin/clusters/${id}`, data);
export const deleteCluster = (id) => api.delete(`/admin/clusters/${id}`);

// ── Health ──
export const getHealth = () => api.get("/admin/health");

// ── Metrics ──
export const getMetrics = () => api.get("/admin/metrics");
export const resetMetrics = () => api.delete("/admin/metrics");

// ── Users ──
export const getUsers = () => api.get("/admin/users");
export const createUser = (data) => api.post("/admin/users", data);
export const updateUser = (id, data) => api.put(`/admin/users/${id}`, data);
export const deleteUser = (id) => api.delete(`/admin/users/${id}`);

// ── Logs ──
export const getLogs = (params) => api.get("/admin/logs", { params });
export const clearLogs = (olderThanDays) =>
  api.delete("/admin/logs", {
    params: olderThanDays != null ? { olderThanDays } : undefined,
  });
export const getLogStats = () => api.get("/admin/logs/stats");

// ── Config Import/Export ──
export const exportConfig = () => api.get("/admin/config/export");
export const importConfig = (data) => api.post("/admin/config/import", data);
