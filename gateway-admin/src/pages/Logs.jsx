import React, { useEffect, useState } from "react";
import {
  Table, Card, Tag, Spin, Typography, Button, Space, App, Select, Input, Row, Col, Statistic,
} from "antd";
import {
  ReloadOutlined, DeleteOutlined, FileTextOutlined, ClockCircleOutlined,
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
      onOk: async () => { await clearLogs(); message.success("Logs cleared"); load(1); },
    });
  };

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 16 }}>
        <Title level={3} style={{ margin: 0 }}>Request Logs</Title>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => load()}>Refresh</Button>
          <Button danger icon={<DeleteOutlined />} onClick={handleClear}>Clear</Button>
        </Space>
      </div>

      {stats && (
        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          <Col xs={12} sm={6}><Card hoverable><Statistic title="Total Logs" value={stats.total} prefix={<FileTextOutlined />} /></Card></Col>
          <Col xs={12} sm={6}><Card hoverable><Statistic title="Last 24h" value={stats.last24h} prefix={<ClockCircleOutlined />} /></Card></Col>
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

      <Table dataSource={logs} rowKey="id" loading={loading} size="small"
        pagination={{ current: page, total, pageSize: 50, onChange: (p) => { setPage(p); load(p); } }}
        columns={[
          { title: "Time", dataIndex: "timestamp", width: 180, render: (v) => new Date(v).toLocaleString() },
          { title: "Method", dataIndex: "method", width: 80, render: (v) => <Tag color={v === "GET" ? "blue" : v === "POST" ? "green" : v === "DELETE" ? "red" : "orange"}>{v}</Tag> },
          { title: "Path", dataIndex: "path", ellipsis: true },
          { title: "Status", dataIndex: "statusCode", width: 80, render: (v) => <Tag color={v < 300 ? "green" : v < 400 ? "blue" : v < 500 ? "orange" : "red"}>{v}</Tag> },
          { title: "Latency", dataIndex: "latencyMs", width: 100, render: (v) => `${v} ms` },
          { title: "Client IP", dataIndex: "clientIp", width: 130 },
          { title: "Route", dataIndex: "routeId", width: 130, render: (v) => <Tag>{v}</Tag> },
        ]}
      />
    </div>
  );
}
