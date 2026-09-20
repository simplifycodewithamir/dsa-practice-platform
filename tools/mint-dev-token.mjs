#!/usr/bin/env node
// Mints a bearer token the locally-run Api will accept, for the times when there is no identity
// provider to get one from: the end-to-end stack in CI, and a checkout with no tenant of its own.
//
// It is deliberately *not* in the Api. Decision D4: the Api validates tokens and never issues
// them, so there is no token-minting code in the deployed binary to expose by accident. The Api
// refuses to start with a symmetric signing key in Production, so nothing minted here is ever
// trusted by a real deployment.
//
//   node tools/mint-dev-token.mjs                       # a fresh key and a token for it
//   node tools/mint-dev-token.mjs --key <base64>        # a token for a key you already have
//   node tools/mint-dev-token.mjs --key <base64> --token-only
//
// The default output is .env lines: DEV_JWT_* configure the Api (docker-compose passes them
// through), and VITE_DEV_ACCESS_TOKEN is what the frontend build bakes in.

import { createHmac, randomBytes } from 'node:crypto';

const args = process.argv.slice(2);

function option(name, fallback) {
  const index = args.indexOf(`--${name}`);
  return index === -1 ? fallback : args[index + 1];
}

const issuer = option('issuer', 'dsa-practice-dev');
const audience = option('audience', 'dsa-practice-api');
const subject = option('subject', 'local-dev-user');
const displayName = option('name', 'Local Developer');
const hours = Number(option('hours', '24'));
const tokenOnly = args.includes('--token-only');

// 32 bytes: HS256's block size, and what the Api's SigningKeys:Length says.
const keyBase64 = option('key', randomBytes(32).toString('base64'));

const base64Url = (value) => Buffer.from(value).toString('base64url');

const issuedAt = Math.floor(Date.now() / 1000);
const header = base64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
const payload = base64Url(
  JSON.stringify({
    // `sub` and `iss` together are the identity the Api provisions a user row from (decision D3).
    sub: subject,
    name: displayName,
    iss: issuer,
    aud: audience,
    iat: issuedAt,
    exp: issuedAt + Math.round(hours * 3600),
  }),
);

const signature = createHmac('sha256', Buffer.from(keyBase64, 'base64'))
  .update(`${header}.${payload}`)
  .digest('base64url');

const token = `${header}.${payload}.${signature}`;

if (tokenOnly) {
  process.stdout.write(`${token}\n`);
} else {
  process.stdout.write(
    [
      `DEV_JWT_ISSUER=${issuer}`,
      `DEV_JWT_AUDIENCE=${audience}`,
      `DEV_JWT_SIGNING_KEY=${keyBase64}`,
      `VITE_DEV_ACCESS_TOKEN=${token}`,
      '',
    ].join('\n'),
  );
}
