import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5016';
const errorRate = new Rate('errors');

export const options = {
  scenarios: {
    spike: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '1m', target: 10 },
        { duration: '10s', target: 1000 },
        { duration: '3m', target: 1000 },
        { duration: '10s', target: 10 },
        { duration: '2m', target: 10 },
        { duration: '1m', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<3000'],
    http_req_failed: ['rate<0.10'],
    errors: ['rate<0.10'],
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/api/products`);
  const success = check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 3000ms': (r) => r.timings.duration < 3000,
  });
  errorRate.add(!success);
  sleep(0.1);
}
