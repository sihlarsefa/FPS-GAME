/**
 * 2 saatlik soak — sabit yük.
 * k6 run k6/soak-2h.js -e BASE_URL=http://localhost:5080
 * Kısa deneme: -e DURATION=5m -e VUS=100
 */
import { sleep } from 'k6';
import { fullPlayerFlow } from './lib/api.js';

const duration = __ENV.DURATION || '2h';
const vus = Number(__ENV.VUS || 2000);

export const options = {
  scenarios: {
    soak: {
      executor: 'constant-vus',
      vus,
      duration,
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<300', 'p(99)<600'],
    http_req_failed: ['rate<0.005'],
  },
};

export default function () {
  fullPlayerFlow(__VU, __ITER);
  sleep(0.05);
}
