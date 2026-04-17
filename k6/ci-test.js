import http from 'k6/http';
import { check, sleep, group } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5016';

export const options = {
  vus: 10,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1500'],
  },
};

function isJsonArray(response) {
  try {
    return Array.isArray(response.json());
  } catch {
    return false;
  }
}

export default function () {
  group('Products API', () => {
    const list = http.get(`${BASE_URL}/api/products`);
    check(list, {
      'GET /api/products 200': (r) => r.status === 200,
      'GET /api/products array': (r) => r.status === 200 && isJsonArray(r),
    });

    const byId = http.get(`${BASE_URL}/api/products/1`);
    check(byId, {
      'GET /api/products/1 200|404': (r) => r.status === 200 || r.status === 404,
    });

    const search = http.get(`${BASE_URL}/api/products/search?q=Product-001`);
    check(search, {
      'GET /api/products/search 200': (r) => r.status === 200,
      'GET /api/products/search array': (r) => r.status === 200 && isJsonArray(r),
    });
  });

  sleep(1);
}
