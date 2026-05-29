import React, { useEffect, useState } from "react";
import {
  Table, Card, Tag, Spin, Typography, Button, Space, App, Select, Input, Row, Col, Statistic, Switch, Skeleton, Dropdown, Tooltip
} from "antd";
import {
  ReloadOutlined, DeleteOutlined, FileTextOutlined, ClockCircleOutlined, DownOutlined, ClearOutlined,
} from "@ant-design/icons";
import { getLogs, clearLogs, getLogStats } from "../api/gatewayApi";

const { Title } = Typography;

export default function Logs() {
  const { message, modal } = App.useApp();
  const [logs, setLogs] = useState([]);
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState({});
  const [showSystem, setShowSystem] = useState(false);

  const load = async (p = page) => {
    setLoading(true);
    try {
      const [logsRes, statsRes] = await Promise.all([
        getLogs({ page: p, pageSize: 50, ...filters }),
        getLogStats(),
      ]);
      setLogs(logsRes.data.logs);
      setTotal(logsRes.data.total);
      setStats(statsRes.data);
    } catch { message.error("Failed to load logs"); }
    finally { setLoading(false); }
  };

  useEffect(() => { load(1); }, [filters]);

  const handleClear = () => {
    modal.confirm({
      title: "Clear all logs?",
      content: "This permanently removes every request log entry.",
      okButtonProps: { danger: true },
      onOk: async () => { await clearLogs(); message.success("Logs cleared"); load(1); },
    });
  };

  const handleClearOlderThan = (days) => {
    modal.confirm({
      title: `Clear logs older than ${days} day(s)?`,
      content: "Recent logs are kept; only older entries are removed.",
      okButtonProps: { danger: true },
      onOk: async () => {
        const res = await clearLogs(days);
        const removed = res?.data?.removed ?? 0;
        message.success(`Removed ${removed} old log entr${removed === 1 ? "y" : "ies"}`);
        load(1);
      },
    });
  };

  const clearMenuItems = [
    { key: "1", label: "Older than 1 day" },
    { key: "7", label: "Older than 7 days" },
    { key: "30", label: "Older than 30 days" },
    { type: "divider" },
    { key: "all", label: "Clear all logs", danger: true },
  ];

  const retention = stats?.retention;

  // Process Logs: rename Unknown to System, filter based on showSystem
  const processedLogs = logs
    .map(l => ({ ...l, routeId: l.routeId === "Unknown" ? "System" : l.routeId }))
    .filter(l => showSystem || l.routeId !== "System");

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
        <Space align="center">
          <Title level={3} style={{ margin: 0 }}>Request Logs</Title>
          {retention && (
            <Tooltip title={retention.enabled
              ? `Logs older than ${retention.retentionDays} day(s) are purged automatically every ${retention.cleanupIntervalHours}h`
              : "Automatic log cleanup is disabled"}>
              <Tag color={retention.enabled ? "green" : "default"} icon={<ClearOutlined />}>
                {retention.enabled ? `Auto-clean: ${retention.retentionDays}d` : "Auto-clean: off"}
              </Tag>
            </Tooltip>
          )}
        </Space>
        <Space wrap>
          <Switch 
            checkedChildren="System: ON" 
            unCheckedChildren="System: OFF" 
            checked={showSystem} 
            onChange={setShowSystem} 
          />
          <Button icon={<ReloadOutlined />} onClick={() => load()}>Refresh</Button>
          <Dropdown
            menu={{
              items: clearMenuItems,
              onClick: ({ key }) => (key === "all" ? handleClear() : handleClearOlderThan(Number(key))),
            }}
          >
            <Button danger icon={<DeleteOutlined />}>
              <Space>Clear<DownOutlined /></Space>
            </Button>
          </Dropdown>
        </Space>
      </div>

      <Skeleton loading={loading && !stats} active paragraph={{ rows: 3 }}>
        {stats && (
          <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
            <Col xs={12} sm={6}><Card hoverable><Statistic title="Total Logs (All)" value={stats.total} prefix={<FileTextOutlined />} /></Card></Col>
            <Col xs={12} sm={6}><Card hoverable><Statistic title="Last 24h (All)" value={stats.last24h} prefix={<ClockCircleOutlined />} /></Card></Col>
            {stats.byStatus?.map((s) => (
              <Col xs={12} sm={6} key={s.statusGroup}>
                <Card hoverable>
                  <Statistic title={s.statusGroup} value={s.count}
                    valueStyle={{ color: s.statusGroup.startsWith("2") ? "#52c41a" : s.statusGroup.startsWith("4") ? "#fa8c16" : "#ff4d4f" }} />
                </Card>
              </Col>
            ))}
          </Row>
        )}

        <Card size="small" style={{ marginBottom: 16 }}>
          <Space wrap>
            <Select placeholder="Method" allowClear style={{ width: 100 }}
              onChange={(v) => setFilters((f) => ({ ...f, method: v }))}>
              {["GET", "POST", "PUT", "DELETE"].map((m) => <Select.Option key={m}>{m}</Select.Option>)}
            </Select>
            <Input placeholder="Route ID" allowClear style={{ width: 150 }}
              onChange={(e) => setFilters((f) => ({ ...f, routeId: e.target.value || undefined }))} />
            <Select placeholder="Status" allowClear style={{ width: 100 }}
              onChange={(v) => setFilters((f) => ({ ...f, statusCode: v }))}>
              {[200, 301, 400, 401, 403, 404, 429, 500, 502, 503].map((s) => <Select.Option key={s}>{s}</Select.Option>)}
            </Select>
          </Space>
        </Card>

        <Table dataSource={processedLogs} rowKey="id" loading={loading} size="small"
          pagination={{ current: page, total, pageSize: 50, onChange: (p) => { setPage(p); load(p); } }}
          columns={[
            { title: "Time", dataIndex: "timestamp", width: 180, render: (v) => new Date(v).toLocaleString() },
            { title: "Method", dataIndex: "method", width: 80, render: (v) => <Tag color={v === "GET" ? "blue" : v === "POST" ? "green" : v === "DELETE" ? "red" : "orange"}>{v}</Tag> },
            { title: "Path", dataIndex: "path", ellipsis: true },
            { title: "Status", dataIndex: "statusCode", width: 80, render: (v) => <Tag color={v < 300 ? "green" : v < 400 ? "blue" : v < 500 ? "orange" : "red"}>{v}</Tag> },
            { title: "Latency", dataIndex: "latencyMs", width: 100, render: (v) => `${v} ms` },
            { title: "Client IP", dataIndex: "clientIp", width: 130 },
            { title: "Route", dataIndex: "routeId", width: 130, render: (v) => <Tag color={v === "System" ? "red" : "default"}>{v}</Tag> },
          ]}
        />
      </Skeleton>
    </div>
  );
}
