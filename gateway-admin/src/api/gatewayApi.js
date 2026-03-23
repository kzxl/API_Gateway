import axios from "axios";

const API_BASE = "http://localhost:5151";
const API_KEY = "gw-admin-key-change-me";

const api = axios.create({
  baseURL: API_BASE,
  headers: { "X-Api-Key": API_KEY },
});

// ── Auth (public, no API key needed) ──
const publicApi = axios.create({ baseURL: API_BASE });

export const login = (username, password) =>
  publicApi.post("/auth/login", { username, password });
export const validateToken = (token) =>
  publicApi.post("/auth/validate", { token });

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
