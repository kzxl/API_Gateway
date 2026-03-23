import React, { useEffect, useState } from "react";
import { Row, Col, Card, Statistic, Tag, Table, Spin, Typography, Space, Tooltip } from "antd";
import {
  ApiOutlined,
  ClusterOutlined,
  NodeIndexOutlined,
  CheckCircleOutlined,
  HeartOutlined,
} from "@ant-design/icons";
import { getHealth } from "../api/gatewayApi";

const { Title, Text } = Typography;

export default function Dashboard() {
  const [health, setHealth] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const load = async () => {
    setLoading(true);
    try {
      const res = await getHealth();
      setHealth(res.data);
      setError(null);
    } catch (err) {
      setError(err.response?.data?.error || err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    const timer = setInterval(load, 10000);
    return () => clearInterval(timer);
  }, []);

  if (loading && !health)
    return (
      <div style={{ textAlign: "center", padding: 80 }}>
        <Spin size="large" />
      </div>
    );

  if (error)
    return (
      <div style={{ textAlign: "center", padding: 80 }}>
        <Text type="danger" style={{ fontSize: 16 }}>
          ⚠️ Cannot connect to gateway: {error}
        </Text>
      </div>
    );

  const gw = health?.gateway || {};
  const destinations = health?.destinations || [];

  const primaryCount = destinations.filter((d) => d.role === "Active").length;
  const standbyCount = destinations.filter((d) => d.role === "Standby").length;

  return (
    <div>
      <div style={{ marginBottom: 24, display: "flex", alignItems: "center", gap: 12 }}>
        <Title level={3} style={{ margin: 0 }}>
          Dashboard
        </Title>
        <Tag icon={<CheckCircleOutlined />} color="success">
          {health?.status?.toUpperCase()}
        </Tag>
      </div>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card hoverable>
            <Statistic
              title="Active Routes"
              value={gw.totalRoutes}
              prefix={<ApiOutlined style={{ color: "#1677ff" }} />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card hoverable>
            <Statistic
              title="Clusters"
              value={gw.totalClusters}
              prefix={<ClusterOutlined style={{ color: "#52c41a" }} />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card hoverable>
            <Statistic
              title="Primary APIs"
              value={primaryCount}
              suffix={
                standbyCount > 0 ? (
                  <Text type="secondary" style={{ fontSize: 14 }}>
                    + {standbyCount} standby
                  </Text>
                ) : null
              }
              prefix={<NodeIndexOutlined style={{ color: "#fa8c16" }} />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card hoverable>
            <Statistic
              title="Proxy Routes (Live)"
              value={gw.activeProxyRoutes}
              prefix={<ApiOutlined style={{ color: "#722ed1" }} />}
            />
          </Card>
        </Col>
      </Row>

      <Card title="Registered Destinations" style={{ marginTop: 24 }}>
        <Table
          dataSource={destinations}
          rowKey={(r, i) => `${r.clusterId}-${i}`}
          size="small"
          pagination={false}
          columns={[
            {
              title: "Cluster",
              dataIndex: "clusterId",
              render: (v) => <Tag color="blue">{v}</Tag>,
            },
            {
              title: "Address",
              dataIndex: "address",
              render: (v) => (
                <a href={v} target="_blank" rel="noreferrer">
                  {v}
                </a>
              ),
            },
            {
              title: "Role",
              dataIndex: "role",
              render: (v) =>
                v === "Standby" ? (
                  <Tooltip title="Sẽ tự động nhận traffic khi primary down">
                    <Tag color="orange">⏳ STANDBY</Tag>
                  </Tooltip>
                ) : (
                  <Tag color="green">✅ PRIMARY</Tag>
                ),
            },
            {
              title: "Health Check",
              dataIndex: "healthCheck",
              render: (v, record) =>
                v === "Enabled" ? (
                  <Tooltip title={`Path: ${record.healthCheckPath} · Every ${record.healthCheckIntervalSeconds}s`}>
                    <Tag icon={<HeartOutlined />} color="green">
                      ON
                    </Tag>
                  </Tooltip>
                ) : (
                  <Tag color="default">OFF</Tag>
                ),
            },
          ]}
        />
      </Card>

      <div style={{ marginTop: 16, opacity: 0.5, fontSize: 12 }}>
        Last updated: {new Date(health?.timestamp).toLocaleString()} · Auto-refresh every 10s
      </div>
    </div>
  );
}
