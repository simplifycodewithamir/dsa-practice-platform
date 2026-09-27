import { Route, Routes } from 'react-router';
import Layout from './components/Layout';
import QuestionsPage from './pages/QuestionsPage';
import QuestionPage from './pages/QuestionPage';
import NotFoundPage from './pages/NotFoundPage';
import AuthCallbackPage from './pages/AuthCallbackPage';
import { isOidcConfigured } from './auth/config';

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<QuestionsPage />} />
        {/* The slug is the public URL key, chosen in item 5 so these pages are indexable. */}
        <Route path="/problems/:slug" element={<QuestionPage />} />
        {/* Only a route when there is a provider to come back from; otherwise it is a 404 like any
            other unknown path, rather than a page that waits forever for a code that never arrives. */}
        {isOidcConfigured && <Route path="/auth/callback" element={<AuthCallbackPage />} />}
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
