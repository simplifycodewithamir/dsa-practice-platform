import { InMemoryWebStorage, WebStorageStateStore } from 'oidc-client-ts';
import type { AuthProviderProps } from 'react-oidc-context';

/**
 * Where the app gets tokens from, decided at build time.
 *
 * Two modes, and which one is in force is decided here rather than scattered through components:
 *
 * - **oidc** — an identity provider is configured, so the app signs people in with Authorization
 *   Code + PKCE and sends the access token it gets back.
 * - **disabled** — nothing is configured. The app runs signed-out, which is what a checkout with
 *   no tenant of its own does. `VITE_DEV_ACCESS_TOKEN` may supply a token for the end-to-end
 *   stack, where the Api trusts a development signing key instead of a real issuer.
 *
 * A configured authority always wins: the development token is ignored outright, so a real build
 * cannot be made to trust one by setting a stray environment variable.
 */
const authority = import.meta.env.VITE_OIDC_AUTHORITY?.trim();
const clientId = import.meta.env.VITE_OIDC_CLIENT_ID?.trim();
const audience = import.meta.env.VITE_OIDC_AUDIENCE?.trim();

export const isOidcConfigured = Boolean(authority && clientId);

export const devAccessToken = isOidcConfigured ? undefined : import.meta.env.VITE_DEV_ACCESS_TOKEN?.trim() || undefined;

export const oidcConfig: AuthProviderProps | undefined = isOidcConfigured
  ? {
      authority: authority!,
      client_id: clientId!,
      // A route of its own, so the callback does not flash the question list while the code is
      // still being exchanged. It must also be registered with the provider verbatim.
      redirect_uri: `${window.location.origin}/auth/callback`,
      post_logout_redirect_uri: window.location.origin,
      // Authorization Code + PKCE. There is no client secret, because a public client cannot keep
      // one; the proof key is what stops a stolen code being redeemed by anyone else.
      response_type: 'code',
      scope: import.meta.env.VITE_OIDC_SCOPE?.trim() || 'openid profile email offline_access',
      // Auth0 issues an opaque token unless the request names the API it is for. It is also what
      // makes the token unusable against any other API on the same tenant.
      extraQueryParams: audience ? { audience } : undefined,
      // Tokens live in memory only: nothing readable is left behind on the device, and a stored
      // token cannot outlive the tab it was issued to. The cost is that a hard refresh re-runs the
      // redirect -- silent, because the session cookie at the provider is first-party there.
      userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
      // The PKCE verifier is the one thing that *must* survive the redirect, so it goes to
      // sessionStorage: same tab, cleared when the tab closes, useless once the code is redeemed.
      stateStore: new WebStorageStateStore({ store: window.sessionStorage }),
      // A judging session is long enough to outlive a short access token; renewing in the
      // background beats a submit that fails with a 401 after half an hour of work.
      automaticSilentRenew: true,
    }
  : undefined;
