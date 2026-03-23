import React, { useEffect, useState } from "react";
import {
  Table,
  Card,
  Tag,
  Spin,
  Typography,
  Button,
  Space,
  Popconfirm,
  App,
  Row,
  Col,
  Statistic,
} from "antd";
import {
  ReloadOutlined,
  DeleteOutlined,
  ThunderboltOutlined,
  ClockCircleOutlined,
  WarningOutlined,
  DashboardOutlined,
} from "@ant-design/icons";
import { getMetrics, resetMetrics } from "../api/gatewayApi";

const { Title, Text } = Typography;

export default function Metrics() {
  const { message } = App.useApp();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    setLoading(true);
    try {
      const res = await getMetrics();
      setData(res.data);
    } catch (err) {
      message.error("Failed to load metrics");
    } finally {
      setLoading(false);
    }
  };

  const handleReset = async () => {
    await resetMetrics();
    message.success("Metrics reset");
    load();
  };

  useEffect(() => {
    load();
    const timer = setInterval(load, 5000); // refresh every 5s
    return () => clearInterval(timer);
  }, []);

  if (loading && !data)
    return (
      <div style={{ textAlign: "center", padding: 80 }}>
        <Spin size="large" />
      </div>
    );

  const routes = data?.routes || {};
  const routeEntries = Object.entries(routes);

  // Aggregate totals
  const totals = routeEntries.reduce(
    (acc, [, m]) => ({
      totalRequests: acc.totalRequests + (m.totalRequests || 0),
      successCount: acc.successCount + (m.successCount || 0),
      errorCount: acc.errorCount + (m.errorCount || 0),
      throughputPerSecond: acc.throughputPerSecond + (m.throughputPerSecond || 0),
    }),
    { totalRequests: 0, successCount: 0, errorCount: 0, throughputPerSecond: 0 }
  );

  const tableData = routeEntries.map(([route, metrics]) => ({
    key: route,
    route,
    ...metrics,
  }));

  return (
    <div>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          marginBottom: 16,
        }}
      >
        <Title level={3} style={{ margin: 0 }}>
          Traffic Metrics
        </Title>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={load}>
            Refresh
          </Button>
          <Popconfirm title="Reset all metrics?" onConfirm={handleReset}>
            <Button danger icon={<DeleteOutlined />}>
              Reset
            </Button>
          </Popconfirm>
        </Space>
      </div>

      {/* Summary cards */}
      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        <Col xs={24} sm={12} lg={6}>
          <Card hoverable>
            <Statistic
              title="Total Requests"
              value={totals.totalRequests}
              prefix={<DashboardOutlined style={{ color: "#1677ff" }} />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card hoverable>
            <Statistic
              title="Success"
              value={totals.successCount}
              valueStyle={{ color: "#52c41a" }}
              prefix={<ThunderboltOutlined />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card hoverable>
            <Statistic
              title="Errors"
              value={totals.errorCount}
              valueStyle={totals.errorCount > 0 ? { color: "#ff4d4f" } : {}}
              prefix={<WarningOutlined />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card hoverable>
            <Statistic
              title="Throughput"
              value={totals.throughputPerSecond}
              suffix="req/s"
              precision={2}
              prefix={<ClockCircleOutlined style={{ color: "#fa8c16" }} />}
            />
          </Card>
        </Col>
      </Row>

      {/* Per-route table */}
      <Card title="Per-Route Breakdown">
        <Table
          dataSource={tableData}
          rowKey="key"
          size="small"
          pagination={false}
          columns={[
            {
              title: "Route",
              dataIndex: "route",
              render: (v) => <Tag color="blue">{v}</Tag>,
            },
            {
              title: "Requests",
              dataIndex: "totalRequests",
              sorter: (a, b) => a.totalRequests - b.totalRequests,
            },
            {
              title: "Success",
              dataIndex: "successCount",
              render: (v) => <Tag color="green">{v}</Tag>,
            },
            {
              title: "Errors",
              dataIndex: "errorCount",
              render: (v) =>
                v > 0 ? (
                  <Tag color="red">{v}</Tag>
                ) : (
                  <Tag color="default">0</Tag>
                ),
            },
            {
              title: "Error Rate",
              dataIndex: "errorRate",
              render: (v) => {
                const color = v > 10 ? "red" : v > 0 ? "orange" : "green";
                return <Tag color={color}>{v}%</Tag>;
              },
            },
            {
              title: "Avg Latency",
              dataIndex: "avgLatencyMs",
              render: (v) => `${v} ms`,
              sorter: (a, b) => a.avgLatencyMs - b.avgLatencyMs,
            },
            {
              title: "Max Latency",
              dataIndex: "maxLatencyMs",
              render: (v) => `${v} ms`,
            },
            {
              title: "Throughput",
              dataIndex: "throughputPerSecond",
              render: (v) => <Tag color="purple">{v} req/s</Tag>,
              sorter: (a, b) => a.throughputPerSecond - b.throughputPerSecond,
            },
            {
              title: "Uptime",
              dataIndex: "uptimeSeconds",
              render: (v) => {
                if (v > 3600) return `${Math.floor(v / 3600)}h ${Math.floor((v % 3600) / 60)}m`;
                if (v > 60) return `${Math.floor(v / 60)}m ${Math.floor(v % 60)}s`;
                return `${v}s`;
              },
            },
          ]}
        />
      </Card>

      <div style={{ marginTop: 16, opacity: 0.5, fontSize: 12 }}>
        Auto-refresh every 5s · Last: {new Date(data?.timestamp).toLocaleString()}
      </div>
    </div>
  );
}
