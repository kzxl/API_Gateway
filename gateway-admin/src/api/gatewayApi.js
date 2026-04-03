import axios from "axios";

// Using relative path by default to allow Nginx reverse proxy to proxy API calls to Gateway backend
const API_BASE = import.meta.env.VITE_API_BASE || "http://192.168.19.79:8887";
const API_KEY = import.meta.env.VITE_API_KEY || "gw-admin-key-change-me";

const api = axios.create({
  baseURL: API_BASE,
  headers: { "X-Api-Key": API_KEY },
});

// Add Authorization header with access token
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// ── Auth (public, no API key needed) ──
const publicApi = axios.create({ baseURL: API_BASE });

export const login = (username, password) =>
  publicApi.post("/auth/login", { username, password });
export const validateToken = (token) =>
  publicApi.post("/auth/validate", { token });
export const refreshToken = (refreshToken) =>
  publicApi.post("/auth/refresh", { refreshToken });
export const logout = (refreshToken) =>
  api.post("/auth/logout", { refreshToken });

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
export const clearLogs = () => api.delete("/admin/logs");
export const getLogStats = () => api.get("/admin/logs/stats");

// ── Config Import/Export ──
export const exportConfig = () => api.get("/admin/config/export");
export const importConfig = (data) => api.post("/admin/config/import", data);
