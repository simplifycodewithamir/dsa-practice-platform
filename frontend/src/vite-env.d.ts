/// <reference types="vite/client" />

/**
 * Build-time configuration. All of it is baked into the bundle and readable by anyone who opens
 * the page, which is fine for every value here: an OIDC public client has no secret to keep.
 */
interface ImportMetaEnv {
  /** Absolute Api URL in production; unset in dev, where Vite proxies /api. */
  readonly VITE_API_BASE_URL?: string;
  /** The identity provider's issuer URL. Unset means the app runs signed-out. */
  readonly VITE_OIDC_AUTHORITY?: string;
  readonly VITE_OIDC_CLIENT_ID?: string;
  /** The Api's identifier at the provider; without it Auth0 issues a token for its own userinfo. */
  readonly VITE_OIDC_AUDIENCE?: string;
  readonly VITE_OIDC_SCOPE?: string;
  /**
   * A token for the end-to-end stack, where the Api trusts a development signing key rather than a
   * real issuer. Ignored outright when an authority is configured.
   */
  readonly VITE_DEV_ACCESS_TOKEN?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
