import React from "react";
import { Layout, Menu, Typography, App as AntApp } from "antd";
import {
  BrowserRouter,
  Routes,
  Route,
  Link,
  useLocation,
} from "react-router-dom";
import {
  DashboardOutlined,
  ApiOutlined,
  ClusterOutlined,
} from "@ant-design/icons";
import Dashboard from "./pages/Dashboard";
import RoutesPage from "./pages/Routes";
import ClustersPage from "./pages/Clusters";
import "./App.css";

const { Sider, Content } = Layout;
const { Title } = Typography;

const menuItems = [
  {
    key: "/",
    icon: <DashboardOutlined />,
    label: <Link to="/">Dashboard</Link>,
  },
  {
    key: "/routes",
    icon: <ApiOutlined />,
    label: <Link to="/routes">Routes</Link>,
  },
  {
    key: "/clusters",
    icon: <ClusterOutlined />,
    label: <Link to="/clusters">Clusters</Link>,
  },
];

function AppLayout() {
  const location = useLocation();

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Sider
        width={240}
        theme="dark"
        style={{
          position: "fixed",
          left: 0,
          top: 0,
          bottom: 0,
          overflow: "auto",
        }}
      >
        <div className="logo-area">
          <ApiOutlined style={{ fontSize: 24, color: "#1677ff" }} />
          <Title level={4} style={{ color: "#fff", margin: 0 }}>
            API Gateway
          </Title>
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[location.pathname]}
          items={menuItems}
        />
      </Sider>
      <Layout style={{ marginLeft: 240 }}>
        <Content style={{ padding: 24, minHeight: "100vh", background: "#f5f5f5" }}>
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/routes" element={<RoutesPage />} />
            <Route path="/clusters" element={<ClustersPage />} />
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
        <AppLayout />
      </BrowserRouter>
    </AntApp>
  );
}
