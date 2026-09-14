import { Route, Routes } from 'react-router';
import Layout from './components/Layout';
import QuestionsPage from './pages/QuestionsPage';
import QuestionPage from './pages/QuestionPage';
import NotFoundPage from './pages/NotFoundPage';

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<QuestionsPage />} />
        {/* The slug is the public URL key, chosen in item 5 so these pages are indexable. */}
        <Route path="/problems/:slug" element={<QuestionPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
