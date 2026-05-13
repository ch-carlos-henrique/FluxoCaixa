import http from 'k6/http';
import { check } from 'k6';

export const options = {
  scenarios: {
    constant_request_rate: {
      executor: 'constant-arrival-rate',
      rate: 50,            // 50 requisições por segundo
      timeUnit: '1s',
      duration: '60s',
      preAllocatedVUs: 20, // VUs pré-alocadas para sustentar a taxa
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<200'], // p95 < 200ms
    http_req_failed:   ['rate<0.05'], // < 5% de erro
  },
};

export default function () {
  const res = http.get(
    'http://localhost:5002/api/consolidation/daily?merchantId=1&date=2026-05-12',
    { headers: { Authorization: `Bearer ${__ENV.JWT_TOKEN}` } }
  );
  check(res, { 'status 200': (r) => r.status === 200 });
}
