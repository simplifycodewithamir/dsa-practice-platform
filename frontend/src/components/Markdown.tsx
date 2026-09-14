import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

/**
 * Renders an authored question statement.
 *
 * react-markdown does not evaluate HTML in the source (no `rehype-raw` here, deliberately), so a
 * statement cannot inject markup even though statements are authored as files in the repository.
 */
export default function Markdown({ children }: { children: string }) {
  return (
    <div className="space-y-4 leading-relaxed text-slate-800">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          h2: ({ children }) => <h2 className="mt-6 text-lg font-semibold text-slate-900">{children}</h2>,
          h3: ({ children }) => <h3 className="mt-4 font-semibold text-slate-900">{children}</h3>,
          p: ({ children }) => <p>{children}</p>,
          ul: ({ children }) => <ul className="list-disc space-y-1 pl-6">{children}</ul>,
          ol: ({ children }) => <ol className="list-decimal space-y-1 pl-6">{children}</ol>,
          code: ({ children, className }) =>
            className?.includes('language-') ? (
              <code className="block overflow-x-auto rounded-md bg-slate-900 p-3 text-sm text-slate-100">{children}</code>
            ) : (
              <code className="rounded bg-slate-200 px-1 py-0.5 text-sm">{children}</code>
            ),
          pre: ({ children }) => <pre className="overflow-x-auto">{children}</pre>,
          a: ({ children, href }) => (
            <a href={href} className="text-sky-700 underline" rel="noreferrer noopener">
              {children}
            </a>
          ),
        }}
      >
        {children}
      </ReactMarkdown>
    </div>
  );
}
