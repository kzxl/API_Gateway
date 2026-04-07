const http = require('http');
const url = require('url');

const server = http.createServer((req, res) => {
  const parsedUrl = url.parse(req.url, true);
  const path = parsedUrl.pathname;

  res.setHeader('Content-Type', 'application/json');

  if (path === '/health') {
    res.writeHead(200);
    res.end(JSON.stringify({ status: 'ok', service: 'test-backend' }));
  } else if (path.startsWith('/users/')) {
    const userId = path.split('/')[2];
    res.writeHead(200);
    res.end(JSON.stringify({
      message: 'User endpoint',
      userId: userId,
      path: path,
      originalUrl: req.url
    }));
  } else if (path.startsWith('/products/')) {
    const productId = path.split('/')[2];
    res.writeHead(200);
    res.end(JSON.stringify({
      message: 'Product endpoint',
      productId: productId,
      path: path
    }));
  } else {
    res.writeHead(404);
    res.end(JSON.stringify({ error: 'Not found' }));
  }
});

const PORT = 5001;
server.listen(PORT, () => {
  console.log(`Test backend running on http://localhost:${PORT}`);
});
