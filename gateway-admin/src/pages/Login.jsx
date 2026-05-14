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
      background: 'linear-gradient(135deg, #09090b 0%, #1a1a2e 50%, #16213e 100%)',
      position: 'relative',
      overflow: 'hidden'
    }}>
      {/* Decorative blurred circles for premium feel */}
      <div style={{ position: 'absolute', width: 400, height: 400, background: 'rgba(22, 119, 255, 0.15)', borderRadius: '50%', filter: 'blur(80px)', top: '-10%', left: '-10%' }} />
      <div style={{ position: 'absolute', width: 300, height: 300, background: 'rgba(114, 46, 209, 0.15)', borderRadius: '50%', filter: 'blur(80px)', bottom: '-10%', right: '-10%' }} />
      
      <Card
        style={{
          width: 420,
          background: 'rgba(22, 27, 34, 0.55)',
          backdropFilter: 'blur(16px)',
          WebkitBackdropFilter: 'blur(16px)',
          boxShadow: '0 8px 32px rgba(0,0,0,0.3), inset 0 0 0 1px rgba(255,255,255,0.05)',
          borderRadius: 16,
          border: 'none',
          zIndex: 1
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
