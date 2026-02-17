import http from 'k6/http';
import { check, sleep } from 'k6';
import { randomString } from 'https://jslib.k6.io/k6-utils/1.2.0/index.js';

export const options = {
  stages: [
    { duration: '30s', target: 50 }, // Ramp to 50 users
    { duration: '1m', target: 200 }, // Load test at 200 users
    { duration: '30s', target: 0 },  // Cooldown
  ],
  thresholds: {
    http_req_duration: ['p(95)<100'], // 95% of requests must be faster than 100ms
    http_req_failed: ['rate<0.01'],   // Error rate < 1%
  },
};

const BASE_URL = 'http://localhost:5000';

export default function () {
  // 1. Create a Short URL (Write)
  const payload = JSON.stringify({
    long_url: `https://example.com/page/${randomString(10)}`,
    expires_in_days: 30,
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
  };

  const createRes = http.post(`${BASE_URL}/api/v1/shorten`, payload, params);

  check(createRes, {
    'created status is 201': (r) => r.status === 201,
    'has short_code': (r) => r.json('shortCode') !== undefined,
  });

  if (createRes.status === 201) {
    const shortCode = createRes.json('shortCode');

    // 2. Access the Short URL (Read) - Simulate Instant Hit (Likely Cache Miss first time logic depends on implementation)
    // We allow a small sleep to simulate user behavior
    sleep(1);

    const redirectRes = http.get(`${BASE_URL}/${shortCode}`, {
        redirects: 0 // We want to check the 302/301 response, not follow it
    });

    check(redirectRes, {
      'redirect status is 302': (r) => r.status === 302,
      'location header present': (r) => r.headers['Location'] !== undefined,
    });
  }
}
