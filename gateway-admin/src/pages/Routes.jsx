import React, { useEffect, useState } from "react";
import {
  Table, Button, Modal, Form, Input, Space, App, Typography, Tag, Popconfirm, InputNumber,
  Divider, Switch, Collapse,
} from "antd";
import { PlusOutlined, EditOutlined, DeleteOutlined, ReloadOutlined, ThunderboltOutlined, SafetyOutlined } from "@ant-design/icons";
import { getRoutes, createRoute, updateRoute, deleteRoute } from "../api/gatewayApi";

const { Title, Text } = Typography;

export default function RoutesPage() {
  const { message } = App.useApp();
  const [routes, setRoutes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try { const res = await getRoutes(); setRoutes(res.data); }
    catch { message.error("Failed to load routes"); }
    finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const openModal = (route = null) => {
    setEditing(route);
    form.resetFields();
    if (route) {
      form.setFieldsValue({
        ...route,
        enableCircuitBreaker: route.circuitBreakerThreshold > 0,
        enableRateLimit: route.rateLimitPerSecond > 0,
        enableCache: route.cacheTtlSeconds > 0,
      });
    }
    setModalOpen(true);
  };

  const handleSave = async () => {
    const values = await form.validateFields();
    const payload = {
      routeId: values.routeId,
      matchPath: values.matchPath,
      methods: values.methods,
      clusterId: values.clusterId,
      rateLimitPerSecond: values.enableRateLimit ? (values.rateLimitPerSecond || 100) : 0,
      circuitBreakerThreshold: values.enableCircuitBreaker ? (values.circuitBreakerThreshold || 50) : 0,
      circuitBreakerDurationSeconds: values.circuitBreakerDurationSeconds || 30,
      ipWhitelist: values.ipWhitelist || null,
      ipBlacklist: values.ipBlacklist || null,
      cacheTtlSeconds: values.enableCache ? (values.cacheTtlSeconds || 60) : 0,
      transformsJson: values.transformsJson || null,
    };

    if (editing) {
      await updateRoute(editing.id, payload);
      message.success("Route updated");
    } else {
      await createRoute(payload);
      message.success("Route created");
    }
    setModalOpen(false);
    load();
  };

  const handleDelete = async (id) => {
    await deleteRoute(id);
    message.success("Route deleted");
    load();
  };

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 16 }}>
        <Title level={3} style={{ margin: 0 }}>Routes</Title>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={load}>Refresh</Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => openModal()}>Add Route</Button>
        </Space>
      </div>

      <Table dataSource={routes} rowKey="id" loading={loading} size="small"
        columns={[
          { title: "Route ID", dataIndex: "routeId", render: (v) => <Tag color="blue">{v}</Tag> },
          { title: "Path", dataIndex: "matchPath" },
          { title: "Methods", dataIndex: "methods", render: (v) => v || <Text type="secondary">ALL</Text> },
          { title: "Cluster", dataIndex: "clusterId", render: (v) => <Tag>{v}</Tag> },
          {
            title: "Protection", render: (_, r) => (
              <Space wrap>
                {r.rateLimitPerSecond > 0 && <Tag color="orange">⚡ {r.rateLimitPerSecond}/s</Tag>}
                {r.circuitBreakerThreshold > 0 && <Tag color="red">🔌 CB {r.circuitBreakerThreshold}%</Tag>}
                {r.ipWhitelist && <Tag color="green">🛡 Whitelist</Tag>}
                {r.ipBlacklist && <Tag color="volcano">🚫 Blacklist</Tag>}
                {r.cacheTtlSeconds > 0 && <Tag color="purple">💾 Cache {r.cacheTtlSeconds}s</Tag>}
              </Space>
            ),
          },
          {
            title: "Actions", width: 100, render: (_, r) => (
              <Space>
                <Button size="small" icon={<EditOutlined />} onClick={() => openModal(r)} />
                <Popconfirm title="Delete route?" onConfirm={() => handleDelete(r.id)}>
                  <Button size="small" danger icon={<DeleteOutlined />} />
                </Popconfirm>
              </Space>
            ),
          },
        ]}
      />

      <Modal title={editing ? "Edit Route" : "New Route"} open={modalOpen} width={600}
        onOk={handleSave} onCancel={() => setModalOpen(false)}>
        <Form form={form} layout="vertical">
          <Form.Item name="routeId" label="Route ID" rules={[{ required: true }]}>
            <Input placeholder="my-route" />
          </Form.Item>
          <Form.Item name="matchPath" label="Match Path" rules={[{ required: true }]}>
            <Input placeholder="/{**catch-all}" />
          </Form.Item>
          <Form.Item name="methods" label="Methods (comma-separated)">
            <Input placeholder="GET,POST (leave empty for all)" />
          </Form.Item>
          <Form.Item name="clusterId" label="Cluster ID" rules={[{ required: true }]}>
            <Input placeholder="my-cluster" />
          </Form.Item>

          <Collapse ghost items={[
            {
              key: "protection",
              label: <><SafetyOutlined /> Protection Settings</>,
              children: (
                <>
                  <Form.Item name="enableRateLimit" label="Rate Limiting" valuePropName="checked">
                    <Switch checkedChildren="ON" unCheckedChildren="OFF" />
                  </Form.Item>
                  <Form.Item noStyle shouldUpdate>
                    {({ getFieldValue }) => getFieldValue("enableRateLimit") && (
                      <Form.Item name="rateLimitPerSecond" label="Max Requests/Second">
                        <InputNumber min={1} max={10000} placeholder="100" style={{ width: "100%" }} />
                      </Form.Item>
                    )}
                  </Form.Item>

                  <Divider />

                  <Form.Item name="enableCircuitBreaker" label="Circuit Breaker" valuePropName="checked">
                    <Switch checkedChildren="ON" unCheckedChildren="OFF" />
                  </Form.Item>
                  <Form.Item noStyle shouldUpdate>
                    {({ getFieldValue }) => getFieldValue("enableCircuitBreaker") && (
                      <>
                        <Form.Item name="circuitBreakerThreshold" label="Error Threshold (%)">
                          <InputNumber min={1} max={100} placeholder="50" style={{ width: "100%" }} />
                        </Form.Item>
                        <Form.Item name="circuitBreakerDurationSeconds" label="Reset Duration (seconds)">
                          <InputNumber min={5} max={600} placeholder="30" style={{ width: "100%" }} />
                        </Form.Item>
                      </>
                    )}
                  </Form.Item>

                  <Divider />

                  <Form.Item name="ipWhitelist" label="IP Whitelist (comma-separated)">
                    <Input placeholder="192.168.1.1, 10.0.0.0" />
                  </Form.Item>
                  <Form.Item name="ipBlacklist" label="IP Blacklist (comma-separated)">
                    <Input placeholder="1.2.3.4" />
                  </Form.Item>
                </>
              ),
            },
            {
              key: "advanced",
              label: <><ThunderboltOutlined /> Advanced Settings</>,
              children: (
                <>
                  <Form.Item name="enableCache" label="Response Caching (GET only)" valuePropName="checked">
                    <Switch checkedChildren="ON" unCheckedChildren="OFF" />
                  </Form.Item>
                  <Form.Item noStyle shouldUpdate>
                    {({ getFieldValue }) => getFieldValue("enableCache") && (
                      <Form.Item name="cacheTtlSeconds" label="Cache TTL (seconds)">
                        <InputNumber min={1} max={86400} placeholder="60" style={{ width: "100%" }} />
                      </Form.Item>
                    )}
                  </Form.Item>

                  <Divider />

                  <Form.Item name="transformsJson" label="Transforms (JSON)">
                    <Input.TextArea rows={3} placeholder='[{"PathPrefix":"/api"}]' />
                  </Form.Item>
                </>
              ),
            },
          ]} />
        </Form>
      </Modal>
    </div>
  );
}
