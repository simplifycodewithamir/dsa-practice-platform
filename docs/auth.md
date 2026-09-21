# Authentication

Who someone is, where that comes from, and how to configure it — for a real identity provider, for
a local machine, and for CI.

The shape is decision **D3**: the Api is *only* a resource server. It validates tokens and issues
none. The SPA is a *public client* doing Authorization Code with PKCE. A thin local `Users` table
maps `(issuer, subject)` to an internal `Guid`, so changing provider never orphans anyone's history.

---

## The flow, once

```
 browser                     identity provider                 Api
    │                                │                          │
    │ 1. GET /authorize                                          │
    │    + code_challenge (S256)     │                          │
    │───────────────────────────────>│                          │
    │        (the person signs in with Google or GitHub)         │
    │ 2. redirect to /auth/callback  │                          │
    │    ?code=…&state=…             │                          │
    │<───────────────────────────────│                          │
    │ 3. POST /oauth/token           │                          │
    │    code + code_verifier        │                          │
    │───────────────────────────────>│                          │
    │ 4. access token (RS256, JWT)   │                          │
    │<───────────────────────────────│                          │
    │ 5. POST /api/v1/submissions,  Authorization: Bearer …      │
    │───────────────────────────────────────────────────────────>
    │                                │  6. GET /.well-known/     │
    │                                │     openid-configuration  │
    │                                │     → jwks_uri → keys     │
    │                                │<──────────────────────────│
    │ 7. 201, and a Users row provisioned on first sight         │
    │<───────────────────────────────────────────────────────────│
```

**Why PKCE and not a client secret.** A single-page app cannot keep a secret: anything shipped to
the browser is readable. PKCE replaces the secret with a per-request proof — the app sends the hash
of a random verifier up front and the verifier itself when redeeming the code, so a code stolen in
transit is worthless to whoever stole it.

**Why the Api never sees the key.** `Authority` points at the provider; the framework fetches its
JWKS document and caches it, and re-fetches when a token arrives signed by a key id it does not
know. Key rotation therefore needs no deployment and no configuration change — which is the whole
reason to prefer it over a key you copy somewhere.

**Why step 6 is not per request.** The document is cached. A JWKS fetch on every submission would
make the provider a hard dependency of judging, not just of signing in.

---

## The provider: Auth0

Picked in item 20 after a fresh free-tier check. 25,000 monthly active users free with no card
required; Google and GitHub are both one-click social connections; and it is a plain OIDC provider,
so the Api is `Authority` + audience and nothing else. (Entra External ID's larger free tier needs a
linked Azure subscription and has no built-in GitHub connection; Clerk's SDK owns the flow, which
makes swapping it out a rewrite rather than a configuration change; self-hosted Keycloak puts login
availability on the same VM as the judge.)

Nothing below is code. Set it up once in the dashboard.

### 1 · Tenant

