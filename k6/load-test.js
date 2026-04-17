import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5016';

const errorRate = new Rate('errors');
const getAllTrend = new Trend('get_all_duration');
const getByIdTrend = new Trend('get_by_id_duration');
const searchTrend = new Trend('search_duration');

function isJsonArray(response) {
  try {
    return Array.isArray(response.json());
  } catch (error) {
    return false;
  }
}

export const options = {
  scenarios: {
    load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 50 },
        { duration: '4m', target: 50 },
        { duration: '30s', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_duration: ['p(50)<200', 'p(95)<500', 'p(99)<1500'],
    http_req_failed: ['rate<0.01'],
    errors: ['rate<0.01'],
  },
};

export default function () {
  group('Get All Products', () => {
    const res = http.get(`${BASE_URL}/api/products`);
    getAllTrend.add(res.timings.duration);

    const success = check(res, {
      'GET /products status 200': (r) => r.status === 200,
      'GET /products returns array': (r) => r.status === 200 && isJsonArray(r),
    });
    errorRate.add(!success);
  });

  sleep(1);

  group('Get Product by ID', () => {
    const randomId = Math.floor(Math.random() * 10000) + 1;
    const res = http.get(`${BASE_URL}/api/products/${randomId}`);
    getByIdTrend.add(res.timings.duration);

    const success = check(res, {
      'GET /products/{id} status 200 or 404': (r) => r.status === 200 || r.status === 404,
    });
    errorRate.add(!success);
  });

  sleep(1);

  group('Search Products', () => {
    const res = http.get(`${BASE_URL}/api/products/search?q=Product-001`);
    searchTrend.add(res.timings.duration);

    const success = check(res, {
      'GET /products/search status 200': (r) => r.status === 200,
      'search returns array': (r) => r.status === 200 && isJsonArray(r),
    });
    errorRate.add(!success);
  });

  sleep(1);
}
