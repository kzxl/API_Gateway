import React from "react";
import { Layout, Menu } from "antd";
import { BrowserRouter, Routes, Route, Link } from "react-router-dom";
import RoutesPage from "./pages/Routes";
import ClustersPage from "./pages/Clusters";

const { Header, Sider, Content } = Layout;

export default function App() {
  return (
    <BrowserRouter>
      <Layout style={{ height: "100vh" }}>
        <Sider>
          <Menu theme="dark" mode="inline">
            <Menu.Item key="routes">
              <Link to="/">Routes</Link>
            </Menu.Item>
            <Menu.Item key="clusters">
              <Link to="/clusters">Clusters</Link>
            </Menu.Item>
          </Menu>
        </Sider>
        <Layout>
          <Header style={{ color: "#fff" }}>API Gateway Admin</Header>
          <Content style={{ padding: 16 }}>
            <Routes>
              <Route path="/" element={<RoutesPage />} />
              <Route path="/clusters" element={<ClustersPage />} />
            </Routes>
          </Content>
        </Layout>
      </Layout>
    </BrowserRouter>
  );
}
