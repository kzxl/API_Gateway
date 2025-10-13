import React, { useEffect, useState } from "react";
import { Table, Button, Modal, Form, Input, Popconfirm } from "antd";
import { getClusters, saveCluster, deleteCluster } from "../api/gatewayApi";

export default function Clusters() {
  const [clusters, setClusters] = useState([]);
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();

  const load = async () => setClusters((await getClusters()).data);

  useEffect(() => {
    load();
  }, []);

  const handleSubmit = async () => {
    const values = await form.validateFields();
    // convert destinations: newline to JSON
    const destList = values.destinations.split("\n").map((line, i) => ({
      id: `dest-${i + 1}`,
      address: line.trim(),
    }));
    await saveCluster({
      ...values,
      destinationsJson: JSON.stringify(destList),
    });
    setOpen(false);
    load();
  };

  const handleDelete = async (id) => {
    await deleteCluster(id);
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
        + Add Cluster
      </Button>
      <Table
        dataSource={clusters}
        rowKey="clusterId"
        columns={[
          { title: "Cluster ID", dataIndex: "clusterId" },
          { title: "Destinations", dataIndex: "destinationsJson" },
          {
            title: "Action",
            render: (_, r) => (
              <Popconfirm
                title="Delete cluster?"
                onConfirm={() => handleDelete(r.clusterId)}>
                <Button danger size="small">
                  Delete
                </Button>
              </Popconfirm>
            ),
          },
        ]}
      />

      <Modal
        title="Add/Edit Cluster"
        open={open}
        onCancel={() => setOpen(false)}
        onOk={handleSubmit}>
        <Form form={form} layout="vertical">
          <Form.Item
            name="clusterId"
            label="Cluster ID"
            rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="destinations" label="Destinations (1 per line)">
            <Input.TextArea rows={4} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
