import { useParams } from 'react-router';

export default function QuestionPage() {
  const { slug } = useParams();

  // Item 16 builds the real page.
  return <h1 className="text-2xl font-semibold">{slug}</h1>;
}
