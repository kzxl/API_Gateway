import React, { useEffect, useState } from "react";
import {
  Table,
  Button,
  Modal,
  Form,
  Input,
  Select,
  Popconfirm,
  Space,
  Tag,
  App,
  Typography,
} from "antd";
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
} from "@ant-design/icons";
import {
  getRoutes,
  createRoute,
  updateRoute,
  deleteRoute,
  getClusters,
} from "../api/gatewayApi";

const { Title } = Typography;

export default function Routes() {
  const { message } = App.useApp();
  const [routes, setRoutes] = useState([]);
  const [clusters, setClusters] = useState([]);
  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try {
      const [r, c] = await Promise.all([getRoutes(), getClusters()]);
      setRoutes(r.data);
      setClusters(c.data);
    } catch (err) {
      message.error("Failed to load routes: " + (err.response?.data?.error || err.message));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const openCreate = () => {
    form.resetFields();
    setEditingId(null);
    setOpen(true);
  };

  const openEdit = (record) => {
    form.setFieldsValue(record);
    setEditingId(record.id);
    setOpen(true);
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      if (editingId) {
        await updateRoute(editingId, { ...values, id: editingId });
        message.success("Route updated");
      } else {
        await createRoute(values);
        message.success("Route created");
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
      await deleteRoute(id);
      message.success("Route deleted");
      load();
    } catch (err) {
      message.error("Delete failed");
    }
  };

  const columns = [
    {
      title: "Route ID",
      dataIndex: "routeId",
      render: (v) => <Tag color="blue">{v}</Tag>,
    },
    {
      title: "Path",
      dataIndex: "matchPath",
      render: (v) => <code>{v}</code>,
    },
    {
      title: "Methods",
      dataIndex: "methods",
      render: (v) =>
        v
          ? v.split(",").map((m) => (
              <Tag key={m} color="green">
                {m.trim()}
              </Tag>
            ))
          : <Tag>ALL</Tag>,
    },
    {
      title: "Cluster",
      dataIndex: "clusterId",
      render: (v) => <Tag color="purple">{v}</Tag>,
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
            title="Delete this route?"
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
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 16 }}>
        <Title level={3} style={{ margin: 0 }}>
          Routes
        </Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          Add Route
        </Button>
      </div>

      <Table
        dataSource={routes}
        columns={columns}
        rowKey="id"
        loading={loading}
        size="middle"
        pagination={{ pageSize: 10 }}
      />

      <Modal
        title={editingId ? "Edit Route" : "Add Route"}
        open={open}
        onCancel={() => setOpen(false)}
        onOk={handleSubmit}
        okText={editingId ? "Update" : "Create"}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="routeId"
            label="Route ID"
            rules={[{ required: true, message: "Required" }]}
          >
            <Input placeholder="e.g. user-api-route" />
          </Form.Item>
          <Form.Item
            name="matchPath"
            label="Match Path"
            rules={[{ required: true, message: "Required" }]}
          >
            <Input placeholder="e.g. /api/users/{**catch-all}" />
          </Form.Item>
          <Form.Item name="methods" label="Methods (comma-separated)">
            <Input placeholder="e.g. GET,POST or leave empty for ALL" />
          </Form.Item>
          <Form.Item
            name="clusterId"
            label="Cluster"
            rules={[{ required: true, message: "Required" }]}
          >
            <Select
              placeholder="Select a cluster"
              options={clusters.map((c) => ({
                label: c.clusterId,
                value: c.clusterId,
              }))}
            />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
