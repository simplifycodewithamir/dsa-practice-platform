import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router';
import App from './App';
import AppAuthProvider from './auth/AppAuthProvider';
import './index.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Questions change when content is redeployed, not while someone is reading them.
      staleTime: 5 * 60 * 1000,
      retry: 1,
    },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        {/* Inside the router: signing in needs to know which page to come back to. */}
        <AppAuthProvider>
          <App />
        </AppAuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
