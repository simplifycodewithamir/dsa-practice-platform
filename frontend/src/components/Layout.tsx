import { Link, Outlet } from 'react-router';
import SignInControl from './SignInControl';
import { useSession } from '../auth/session';

export default function Layout() {
  const session = useSession();

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <header className="border-b border-slate-200 bg-white">
        <nav className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <Link
            to="/"
            className="text-lg font-semibold tracking-tight focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-900"
          >
            DSA Practice
          </Link>
          <div className="flex items-center gap-4">
            <span className="hidden text-sm text-slate-500 sm:inline">Free practice for students</span>
            {/* Only for someone who has an account: a link to an empty account page is noise. */}
            {session.isAuthenticated && (
              <Link
                to="/account"
                className="text-sm font-medium text-slate-700 underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
              >
                My account
              </Link>
            )}
            <SignInControl />
          </div>
        </nav>
      </header>

      <main className="mx-auto max-w-5xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  );
}
