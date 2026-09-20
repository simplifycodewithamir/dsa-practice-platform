import { useEffect } from 'react';
import { useNavigate } from 'react-router';
import { useAuth } from 'react-oidc-context';
import { Link } from 'react-router';

/**
 * Where the identity provider sends the browser back with an authorization code.
 *
 * The library exchanges the code for tokens on its own as soon as it sees the parameters in the
 * URL; this page exists so that exchange happens somewhere that says what is going on, and so the
 * code and state never sit in the address bar of a real page. Nothing here reads them.
 */
export default function AuthCallbackPage() {
  const auth = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!auth.isAuthenticated) {
      return;
    }

    // Where they were when they clicked sign in, carried through the provider in `state`.
    const returnTo = (auth.user?.state as { returnTo?: string } | undefined)?.returnTo;
    // replace: the callback URL is a step in a flow, not somewhere the back button should return to.
    void navigate(returnTo ?? '/', { replace: true });
  }, [auth.isAuthenticated, auth.user, navigate]);

  if (auth.error) {
    return (
      <div role="alert">
        <h1 className="text-2xl font-semibold">Could not sign you in</h1>
        <p className="mt-2 text-slate-600">{auth.error.message}</p>
        <Link to="/" className="mt-4 inline-block text-sky-700 underline">
          Back to the questions
        </Link>
      </div>
    );
  }

  return <p className="text-slate-600">Signing you in…</p>;
}
