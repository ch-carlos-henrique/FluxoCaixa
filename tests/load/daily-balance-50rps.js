import http from 'k6/http';
import { check } from 'k6';

// Configurável por variáveis de ambiente
const BASE_URL  = __ENV.BASE_URL  || 'http://localhost';
const OPS_URL   = `${BASE_URL}:5001`;
const CONS_URL  = __ENV.CONS_URL  || `${BASE_URL}:5002`;
const TEST_DATE = __ENV.TEST_DATE || '2026-05-13';

// merchantId semeado no seed de dev (todos os merchant01-20 apontam para este ID)
const MERCHANT_ID = '00000000-0000-0000-0000-000000000001';

// 20 VUs × 1 token cada → cada VU fica em ~2,5 RPS, bem abaixo do limite de 600 req/min (10 RPS)
const NUM_USERS = 20;

export const options = {
  scenarios: {
    constant_request_rate: {
      executor:         'constant-arrival-rate',
      rate:             50,   // 50 requisições por segundo
      timeUnit:         '1s',
      duration:         '60s',
      preAllocatedVUs:  NUM_USERS,
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<200'], // p95 < 200ms
    http_req_failed:   ['rate<0.05'], // < 5% de erro de rede/5xx
  },
};

/**
 * setup() é executado uma única vez antes do teste.
 * Autentica os 20 usuários de carga e retorna seus tokens.
 * Cada VU usa o próprio token, evitando compartilhamento de bucket de rate limit.
 */
export function setup() {
  const tokens = [];

  for (let i = 1; i <= NUM_USERS; i++) {
    const email = `merchant${String(i).padStart(2, '0')}@fluxocaixa.dev`;
    const res = http.post(
      `${OPS_URL}/api/auth/login`,
      JSON.stringify({ email, password: 'Merchant@123' }),
      { headers: { 'Content-Type': 'application/json' } },
    );

    if (res.status !== 200) {
      throw new Error(`Login falhou para ${email}: HTTP ${res.status} — ${res.body}`);
    }

    tokens.push(JSON.parse(res.body).accessToken);
  }

  return { tokens };
}

/**
 * Função executada por cada VU a cada iteração.
 * Cada VU usa seu próprio token para isolar o bucket de rate limit.
 */
export default function (data) {
  // VU IDs são 1-based; tokens é 0-based
  const token = data.tokens[(__VU - 1) % data.tokens.length];

  const res = http.get(
    `${CONS_URL}/api/consolidation/daily?merchantId=${MERCHANT_ID}&date=${TEST_DATE}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );

  check(res, {
    // 200 = dados encontrados; 404 = sem dados no dia (esperado em dev sem transações)
    'resposta válida (200/404)': (r) => r.status === 200 || r.status === 404,
    'sem rate limiting (429)':   (r) => r.status !== 429,
    'sem erro de servidor (5xx)': (r) => r.status < 500,
  });
}
