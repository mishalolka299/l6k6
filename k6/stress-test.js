import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5016';
const errorRate = new Rate('errors');

export const options = {
  scenarios: {
    stress: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '2m', target: 50 },
        { duration: '2m', target: 50 },
        { duration: '2m', target: 100 },
        { duration: '2m', target: 100 },
        { duration: '2m', target: 250 },
        { duration: '2m', target: 250 },
        { duration: '2m', target: 500 },
        { duration: '2m', target: 500 },
        { duration: '2m', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<2000'],
    http_req_failed: ['rate<0.05'],
    errors: ['rate<0.05'],
  },
};

export default function () {
  group('API Under Stress', () => {
    const res = http.get(`${BASE_URL}/api/products`);
    const success = check(res, {
      'status is 200': (r) => r.status === 200,
      'response time < 2000ms': (r) => r.timings.duration < 2000,
    });
    errorRate.add(!success);
  });

  sleep(0.5);
}
