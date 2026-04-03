// PM2 Ecosystem file for auto-scaling
module.exports = {
  apps: [
    {
      name: 'gateway-node',
      script: './server-uarch.js',

      // Cluster mode - auto scale to CPU cores
      instances: 'max', // or number like 4
      exec_mode: 'cluster',

      // Auto-restart configuration
      watch: false, // Set true for development
      max_memory_restart: '500M',

      // Environment variables
      env: {
        NODE_ENV: 'production',
        PORT: 8887
      },

      // Logging
      error_file: './logs/error.log',
      out_file: './logs/out.log',
      log_date_format: 'YYYY-MM-DD HH:mm:ss Z',

      // Advanced features
      min_uptime: '10s',
      max_restarts: 10,
      autorestart: true,

      // Graceful shutdown
      kill_timeout: 5000,
      wait_ready: true,
      listen_timeout: 10000
    }
  ]
};
