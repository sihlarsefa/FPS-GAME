/**
 * Spike: düşük taban → ani 10k → toparlanma.
 * k6 run k6/spike.js -e BASE_URL=http://localhost:5080
 */
import { sleep } from 'k6';
import { fullPlayerFlow } from './lib/api.js';

export const options = {
  scenarios: {
    spike: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '1m', target: 500 },
        { duration: '2m', target: 500 },
        { duration: '30s', target: 10000 },
        { duration: '1m', target: 10000 },
        { duration: '30s', target: 500 },
        { duration: '2m', target: 500 },
        { duration: '1m', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<600', 'p(99)<1200'],
    http_req_failed: ['rate<0.05'],
  },
};

export default function () {
  fullPlayerFlow(__VU, __ITER);
  sleep(0.01);
}
