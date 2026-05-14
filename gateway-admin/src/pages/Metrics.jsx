import React, { useEffect, useState, useRef } from "react";
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
  Switch,
  Select,
  Skeleton
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
import { Line } from "@ant-design/plots";

const { Title, Text } = Typography;

export default function Metrics() {
  const { message } = App.useApp();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  
  // UX Enhancements State
  const [showSystem, setShowSystem] = useState(false);
  const [refreshInterval, setRefreshInterval] = useState(5000);
  const [history, setHistory] = useState([]); // For Line Chart

  const timerRef = useRef(null);

  const load = async () => {
    try {
      const res = await getMetrics();
      setData(res.data);
      
      setHistory((prev) => {
        const newHist = [...prev, { time: new Date().toLocaleTimeString('en-GB', { hour12: false }), data: res.data }];
        if (newHist.length > 20) newHist.shift(); // keep last 20
        return newHist;
      });
    } catch (err) {
      // Avoid spamming errors on auto-refresh
      if (!data) message.error("Failed to load metrics");
    } finally {
      setLoading(false);
    }
  };

  const handleReset = async () => {
    await resetMetrics();
    message.success("Metrics reset");
    setHistory([]);
    load();
  };

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    if (timerRef.current) clearInterval(timerRef.current);
    if (refreshInterval > 0) {
      timerRef.current = setInterval(load, refreshInterval);
    }
    return () => clearInterval(timerRef.current);
  }, [refreshInterval]);

  // Process data & apply System filter
  const rawRoutes = data?.routes || {};
  const routeEntries = Object.entries(rawRoutes)
    .map(([route, m]) => ({
      key: route,
      route: route === "Unknown" ? "System" : route,
      ...m,
    }))
    .filter((m) => showSystem || m.route !== "System");

  const totals = routeEntries.reduce(
    (acc, m) => ({
      totalRequests: acc.totalRequests + (m.totalRequests || 0),
      successCount: acc.successCount + (m.successCount || 0),
      errorCount: acc.errorCount + (m.errorCount || 0),
      throughputPerSecond: acc.throughputPerSecond + (m.throughputPerSecond || 0),
    }),
    { totalRequests: 0, successCount: 0, errorCount: 0, throughputPerSecond: 0 }
  );

  // Prepare Chart Data
  const chartData = [];
  history.forEach((h) => {
    const hEntries = Object.entries(h.data?.routes || {})
      .map(([route, m]) => ({ route: route === "Unknown" ? "System" : route, ...m }))
      .filter((m) => showSystem || m.route !== "System");
      
    const tp = hEntries.reduce((acc, m) => acc + (m.throughputPerSecond || 0), 0);
    const err = hEntries.reduce((acc, m) => acc + (m.errorCount || 0), 0);
    
    chartData.push({ time: h.time, value: tp, category: "Throughput (req/s)" });
    chartData.push({ time: h.time, value: err, category: "Errors" });
  });

  const chartConfig = {
    data: chartData,
    xField: "time",
    yField: "value",
    colorField: "category",
    color: ["#1677ff", "#ff4d4f"],
    smooth: true,
    animation: false,
    height: 180,
    theme: 'dark',
    legend: { position: 'top-right' }
  };

  return (
    <div>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: 16,
        }}
      >
        <Title level={3} style={{ margin: 0 }}>
          Traffic Metrics
        </Title>
        <Space wrap>
          <Switch 
            checkedChildren="System: ON" 
            unCheckedChildren="System: OFF" 
            checked={showSystem} 
            onChange={setShowSystem} 
          />
          <Select 
            value={refreshInterval} 
            onChange={setRefreshInterval} 
            style={{ width: 130 }}
            options={[
              { label: 'Auto-refresh: Off', value: 0 },
              { label: 'Refresh: 5s', value: 5000 },
              { label: 'Refresh: 10s', value: 10000 },
              { label: 'Refresh: 30s', value: 30000 },
            ]}
          />
          <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
            Refresh
          </Button>
          <Popconfirm title="Reset all metrics?" onConfirm={handleReset}>
            <Button danger icon={<DeleteOutlined />}>
              Reset
            </Button>
          </Popconfirm>
        </Space>
      </div>

      <Skeleton loading={loading && !data} active paragraph={{ rows: 10 }}>
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
            <Card 
              hoverable 
              style={{
                boxShadow: totals.errorCount > 0 ? "0 0 20px rgba(255, 77, 79, 0.4)" : undefined,
                borderColor: totals.errorCount > 0 ? "#ff4d4f" : undefined,
                transition: "all 0.3s ease"
              }}
            >
              <Statistic
                title="Errors"
                value={totals.errorCount}
                valueStyle={totals.errorCount > 0 ? { color: "#ff4d4f", fontWeight: 'bold' } : {}}
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

        {/* Real-time Chart */}
        {history.length > 1 && (
          <Card title="Traffic Trend (Real-time)" style={{ marginBottom: 24 }} size="small">
            <Line {...chartConfig} />
          </Card>
        )}

        {/* Per-route table */}
        <Card title="Per-Route Breakdown" size="small">
          <Table
            dataSource={routeEntries}
            rowKey="key"
            size="small"
            pagination={false}
            columns={[
              {
                title: "Route",
                dataIndex: "route",
                render: (v) => <Tag color={v === "System" ? "red" : "blue"}>{v}</Tag>,
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
                  const formatted = Number(v).toFixed(2);
                  return <Tag color={color}>{formatted}%</Tag>;
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
                render: (v) => {
                  const num = Number(v);
                  let formatted = num;
                  if (num >= 1000) {
                    formatted = (num / 1000).toFixed(1) + "k";
                  } else if (!Number.isInteger(num)) {
                    formatted = num.toFixed(2);
                  }
                  return <Tag color="purple">{formatted} req/s</Tag>;
                },
                sorter: (a, b) => a.throughputPerSecond - b.throughputPerSecond,
              },
              {
                title: "Uptime",
                dataIndex: "uptimeSeconds",
                render: (v) => {
                  if (!v) return "0s";
                  const mo = Math.floor(v / 2592000);
                  const d = Math.floor((v % 2592000) / 86400);
                  const h = Math.floor((v % 86400) / 3600);
                  const m = Math.floor((v % 3600) / 60);
                  const s = Math.floor(v % 60);

                  const parts = [];
                  if (mo > 0) {
                    parts.push(`${mo}mo`);
                    if (d > 0) parts.push(`${d}d`);
                  } else if (d > 0) {
                    parts.push(`${d}d`);
                    if (h > 0) parts.push(`${h}h`);
                  } else if (h > 0) {
                    parts.push(`${h}h`);
                    if (m > 0) parts.push(`${m}m`);
                  } else if (m > 0) {
                    parts.push(`${m}m`);
                    parts.push(`${s}s`);
                  } else {
                    parts.push(`${s}s`);
                  }

                  return <Tag color="blue">{parts.join(" ")}</Tag>;
                },
              },
            ]}
          />
        </Card>

        <div style={{ marginTop: 16, opacity: 0.5, fontSize: 12 }}>
          Last updated: {new Date(data?.timestamp).toLocaleString()}
        </div>
      </Skeleton>
    </div>
  );
}
