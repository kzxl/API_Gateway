import React, { useEffect, useState } from "react";
import { Table, Button, Modal, Form, Input, Select, Popconfirm } from "antd";
import {
  getRoutes,
  saveRoute,
  deleteRoute,
  getClusters,
} from "../api/gatewayApi";

export default function Routes() {
  const [routes, setRoutes] = useState([]);
  const [clusters, setClusters] = useState([]);
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();

  const load = async () => {
    const [r, c] = await Promise.all([getRoutes(), getClusters()]);
    setRoutes(r.data);
    setClusters(c.data);
  };

  useEffect(() => {
    load();
  }, []);

  const handleSubmit = async () => {
    const values = await form.validateFields();
    await saveRoute(values);
    setOpen(false);
    load();
  };

  const handleDelete = async (id) => {
    await deleteRoute(id);
    load();
  };

  return (
    <div>
      <Button
        type="primary"
        onClick={() => {
          form.resetFields();
          setOpen(true);
        }}>
        + Add Route
      </Button>
      <Table
        dataSource={routes}
        rowKey="routeId"
        columns={[
          { title: "Route ID", dataIndex: "routeId" },
          { title: "Path", dataIndex: "matchPath" },
          { title: "Methods", dataIndex: "methods" },
          { title: "Cluster", dataIndex: "clusterId" },
          {
            title: "Action",
            render: (_, r) => (
              <Popconfirm
                title="Delete route?"
                onConfirm={() => handleDelete(r.routeId)}>
                <Button danger size="small">
                  Delete
                </Button>
              </Popconfirm>
            ),
          },
        ]}
      />

      <Modal
        title="Add/Edit Route"
        open={open}
        onCancel={() => setOpen(false)}
        onOk={handleSubmit}>
        <Form form={form} layout="vertical">
          <Form.Item
            name="routeId"
            label="Route ID"
            rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="matchPath" label="Path" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="methods" label="Methods (comma-separated)">
            <Input />
          </Form.Item>
          <Form.Item
            name="clusterId"
            label="Cluster"
            rules={[{ required: true }]}>
            <Select
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
