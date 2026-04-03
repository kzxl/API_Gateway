// Core metrics tracking (singleton)
class MetricsService {
  constructor() {
    this.metrics = {
      totalRequests: 0,
      successRequests: 0,
      failedRequests: 0,
      totalLatency: 0,
      wsConnections: 0,
      wsMessages: 0,
      startTime: Date.now()
    };
  }

  incrementRequests() {
    this.metrics.totalRequests++;
  }

  incrementSuccess() {
    this.metrics.successRequests++;
  }

  incrementFailed() {
    this.metrics.failedRequests++;
  }

  addLatency(latency) {
    this.metrics.totalLatency += latency;
  }

  incrementWsConnections() {
    this.metrics.wsConnections++;
  }

  decrementWsConnections() {
    this.metrics.wsConnections--;
  }

  incrementWsMessages() {
    this.metrics.wsMessages++;
  }

  getMetrics() {
    const uptime = Math.floor((Date.now() - this.metrics.startTime) / 1000);
    const avgLatency = this.metrics.totalRequests > 0
      ? Math.round(this.metrics.totalLatency / this.metrics.totalRequests)
      : 0;

    return {
      totalRequests: this.metrics.totalRequests,
      successRequests: this.metrics.successRequests,
      failedRequests: this.metrics.failedRequests,
      successRate: this.metrics.totalRequests > 0
        ? ((this.metrics.successRequests / this.metrics.totalRequests) * 100).toFixed(2)
        : 0,
      avgLatency,
      wsConnections: this.metrics.wsConnections,
      wsMessages: this.metrics.wsMessages,
      uptime,
      timestamp: new Date().toISOString()
    };
  }
}

// Singleton instance
const metricsService = new MetricsService();

module.exports = metricsService;
