import { useEffect, useMemo, type ReactNode } from 'react';
import { AuthProvider, useAuth } from 'react-oidc-context';
import { setAccessTokenProvider } from '../api/client';
import { devAccessToken, oidcConfig } from './config';
import { SessionContext, type Session } from './session';

/**
 * Puts a `Session` in context: from an identity provider when one is configured, and from nothing
 * when one is not.
 *
 * The branch is on a module constant decided at build time, so it is taken once and never flips
 * between renders -- which is what makes a conditional provider safe here.
 */
export default function AppAuthProvider({ children }: { children: ReactNode }) {
  if (oidcConfig === undefined) {
    return <SignedOutSession>{children}</SignedOutSession>;
  }

  return (
    <AuthProvider {...oidcConfig}>
      <OidcSession>{children}</OidcSession>
    </AuthProvider>
  );
}

/** Hands the current access token to the fetch layer, which is not a component and cannot use a hook. */
function usePublishAccessToken(accessToken: string | undefined) {
  useEffect(() => {
    setAccessTokenProvider(() => accessToken);
    return () => setAccessTokenProvider(undefined);
  }, [accessToken]);
}

function OidcSession({ children }: { children: ReactNode }) {
  const auth = useAuth();
  usePublishAccessToken(auth.user?.access_token);

  const session = useMemo<Session>(
    () => ({
      mode: 'oidc',
      isAuthenticated: auth.isAuthenticated,
      isLoading: auth.isLoading,
      displayName: auth.user?.profile.name ?? auth.user?.profile.email,
      error: auth.error,
      // Where to come back to, carried through the provider in the `state` parameter rather than
      // in storage, so it survives the round trip and is tied to this one request.
      signIn: (returnTo) => void auth.signinRedirect({ state: { returnTo } }),
      signOut: () => {
        // Ends the session at the provider as well, not just here -- otherwise "sign out" followed
        // by "sign in" silently signs you back in as the same person, on a shared machine too.
        // Providers that expose no end-session endpoint make that throw, and dropping the local
        // user is then the most that can be done.
        void auth.signoutRedirect().catch(() => auth.removeUser());
      },
    }),
    [auth],
  );

  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}

function SignedOutSession({ children }: { children: ReactNode }) {
  usePublishAccessToken(devAccessToken);

  const session = useMemo<Session>(
    () => ({
      mode: 'disabled',
      isAuthenticated: false,
      isLoading: false,
      signIn: () => {},
      signOut: () => {},
    }),
    [],
  );

  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}