There is no separate "create tenant" step for your first one: **signing up creates it**. Go to
[auth0.com/signup](https://auth0.com/signup) and sign in with Google or GitHub — no card is asked
for on the free plan.

Signup then asks for the two things that are **permanent**:

| | |
|---|---|
| **Tenant name** | lowercase letters, numbers and hyphens, 3–63 characters, globally unique — e.g. `dsa-practice`. It **cannot be changed**, or reused after the tenant is deleted. |
| **Region** | AU, CA, EU, JP, UK or US. Pick the one closest to your users; there is no India region, so EU or AU. The sub-locality (`eu-2`, `us-3`, …) is assigned for you. |

Both become the domain, and the domain is the issuer:
`https://<tenant>.<region>.auth0.com/` — **the trailing slash matters**, discovery fails without it.

Anything else signup asks (use case, company size) is marketing, not configuration.

A second tenant, later, is the dropdown at the top left of the dashboard → *Create tenant*. Auth0
recommends one per environment; this project does not need one until there is something deployed
to keep separate from local development.

### 2 · An API (this is the audience)

*Applications → APIs → Create API*.

| Field | Value |
|---|---|
| Name | DSA Practice API |
| Identifier | `https://api.<your-domain>` — an identifier, never fetched, so it need not resolve |
| Signing algorithm | RS256 |

Enable **Allow Offline Access** if you want refresh tokens (the SPA asks for `offline_access`).

The identifier is what goes in `ValidAudiences` and in `VITE_OIDC_AUDIENCE`. Without it Auth0 issues
an opaque token for its own `/userinfo`, not a JWT for this Api — and an Api that does not check the
audience would accept that token, which is why the Api refuses to start without one configured.

### 3 · An application (the SPA)

*Applications → Applications → Create → Single Page Application*. Then, in its settings:

| Field | Value |
|---|---|
| Allowed Callback URLs | `http://localhost:5173/auth/callback`, `http://localhost:4173/auth/callback`, `https://<your-domain>/auth/callback` |
| Allowed Logout URLs | `http://localhost:5173`, `http://localhost:4173`, `https://<your-domain>` |
| Allowed Web Origins | the same three origins |
| Refresh Token Rotation | on, with reuse detection |

The client id is public — it is in the bundle either way. There is no client secret to configure,
because a SPA is a public client.

### 4 · Social connections

*Authentication → Social* → Google and GitHub, then enable both for the SPA application. Auth0's
own development keys work for trying it out and are rate-limited and shared; create real OAuth apps
at Google and GitHub before launch (item 30).

---

## Configuring the Api

All of it is configuration — `Authentication:Schemes:Bearer` is bound by `AddJwtBearer()` itself
(decision D4), so pointing the Api at a provider changes no code.

```bash
Auth__RequireAuthentication=true
Authentication__Schemes__Bearer__Authority=https://<tenant>.<region>.auth0.com/
Authentication__Schemes__Bearer__ValidAudiences__0=https://api.<your-domain>
```

`AuthConfigurationValidator` refuses to start on the three mistakes that would otherwise be silent:

| Configuration | Why it is refused |
|---|---|
| enforcement on, no `Authority` and no signing keys | every request is a 401, forever, and nothing says so |
| enforcement on, no audience | a token the same issuer minted for any other API is accepted here |
| a symmetric `SigningKeys` entry in Production | those are development keys; anything holding one can mint any subject |
| a `SigningKeys` entry with an empty value | a `.env` missing its `DEV_JWT_*` lines |

**Roles are not read from the token.** `Users.Role` is ours. Changing provider cannot change who is
an admin, and no provider's configuration mistake can grant it.

## Configuring the SPA

`frontend/.env.local` (copy `frontend/.env.example`):

```bash
VITE_OIDC_AUTHORITY=https://<tenant>.<region>.auth0.com/
VITE_OIDC_CLIENT_ID=<the SPA application's client id>
VITE_OIDC_AUDIENCE=https://api.<your-domain>
```

All three are baked into the bundle and readable by anyone. That is fine: a public client has no
secret. **With the authority unset the app runs signed-out** — questions readable, submitting not
offered — which is what a checkout with no tenant of its own does.

### Where the tokens live

Access and refresh tokens are held in memory (`InMemoryWebStorage`), so nothing readable is left on
the device and a token cannot outlive its tab. The cost is a hard refresh re-running the redirect;
it is silent while the session cookie at the provider is alive, because that cookie is first-party
*there*, so nothing about third-party cookie blocking applies to it.

The PKCE verifier is the exception: it goes to `sessionStorage`, because it is the one value that
must survive the redirect. It is single-use and worthless once the code is redeemed.

A **BFF** — tokens server-side, an `HttpOnly` cookie to the browser — is the stricter option, and is
what to revisit if this ever holds anything worth stealing (item 35). It costs a server-side session
store and a stateful hop in front of a currently static frontend, which is not a trade worth making
for a practice site before launch.

---

## Local development, without a provider

Two ways, depending on what you are working on.

### Hitting the Api directly (Scalar, curl, the debugger)

`dotnet user-jwts` writes `Authentication:Schemes:Bearer` into user secrets and prints a token. No
tenant, no network, and no token-minting code in the Api (D4):

```bash
dotnet user-jwts create --project source/DsaPractice.Api --audience dsa-practice-api --name local-dev-user
```

Paste the token into Scalar's **Authorize** box — the OpenAPI document declares the bearer scheme
and marks the endpoints that need it.

Or don't configure anything and set `Auth__RequireAuthentication=false`: submissions are then
attributed to a single `local-development/anonymous` user, so the foreign key still holds and the
judging loop still works. You lose the ability to see anything about ownership, which is the point
of having it on.

### The containerised stack, and CI

`docker compose --profile full-stack up` runs the Api with enforcement **on**, trusting a symmetric
key from `.env` instead of an Authority. One command produces the key and a matching token:

```bash
node tools/mint-dev-token.mjs                      # a fresh key, and a token for it
node tools/mint-dev-token.mjs --key <base64>       # another token for a key you already have
```

Put its `DEV_JWT_*` lines in `.env` (the compose file passes them to the Api) and its
`VITE_DEV_ACCESS_TOKEN` line in `frontend/.env.local` (Vite bakes it into the build). The end-to-end
CI job does exactly this, which is why that suite runs against a stack that really enforces
authentication rather than one with it switched off.

A build with a real `VITE_OIDC_AUTHORITY` ignores `VITE_DEV_ACCESS_TOKEN` outright, so a stray
environment variable cannot make a production bundle trust a development token.

---

## What is deliberately not here

- **Registration, password reset, MFA, bot signup.** The provider's, not ours (D3).
- **A token endpoint in the Api.** D4. Everything above mints tokens outside the deployed binary.
- **`/api/v1/me` and account deletion.** Item 21.
- **Rate limiting per user.** Item 22 — an account is what makes per-user limits possible at all.
