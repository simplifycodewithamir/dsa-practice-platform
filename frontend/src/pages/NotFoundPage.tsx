import { Link } from 'react-router';

export default function NotFoundPage() {
  return (
    <div className="text-center">
      <h1 className="text-2xl font-semibold">Page not found</h1>
      <p className="mt-2 text-slate-600">That page does not exist.</p>
      <Link to="/" className="mt-4 inline-block text-sky-700 underline">
        Back to the questions
      </Link>
    </div>
  );
}
