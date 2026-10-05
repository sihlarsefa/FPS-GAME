/**
 * 10k eşzamanlı — kademeli ramp.
 * k6 run k6/ramp-10k.js -e BASE_URL=http://localhost:5080
 */
import { sleep } from 'k6';
import { fullPlayerFlow } from './lib/api.js';

export const options = {
  scenarios: {
    ramp_10k: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '2m', target: 1000 },
        { duration: '2m', target: 2500 },
        { duration: '2m', target: 5000 },
        { duration: '3m', target: 7500 },
        { duration: '3m', target: 10000 },
        { duration: '2m', target: 10000 },
        { duration: '2m', target: 0 },
      ],
      gracefulRampDown: '1m',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<400', 'p(99)<800'],
    http_req_failed: ['rate<0.02'],
  },
};

export default function () {
  fullPlayerFlow(__VU, __ITER);
  sleep(0.02);
}
