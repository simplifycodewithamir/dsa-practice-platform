import { useLocation } from 'react-router';
import { useSession } from '../auth/session';

/**
 * Who you are, in the header. Renders nothing at all when no identity provider is configured:
 * a "Sign in" button that cannot sign anyone in is worse than no button.
 */
export default function SignInControl() {
  const session = useSession();
  const location = useLocation();

  if (session.mode === 'disabled') {
    return null;
  }

  if (session.isLoading) {
    return <span className="text-sm text-slate-500">Signing in…</span>;
  }

  if (!session.isAuthenticated) {
    return (
      <button
        type="button"
        // Come back to the question they were reading, not to the home page.
        onClick={() => session.signIn(location.pathname + location.search)}
        className="rounded-md bg-slate-900 px-3 py-1.5 text-sm font-medium text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
      >
        Sign in
      </button>
    );
  }

  return (
    <div className="flex items-center gap-3 text-sm">
      <span className="text-slate-600">{session.displayName ?? 'Signed in'}</span>
      <button
        type="button"
        onClick={session.signOut}
        className="rounded-md border border-slate-300 px-3 py-1.5 font-medium text-slate-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
      >
        Sign out
      </button>
    </div>
  );
}
