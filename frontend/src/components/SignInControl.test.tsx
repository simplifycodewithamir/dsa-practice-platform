import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import SignInControl from './SignInControl';
import { noAuthSession, renderPage, signedInSession, signedOutSession } from '../test/render';

describe('SignInControl', () => {
  it('renders nothing when no identity provider is configured', () => {
    // A checkout with no tenant of its own: a button that cannot sign anyone in is worse than none.
    const { container } = renderPage(<SignInControl />, { session: noAuthSession });

    expect(container).toBeEmptyDOMElement();
  });

  it('offers to sign in, and comes back to the page you were reading', async () => {
    const session = signedOutSession();
    renderPage(<SignInControl />, { session, path: '/problems/two-sum', route: '/problems/:slug' });

    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(session.signIn).toHaveBeenCalledWith('/problems/two-sum');
  });

  it('shows who is signed in, and lets them out again', async () => {
    const session = signedInSession();
    renderPage(<SignInControl />, { session });

    expect(screen.getByText('Ada Lovelace')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    expect(session.signOut).toHaveBeenCalled();
  });

  it('says nothing about being signed out while a redirect is still being processed', () => {
    // Otherwise the header flashes "Sign in" at someone who is in the middle of signing in.
    renderPage(<SignInControl />, { session: signedOutSession({ isLoading: true }) });

    expect(screen.queryByRole('button', { name: 'Sign in' })).not.toBeInTheDocument();
    expect(screen.getByText('Signing in…')).toBeInTheDocument();
  });
});
