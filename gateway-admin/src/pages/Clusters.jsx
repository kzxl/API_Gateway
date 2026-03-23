import React, { useEffect, useState } from "react";
import {
  Table,
  Button,
  Modal,
  Form,
  Input,
  InputNumber,
  Switch,
  Select,
  Popconfirm,
  Space,
  Tag,
  App,
  Typography,
  Divider,
  Card,
  Row,
  Col,
  Tooltip,
} from "antd";
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  HeartOutlined,
  MinusCircleOutlined,
  PlusCircleOutlined,
} from "@ant-design/icons";
import {
  getClusters,
  createCluster,
  updateCluster,
  deleteCluster,
} from "../api/gatewayApi";

const { Title, Text } = Typography;

const LB_POLICIES = [
  { label: "Round Robin", value: "RoundRobin" },
  { label: "Random", value: "Random" },
  { label: "Least Requests", value: "LeastRequests" },
  { label: "First Alphabetical", value: "FirstAlphabetical" },
  { label: "Power of Two Choices", value: "PowerOfTwoChoices" },
];

export default function Clusters() {
  const { message } = App.useApp();
  const [clusters, setClusters] = useState([]);
  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();
  const [destinations, setDestinations] = useState([
    { address: "", health: "Active" },
  ]);

  const load = async () => {
    setLoading(true);
    try {
      setClusters((await getClusters()).data);
    } catch (err) {
      message.error(
        "Failed to load clusters: " +
          (err.response?.data?.error || err.message)
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const openCreate = () => {
    form.resetFields();
    form.setFieldsValue({
      enableHealthCheck: true,
      healthCheckPath: "/health",
      healthCheckIntervalSeconds: 10,
      healthCheckTimeoutSeconds: 5,
      loadBalancingPolicy: "RoundRobin",
    });
    setDestinations([{ address: "", health: "Active" }]);
    setEditingId(null);
    setOpen(true);
  };

  const openEdit = (record) => {
    let dests = [{ address: "", health: "Active" }];
    try {
      const parsed = JSON.parse(record.destinationsJson || "[]");
      if (parsed.length > 0)
        dests = parsed.map((d) => ({
          address: d.address,
          health: d.health || "Active",
        }));
    } catch {
      /* keep default */
    }
    setDestinations(dests);
    form.setFieldsValue({
      clusterId: record.clusterId,
      enableHealthCheck: record.enableHealthCheck ?? true,
      healthCheckPath: record.healthCheckPath || "/health",
      healthCheckIntervalSeconds: record.healthCheckIntervalSeconds || 10,
      healthCheckTimeoutSeconds: record.healthCheckTimeoutSeconds || 5,
      loadBalancingPolicy: record.loadBalancingPolicy || "RoundRobin",
      enableRetry: (record.retryCount || 0) > 0,
      retryCount: record.retryCount || 3,
      retryDelayMs: record.retryDelayMs || 1000,
    });
    setEditingId(record.id);
    setOpen(true);
  };

  const addDestination = () => {
    setDestinations([...destinations, { address: "", health: "Active" }]);
  };

  const removeDestination = (index) => {
    if (destinations.length <= 1) return;
    setDestinations(destinations.filter((_, i) => i !== index));
  };

  const updateDestination = (index, field, value) => {
    const newDests = [...destinations];
    newDests[index] = { ...newDests[index], [field]: value };
    setDestinations(newDests);
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      const validDests = destinations.filter((d) => d.address.trim());
      if (validDests.length === 0) {
        message.error("At least one destination is required");
        return;
      }

      const destList = validDests.map((d, i) => ({
        id: `dest-${i + 1}`,
        address: d.address.trim(),
        health: d.health || "Active",
      }));

      const payload = {
        clusterId: values.clusterId,
        destinationsJson: JSON.stringify(destList),
        enableHealthCheck: values.enableHealthCheck ?? true,
        healthCheckPath: values.healthCheckPath || "/health",
        healthCheckIntervalSeconds: values.healthCheckIntervalSeconds || 10,
        healthCheckTimeoutSeconds: values.healthCheckTimeoutSeconds || 5,
        loadBalancingPolicy: values.loadBalancingPolicy || "RoundRobin",
        retryCount: values.enableRetry ? (values.retryCount || 3) : 0,
        retryDelayMs: values.retryDelayMs || 1000,
      };

      if (editingId) {
        await updateCluster(editingId, { ...payload, id: editingId });
        message.success("Cluster updated");
      } else {
        await createCluster(payload);
        message.success("Cluster created");
      }
      setOpen(false);
      load();
    } catch (err) {
      if (err.response) {
        message.error(err.response.data?.error || "Save failed");
      }
    }
  };

  const handleDelete = async (id) => {
    try {
      await deleteCluster(id);
      message.success("Cluster deleted");
      load();
    } catch (err) {
      message.error("Delete failed");
    }
  };

  const parseDestinations = (json) => {
    try {
      return JSON.parse(json || "[]");
    } catch {
      return [];
    }
  };

  const columns = [
    {
      title: "Cluster ID",
      dataIndex: "clusterId",
      render: (v) => <Tag color="purple">{v}</Tag>,
    },
    {
      title: "Destinations",
      dataIndex: "destinationsJson",
      render: (json) => {
        const dests = parseDestinations(json);
        return (
          <Space direction="vertical" size={2}>
            {dests.map((d, i) => (
              <Space key={i} size={4}>
                <Tag color={d.health === "Standby" ? "orange" : "cyan"}>
                  {d.health === "Standby" ? "⏳ STANDBY" : "✅ PRIMARY"}
                </Tag>
                <Text>{d.address}</Text>
              </Space>
            ))}
          </Space>
        );
      },
    },
    {
      title: "Health Check",
      render: (_, record) => (
        <Space>
          {record.enableHealthCheck ? (
            <Tooltip title={`Path: ${record.healthCheckPath} · Every ${record.healthCheckIntervalSeconds}s`}>
              <Tag icon={<HeartOutlined />} color="green">
                ON
              </Tag>
            </Tooltip>
          ) : (
            <Tag color="default">OFF</Tag>
          )}
        </Space>
      ),
    },
    {
      title: "LB Policy",
      dataIndex: "loadBalancingPolicy",
      render: (v) => <Tag>{v || "RoundRobin"}</Tag>,
    },
    {
      title: "Actions",
      width: 120,
      render: (_, record) => (
        <Space>
          <Button
            type="text"
            icon={<EditOutlined />}
            onClick={() => openEdit(record)}
          />
          <Popconfirm
            title="Delete this cluster?"
            description="Routes using this cluster will stop working."
            onConfirm={() => handleDelete(record.id)}
          >
            <Button type="text" danger icon={<DeleteOutlined />} />
          </Popconfirm>
        </Space>
      ),
    },
  ];

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
          Clusters
        </Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          Add Cluster
        </Button>
      </div>

      <Table
        dataSource={clusters}
        columns={columns}
        rowKey="id"
        loading={loading}
        size="middle"
        pagination={{ pageSize: 10 }}
      />

      <Modal
        title={editingId ? "Edit Cluster" : "Add Cluster"}
        open={open}
        onCancel={() => setOpen(false)}
        onOk={handleSubmit}
        okText={editingId ? "Update" : "Create"}
        width={640}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="clusterId"
            label="Cluster ID"
            rules={[{ required: true, message: "Required" }]}
          >
            <Input placeholder="e.g. user-service-cluster" />
          </Form.Item>

          {/* ── Destinations ── */}
          <Divider orientation="left" plain>
            Destinations (Primary + Failover)
          </Divider>

          {destinations.map((dest, index) => (
            <Row key={index} gutter={8} style={{ marginBottom: 8 }}>
              <Col flex="auto">
                <Input
                  placeholder="http://localhost:5001"
                  value={dest.address}
                  onChange={(e) =>
                    updateDestination(index, "address", e.target.value)
                  }
                />
              </Col>
              <Col>
                <Select
                  value={dest.health}
                  onChange={(v) => updateDestination(index, "health", v)}
                  style={{ width: 120 }}
                  options={[
                    { label: "✅ Primary", value: "Active" },
                    { label: "⏳ Standby", value: "Standby" },
                  ]}
                />
              </Col>
              <Col>
                <Button
                  type="text"
                  danger
                  icon={<MinusCircleOutlined />}
                  disabled={destinations.length <= 1}
                  onClick={() => removeDestination(index)}
                />
              </Col>
            </Row>
          ))}

          <Button
            type="dashed"
            block
            icon={<PlusCircleOutlined />}
            onClick={addDestination}
            style={{ marginBottom: 16 }}
          >
            Add Destination
          </Button>

          {/* ── Health Check ── */}
          <Divider orientation="left" plain>
            Health Check (Failover)
          </Divider>
          <Card size="small" style={{ marginBottom: 16 }}>
            <Text type="secondary" style={{ display: "block", marginBottom: 12 }}>
              Khi bật Health Check, gateway sẽ tự động probe các destinations. Nếu primary bị
              down, traffic tự động chuyển sang standby.
            </Text>
            <Row gutter={16}>
              <Col span={6}>
                <Form.Item
                  name="enableHealthCheck"
                  label="Enable"
                  valuePropName="checked"
                >
                  <Switch />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="healthCheckPath" label="Path">
                  <Input placeholder="/health" />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="healthCheckIntervalSeconds" label="Interval (s)">
                  <InputNumber min={1} max={300} style={{ width: "100%" }} />
                </Form.Item>
              </Col>
              <Col span={6}>
                <Form.Item name="healthCheckTimeoutSeconds" label="Timeout (s)">
                  <InputNumber min={1} max={60} style={{ width: "100%" }} />
                </Form.Item>
              </Col>
            </Row>
          </Card>

          {/* ── Load Balancing ── */}
          <Form.Item name="loadBalancingPolicy" label="Load Balancing Policy">
            <Select options={LB_POLICIES} />
          </Form.Item>

          {/* ── Retry Policy ── */}
          <Divider orientation="left" plain>
            Retry Policy
          </Divider>
          <Card size="small">
            <Form.Item name="enableRetry" label="Enable Retry" valuePropName="checked">
              <Switch checkedChildren="ON" unCheckedChildren="OFF" />
            </Form.Item>
            <Form.Item noStyle shouldUpdate>
              {({ getFieldValue }) => getFieldValue("enableRetry") && (
                <Row gutter={16}>
                  <Col span={12}>
                    <Form.Item name="retryCount" label="Max Retries">
                      <InputNumber min={1} max={10} placeholder="3" style={{ width: "100%" }} />
                    </Form.Item>
                  </Col>
                  <Col span={12}>
                    <Form.Item name="retryDelayMs" label="Delay (ms)">
                      <InputNumber min={100} max={30000} step={100} placeholder="1000" style={{ width: "100%" }} />
                    </Form.Item>
                  </Col>
                </Row>
              )}
            </Form.Item>
          </Card>
        </Form>
      </Modal>
    </div>
  );
}
