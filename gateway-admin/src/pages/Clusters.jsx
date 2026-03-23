import React, { useEffect, useState } from "react";
import {
  Table,
  Button,
  Modal,
  Form,
  Input,
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
  getClusters,
  createCluster,
  updateCluster,
  deleteCluster,
} from "../api/gatewayApi";

const { Title } = Typography;

export default function Clusters() {
  const { message } = App.useApp();
  const [clusters, setClusters] = useState([]);
  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try {
      setClusters((await getClusters()).data);
    } catch (err) {
      message.error("Failed to load clusters: " + (err.response?.data?.error || err.message));
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
    // Parse destinationsJson back to newline-separated addresses
    let destinations = "";
    try {
      const dests = JSON.parse(record.destinationsJson || "[]");
      destinations = dests.map((d) => d.address).join("\n");
    } catch {
      destinations = record.destinationsJson;
    }
    form.setFieldsValue({
      clusterId: record.clusterId,
      destinations,
    });
    setEditingId(record.id);
    setOpen(true);
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      const destList = (values.destinations || "")
        .split("\n")
        .filter((line) => line.trim())
        .map((line, i) => ({
          id: `dest-${i + 1}`,
          address: line.trim(),
        }));

      const payload = {
        clusterId: values.clusterId,
        destinationsJson: JSON.stringify(destList),
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

  // Parse destinations for display
  const parseDestinations = (json) => {
    try {
      const dests = JSON.parse(json || "[]");
      return dests.map((d) => d.address);
    } catch {
      return [json];
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
      render: (json) => (
        <Space direction="vertical" size={2}>
          {parseDestinations(json).map((addr, i) => (
            <Tag key={i} color="cyan">
              {addr}
            </Tag>
          ))}
        </Space>
      ),
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
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 16 }}>
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
          <Form.Item
            name="destinations"
            label="Destinations (one address per line)"
            rules={[{ required: true, message: "At least one destination" }]}
          >
            <Input.TextArea
              rows={4}
              placeholder={"http://localhost:5001\nhttp://localhost:5002"}
            />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
