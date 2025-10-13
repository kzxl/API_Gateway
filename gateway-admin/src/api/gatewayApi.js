import axios from "axios";

const api = axios.create({
  baseURL: "http://localhost:5000/api/gateway", // trỏ tới backend .NET
});

export const getRoutes = () => api.get("/routes");
export const saveRoute = (data) => api.post("/routes", data);
export const deleteRoute = (id) => api.delete(`/routes/${id}`);

export const getClusters = () => api.get("/clusters");
export const saveCluster = (data) => api.post("/clusters", data);
export const deleteCluster = (id) => api.delete(`/clusters/${id}`);
