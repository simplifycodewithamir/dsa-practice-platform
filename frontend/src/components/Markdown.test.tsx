import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import Markdown from './Markdown';

describe('Markdown', () => {
  it('renders a statement as structured markup, not as literal markdown', () => {
    render(<Markdown>{'## Constraints\n\nAt most **10** numbers.'}</Markdown>);

    expect(screen.getByRole('heading', { level: 2, name: 'Constraints' })).toBeInTheDocument();
    expect(screen.getByText('10').tagName).toBe('STRONG');
  });

  it('renders lists and tables, which statements are authored with', () => {
    render(<Markdown>{'- one\n- two\n\n| a | b |\n| - | - |\n| 1 | 2 |'}</Markdown>);

    expect(screen.getAllByRole('listitem')).toHaveLength(2);
    // The GitHub-flavoured table syntax only works because remark-gfm is wired in.
    expect(screen.getByRole('table')).toBeInTheDocument();
  });

  it('does not evaluate HTML embedded in a statement', () => {
    // Statements are authored files rather than user input, but rehype-raw is left out
    // deliberately: nothing in a statement may become live markup.
    const { container } = render(
      <Markdown>{'<img src="x" onerror="alert(1)" /> and <script>alert(1)</script>'}</Markdown>,
    );

    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('script')).toBeNull();
  });

  it('renders a link without letting it reach the opener window', () => {
    render(<Markdown>{'[docs](https://example.com)'}</Markdown>);

    const link = screen.getByRole('link', { name: 'docs' });
    expect(link).toHaveAttribute('href', 'https://example.com');
    expect(link).toHaveAttribute('rel', expect.stringContaining('noopener'));
  });

  it('renders an empty statement without crashing', () => {
    const { container } = render(<Markdown>{''}</Markdown>);

    expect(container.textContent).toBe('');
  });
});
