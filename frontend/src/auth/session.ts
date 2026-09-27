import { createContext, useContext } from 'react';

/**
 * What the rest of the app is allowed to know about signing in.
 *
 * Deliberately smaller than the OIDC library's own context: components should not be able to reach
 * for a refresh token or drive a redirect flow by hand, and swapping identity provider -- or
 * running with none at all -- must not touch a component.
 */
export type Session = {
  /** 'disabled' means no identity provider is configured; there is nothing to sign in to. */
  mode: 'oidc' | 'disabled';
  isAuthenticated: boolean;
  /** True while a redirect is being processed, so the header doesn't flicker "Sign in". */
  isLoading: boolean;
  /** Whatever the provider knows us by -- a name, failing that an email, failing that nothing. */
  displayName?: string;
  error?: Error;
  signIn: (returnTo?: string) => void;
  signOut: () => void;
};

export const SessionContext = createContext<Session | undefined>(undefined);

export function useSession(): Session {
  const session = useContext(SessionContext);
  if (session === undefined) {
    throw new Error('useSession must be used inside <AppAuthProvider>.');
  }

  return session;
}
