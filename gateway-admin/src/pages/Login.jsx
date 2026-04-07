import React, { useState } from 'react';
import { Form, Input, Button, Card, Typography, App, Space } from 'antd';
import { UserOutlined, LockOutlined, ApiOutlined, LinkOutlined } from '@ant-design/icons';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import { setApiBaseUrl } from '../api/gatewayApi';

const { Title, Text } = Typography;

/**
 * Login Page with optimized UX.
 * UArch: Minimal state, optimistic updates.
 */
export default function Login() {
  const { login } = useAuth();
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);

  const handleLogin = async (values) => {
    setLoading(true);
    try {
      // Set API base URL before login
      setApiBaseUrl(values.apiUrl);

      await login(values.username, values.password);
      message.success('Login successful');
      navigate('/');
    } catch (err) {
      message.error(err.response?.data?.error || 'Login failed. Check API URL and credentials.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
      minHeight: '100vh',
      background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)'
    }}>
      <Card
        style={{
          width: 420,
          boxShadow: '0 8px 32px rgba(0,0,0,0.1)',
          borderRadius: 8
        }}
      >
        <div style={{ textAlign: 'center', marginBottom: 32 }}>
          <ApiOutlined style={{ fontSize: 56, color: '#1677ff', marginBottom: 16 }} />
          <Title level={2} style={{ margin: 0 }}>API Gateway</Title>
          <Text type="secondary">Admin Control Panel</Text>
        </div>

        <Form
          onFinish={handleLogin}
          size="large"
          initialValues={{
            apiUrl: localStorage.getItem('apiBaseUrl') || 'http://localhost:8887',
            username: 'admin',
            password: 'admin123'
          }}
        >
          <Form.Item
            name="apiUrl"
            rules={[{ required: true, message: 'Please enter API URL' }]}
          >
            <Input
              prefix={<LinkOutlined />}
              placeholder="API URL (e.g., http://localhost:8887)"
            />
          </Form.Item>

          <Form.Item
            name="username"
            rules={[{ required: true, message: 'Please enter username' }]}
          >
            <Input
              prefix={<UserOutlined />}
              placeholder="Username"
              autoComplete="username"
            />
          </Form.Item>

          <Form.Item
            name="password"
            rules={[{ required: true, message: 'Please enter password' }]}
          >
            <Input.Password
              prefix={<LockOutlined />}
              placeholder="Password"
              autoComplete="current-password"
            />
          </Form.Item>

          <Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              block
              loading={loading}
              size="large"
            >
              Login
            </Button>
          </Form.Item>

          <div style={{ textAlign: 'center', marginTop: 16 }}>
            <Text type="secondary" style={{ fontSize: 12 }}>
              Default: admin / admin123
            </Text>
          </div>
        </Form>
      </Card>
    </div>
  );
}
