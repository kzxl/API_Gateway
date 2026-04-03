import React from "react";
import { Layout, Menu, Typography, App as AntApp, Button, Dropdown, Avatar, Badge, Space } from "antd";
import {
  BrowserRouter, Routes, Route, Link, useLocation, Navigate,
} from "react-router-dom";
import {
  DashboardOutlined, ApiOutlined, ClusterOutlined, BarChartOutlined,
  FileTextOutlined, UserOutlined, SettingOutlined, LogoutOutlined, BellOutlined,
  SafetyOutlined,
} from "@ant-design/icons";
import { AuthProvider, useAuth } from "./contexts/AuthContext";
import ProtectedRoute from "./components/ProtectedRoute";
import Login from "./pages/Login";
import Dashboard from "./pages/Dashboard";
import RoutesPage from "./pages/Routes";
import ClustersPage from "./pages/Clusters";
import MetricsPage from "./pages/Metrics";
import LogsPage from "./pages/Logs";
import UsersPage from "./pages/Users";
import SettingsPage from "./pages/Settings";
import "./App.css";

const { Sider, Content, Header } = Layout;
const { Title, Text } = Typography;

const menuItems = [
  { key: "/", icon: <DashboardOutlined />, label: <Link to="/">Dashboard</Link> },
  { key: "/routes", icon: <ApiOutlined />, label: <Link to="/routes">Routes</Link> },
  { key: "/clusters", icon: <ClusterOutlined />, label: <Link to="/clusters">Clusters</Link> },
  { key: "/users", icon: <UserOutlined />, label: <Link to="/users">Users</Link> },
  { key: "/permissions", icon: <SafetyOutlined />, label: <Link to="/permissions">Permissions</Link> },
  { key: "/metrics", icon: <BarChartOutlined />, label: <Link to="/metrics">Metrics</Link> },
  { key: "/logs", icon: <FileTextOutlined />, label: <Link to="/logs">Logs</Link> },
  { key: "/settings", icon: <SettingOutlined />, label: <Link to="/settings">Settings</Link> },
];

function AppLayout() {
  const location = useLocation();
  const { user, logout } = useAuth();

  const handleLogout = async () => {
    await logout();
  };

  const userMenuItems = [
    {
      key: 'profile',
      icon: <UserOutlined />,
      label: 'Profile',
    },
    {
      key: 'settings',
      icon: <SettingOutlined />,
      label: 'Settings',
    },
    { type: 'divider' },
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: 'Logout',
      danger: true,
      onClick: handleLogout,
    },
  ];

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Sider width={240} theme="dark" style={{ position: "fixed", left: 0, top: 0, bottom: 0, overflow: "auto" }}>
        <div style={{
          height: 64,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          borderBottom: '1px solid rgba(255,255,255,0.1)',
        }}>
          <ApiOutlined style={{ fontSize: 32, color: "#1677ff", marginRight: 12 }} />
          <Text strong style={{ color: "#fff", fontSize: 18 }}>API Gateway</Text>
        </div>
        <Menu theme="dark" mode="inline" selectedKeys={[location.pathname]} items={menuItems} style={{ marginTop: 16 }} />
      </Sider>
      <Layout style={{ marginLeft: 240 }}>
        <Header style={{
          background: "#fff",
          padding: "0 24px",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          boxShadow: "0 2px 8px rgba(0,0,0,0.06)",
          position: "sticky",
          top: 0,
          zIndex: 1,
        }}>
          <div>
            <Text strong style={{ fontSize: 16 }}>
              {menuItems.find(item => item.key === location.pathname)?.label?.props?.children || 'Dashboard'}
            </Text>
          </div>

          <Space size="large">
            <Badge count={3} size="small">
              <BellOutlined style={{ fontSize: 20, cursor: 'pointer' }} />
            </Badge>

            <Dropdown menu={{ items: userMenuItems }} placement="bottomRight">
              <Space style={{ cursor: 'pointer' }}>
                <Avatar style={{ backgroundColor: '#1677ff' }} icon={<UserOutlined />} />
                <div>
                  <Text strong>{user?.username || 'Admin'}</Text>
                  <br />
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    {user?.role || 'Administrator'}
                  </Text>
                </div>
              </Space>
            </Dropdown>
          </Space>
        </Header>
        <Content style={{ padding: 24, minHeight: "calc(100vh - 64px)", background: "#f0f2f5" }}>
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/routes" element={<RoutesPage />} />
            <Route path="/clusters" element={<ClustersPage />} />
            <Route path="/metrics" element={<MetricsPage />} />
            <Route path="/logs" element={<LogsPage />} />
            <Route path="/users" element={<UsersPage />} />
            <Route path="/settings" element={<SettingsPage />} />
          </Routes>
        </Content>
      </Layout>
    </Layout>
  );
}

export default function App() {
  return (
    <AntApp>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route path="/*" element={
              <ProtectedRoute>
                <AppLayout />
              </ProtectedRoute>
            } />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </AntApp>
  );
}
