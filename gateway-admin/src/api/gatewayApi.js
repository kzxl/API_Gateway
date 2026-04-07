import axios from "axios";

// Get API base URL from localStorage or use default
const getApiBase = () => {
  return localStorage.getItem('apiBaseUrl') || "http://localhost:8887";
};

const API_KEY = import.meta.env.VITE_API_KEY || "gw-admin-key-change-me";

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
