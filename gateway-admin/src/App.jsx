import React, { createContext, useContext, useState, useEffect } from "react";
import { Layout, Menu, Typography, App as AntApp, Button, Dropdown, Avatar, Badge, Space, ConfigProvider, theme, Switch } from "antd";
import {
  BrowserRouter, Routes, Route, Link, useLocation, Navigate,
} from "react-router-dom";
import {
  DashboardOutlined, ApiOutlined, ClusterOutlined, BarChartOutlined,
  FileTextOutlined, UserOutlined, SettingOutlined, LogoutOutlined, BellOutlined,
  SafetyOutlined, BulbOutlined, BulbFilled
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

const ThemeContext = createContext();

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
  const { isDarkMode, toggleTheme } = useContext(ThemeContext);

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

  const headerBg = isDarkMode ? "rgba(13, 17, 23, 0.8)" : "rgba(255, 255, 255, 0.8)";
  const headerBorder = isDarkMode ? "rgba(255,255,255,0.08)" : "rgba(0,0,0,0.06)";

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Sider 
        width={240} 
        theme={isDarkMode ? "dark" : "light"} 
        style={{ 
          position: "fixed", 
          left: 0, 
          top: 0, 
          bottom: 0, 
          overflow: "hidden", 
          display: 'flex', 
          flexDirection: 'column' 
        }}
      >
        <div style={{ flex: 1, overflow: 'auto', display: 'flex', flexDirection: 'column' }}>
          <div style={{
            height: 64,
            minHeight: 64,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            borderBottom: `1px solid ${headerBorder}`,
          }}>
            <ApiOutlined style={{ fontSize: 32, color: "#1677ff", marginRight: 12 }} />
            <Text strong style={{ color: isDarkMode ? "#fff" : "#000", fontSize: 18 }}>API Gateway</Text>
          </div>
          <Menu 
            theme={isDarkMode ? "dark" : "light"} 
            mode="inline" 
            selectedKeys={[location.pathname]} 
            items={menuItems} 
            style={{ marginTop: 16, flex: 1, borderRight: 0 }} 
          />
          <div style={{
            padding: '16px',
            borderTop: `1px solid ${headerBorder}`,
            display: 'flex',
            alignItems: 'center',
            gap: '12px'
          }}>
            <Switch 
              checked={isDarkMode} 
              onChange={toggleTheme} 
              checkedChildren={<BulbFilled />} 
              unCheckedChildren={<BulbOutlined />} 
            />
            <Text type="secondary" style={{ fontSize: 13 }}>{isDarkMode ? 'Dark Mode' : 'Light Mode'}</Text>
          </div>
        </div>
      </Sider>
      
      <Layout style={{ marginLeft: 240 }}>
        <Header style={{
          background: headerBg,
          backdropFilter: "blur(12px)",
          WebkitBackdropFilter: "blur(12px)",
          padding: "0 24px",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          borderBottom: `1px solid ${headerBorder}`,
          position: "sticky",
          top: 0,
          zIndex: 10,
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
        
        <Content style={{ padding: 24, minHeight: "calc(100vh - 64px)", background: "transparent" }}>
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
  const [isDarkMode, setIsDarkMode] = useState(() => {
    return localStorage.getItem('theme') !== 'light';
  });

  const toggleTheme = () => {
    setIsDarkMode(prev => {
      const next = !prev;
      localStorage.setItem('theme', next ? 'dark' : 'light');
      document.body.style.backgroundColor = next ? '#010409' : '#f0f2f5';
      return next;
    });
  };

  useEffect(() => {
    document.body.style.backgroundColor = isDarkMode ? '#010409' : '#f0f2f5';
  }, [isDarkMode]);

  const darkTheme = {
    algorithm: theme.darkAlgorithm,
    token: {
      colorPrimary: '#1677ff',
      colorBgBase: '#0d1117',
      colorBgContainer: '#161b22',
      colorBgElevated: '#21262d',
      borderRadius: 8,
      wireframe: false,
      fontFamily: "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
    },
    components: {
      Layout: {
        siderBg: '#0d1117',
        headerBg: 'rgba(13, 17, 23, 0.8)',
        headerPadding: '0 24px',
        bodyBg: '#010409',
      },
      Card: {
        colorBgContainer: 'rgba(22, 27, 34, 0.6)',
        boxShadowTertiary: '0 4px 30px rgba(0, 0, 0, 0.1)',
        lineWidth: 1,
        colorBorderSecondary: 'rgba(255, 255, 255, 0.08)'
      }
    }
  };

  const lightTheme = {
    algorithm: theme.defaultAlgorithm,
    token: {
      colorPrimary: '#1677ff',
      colorBgBase: '#ffffff',
      colorBgContainer: '#ffffff',
      colorBgElevated: '#ffffff',
      borderRadius: 8,
      wireframe: false,
      fontFamily: "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
    },
    components: {
      Layout: {
        siderBg: '#ffffff',
        headerBg: 'rgba(255, 255, 255, 0.8)',
        headerPadding: '0 24px',
        bodyBg: '#f0f2f5',
      },
      Card: {
        colorBgContainer: 'rgba(255, 255, 255, 0.7)',
        boxShadowTertiary: '0 4px 30px rgba(0, 0, 0, 0.05)',
        lineWidth: 1,
        colorBorderSecondary: 'rgba(0, 0, 0, 0.06)'
      }
    }
  };

  return (
    <ThemeContext.Provider value={{ isDarkMode, toggleTheme }}>
      <ConfigProvider theme={isDarkMode ? darkTheme : lightTheme}>
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
      </ConfigProvider>
    </ThemeContext.Provider>
  );
}
