import React, { useEffect, useState } from "react";
import {
  Table, Card, Typography, Button, Space, Modal, Form, Input, Select, Switch, Tag, App, Popconfirm,
} from "antd";
import { PlusOutlined, ReloadOutlined, UserOutlined, EditOutlined, DeleteOutlined } from "@ant-design/icons";
import { getUsers, createUser, updateUser, deleteUser } from "../api/gatewayApi";

const { Title } = Typography;

export default function Users() {
  const { message } = App.useApp();
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try { const res = await getUsers(); setUsers(res.data); }
    catch { message.error("Failed to load users"); }
    finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const openModal = (user = null) => {
    setEditing(user);
    form.resetFields();
    if (user) form.setFieldsValue({ username: user.username, role: user.role, isActive: user.isActive });
    setModalOpen(true);
  };

  const handleSave = async () => {
    const values = await form.validateFields();
    if (editing) {
      await updateUser(editing.id, values);
      message.success("User updated");
    } else {
      await createUser(values);
      message.success("User created");
    }
    setModalOpen(false);
    load();
  };

  const handleDelete = async (id) => {
    await deleteUser(id);
    message.success("User deleted");
    load();
  };

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 16 }}>
        <Title level={3} style={{ margin: 0 }}><UserOutlined /> User Management</Title>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={load}>Refresh</Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => openModal()}>Add User</Button>
        </Space>
      </div>

      <Table dataSource={users} rowKey="id" loading={loading} size="small"
        columns={[
          { title: "Username", dataIndex: "username", render: (v) => <Tag color="blue">{v}</Tag> },
          { title: "Role", dataIndex: "role", render: (v) => <Tag color={v === "Admin" ? "red" : "green"}>{v}</Tag> },
          { title: "Active", dataIndex: "isActive", render: (v) => v ? <Tag color="green">Active</Tag> : <Tag color="default">Inactive</Tag> },
          { title: "Created", dataIndex: "createdAt", render: (v) => new Date(v).toLocaleString() },
          {
            title: "Actions", render: (_, record) => (
              <Space>
                <Button size="small" icon={<EditOutlined />} onClick={() => openModal(record)} />
                <Popconfirm title="Delete user?" onConfirm={() => handleDelete(record.id)}>
                  <Button size="small" danger icon={<DeleteOutlined />} />
                </Popconfirm>
              </Space>
            ),
          },
        ]}
      />

      <Modal title={editing ? "Edit User" : "New User"} open={modalOpen}
        onOk={handleSave} onCancel={() => setModalOpen(false)}>
        <Form form={form} layout="vertical">
          <Form.Item name="username" label="Username" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="password" label="Password" rules={editing ? [] : [{ required: true }]}>
            <Input.Password placeholder={editing ? "Leave blank to keep current" : ""} />
          </Form.Item>
          <Form.Item name="role" label="Role" initialValue="User">
            <Select options={[{ value: "Admin" }, { value: "User" }]} />
          </Form.Item>
          {editing && (
            <Form.Item name="isActive" label="Active" valuePropName="checked">
              <Switch />
            </Form.Item>
          )}
        </Form>
      </Modal>
    </div>
  );
}
