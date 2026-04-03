// Core configuration
const JWT_SECRET = process.env.JWT_SECRET || 'GatewaySecretKey-Change-This-In-Production-Min32Chars!';
const PORT = process.env.PORT || 8887;
const NODE_ENV = process.env.NODE_ENV || 'development';

module.exports = {
  JWT_SECRET,
  PORT,
  NODE_ENV,
  JWT_ACCESS_EXPIRY: '15m',
  JWT_REFRESH_EXPIRY: '7d',
  ACCOUNT_LOCKOUT_ATTEMPTS: 5,
  ACCOUNT_LOCKOUT_DURATION: 30 * 60 * 1000, // 30 minutes
  RATE_LIMIT_WINDOW: 1000, // 1 second
  RATE_LIMIT_MAX: 100 // 100 requests per second
};
