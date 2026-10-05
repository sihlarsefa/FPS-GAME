/**
 * Temel senaryo: kayıt, giriş, 10'luk tim akışı, matchmaking, sahte maç sonucu.
 * Çalıştırma: k6 run k6/harekat-scenario.js -e BASE_URL=http://localhost:5080
 */
import { sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import { fullPlayerFlow } from './lib/api.js';

export const errorRate = new Rate('harekat_errors');
export const flowDuration = new Trend('harekat_flow_ms');

export const options = {
  scenarios: {
    players: {
      executor: 'constant-vus',
      vus: Number(__ENV.VUS || 20),
      duration: __ENV.DURATION || '2m',
    },
  },
  thresholds: {
    http_req_duration: ['p(50)<100', 'p(95)<250', 'p(99)<500'],
    http_req_failed: ['rate<0.01'],
    harekat_errors: ['rate<0.01'],
  },
};

export default function () {
  const start = Date.now();
  try {
    fullPlayerFlow(__VU, __ITER);
  } catch (e) {
    errorRate.add(1);
  }
  flowDuration.add(Date.now() - start);
  sleep(Number(__ENV.THINK || 0.05));
}
