import React, { useState } from "react";
import { Card, Typography, Button, Space, Upload, App, Divider, Descriptions, Tag } from "antd";
import { DownloadOutlined, UploadOutlined, SettingOutlined } from "@ant-design/icons";
import { exportConfig, importConfig } from "../api/gatewayApi";

const { Title, Text, Paragraph } = Typography;

export default function Settings() {
  const { message, modal } = App.useApp();
  const [exporting, setExporting] = useState(false);

  const handleExport = async () => {
    setExporting(true);
    try {
      const res = await exportConfig();
      const blob = new Blob([JSON.stringify(res.data, null, 2)], { type: "application/json" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `gateway-config-${new Date().toISOString().slice(0, 10)}.json`;
      a.click();
      URL.revokeObjectURL(url);
      message.success("Config exported");
    } catch { message.error("Export failed"); }
    finally { setExporting(false); }
  };

  const handleImport = (file) => {
    const reader = new FileReader();
    reader.onload = async (e) => {
      try {
        const data = JSON.parse(e.target.result);
        modal.confirm({
          title: "Import Configuration?",
          content: `This will import ${data.routes?.length ?? 0} routes and ${data.clusters?.length ?? 0} clusters. Existing entries with same IDs will be updated.`,
          onOk: async () => {
            const res = await importConfig(data);
            message.success(`Imported: ${res.data.routesImported} routes, ${res.data.clustersImported} clusters`);
          },
        });
      } catch { message.error("Invalid JSON file"); }
    };
    reader.readAsText(file);
    return false;
  };

  return (
    <div>
      <Title level={3}><SettingOutlined /> Settings</Title>

      <Card title="Configuration Backup" style={{ marginBottom: 24 }}>
        <Paragraph>Export or import your gateway configuration (routes, clusters, users).</Paragraph>
        <Space size="large">
          <Button type="primary" icon={<DownloadOutlined />} loading={exporting} onClick={handleExport} size="large">
            Export Config
          </Button>
          <Upload beforeUpload={handleImport} showUploadList={false} accept=".json">
            <Button icon={<UploadOutlined />} size="large">Import Config</Button>
          </Upload>
        </Space>
      </Card>

      <Card title="Gateway Info">
        <Descriptions column={1} bordered size="small">
          <Descriptions.Item label="Version"><Tag color="blue">1.0.0</Tag></Descriptions.Item>
          <Descriptions.Item label="Backend">http://localhost:5151</Descriptions.Item>
          <Descriptions.Item label="Auth">JWT Bearer</Descriptions.Item>
          <Descriptions.Item label="Proxy Engine">YARP (Yet Another Reverse Proxy)</Descriptions.Item>
          <Descriptions.Item label="Database">SQLite</Descriptions.Item>
          <Descriptions.Item label="Features">
            <Space wrap>
              <Tag color="green">Rate Limiting</Tag>
              <Tag color="green">Circuit Breaker</Tag>
              <Tag color="green">IP Filter</Tag>
              <Tag color="green">Health Check</Tag>
              <Tag color="green">Failover</Tag>
              <Tag color="green">Request Logging</Tag>
              <Tag color="green">JWT Auth</Tag>
              <Tag color="green">Traffic Metrics</Tag>
              <Tag color="green">Response Caching</Tag>
              <Tag color="green">Request Transforms</Tag>
              <Tag color="green">Config Backup</Tag>
              <Tag color="green">Retry Policy</Tag>
            </Space>
          </Descriptions.Item>
        </Descriptions>
      </Card>
    </div>
  );
}
