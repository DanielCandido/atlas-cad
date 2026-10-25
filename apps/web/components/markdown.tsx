import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

export function normalizeMarkdown(source: string) {
  return String(source || '')
    .replace(/\r\n?/g, '\n')
    .replace(/\\n/g, '\n');
}

export function Markdown({ source, className = '' }: { source: string; className?: string }) {
  return (
    <div className={`markdown-content ${className}`.trim()}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          a: ({ href = '', children, ...props }) => {
            const external = /^https?:\/\//i.test(href);
            return (
              <a
                href={href}
                target={external ? '_blank' : undefined}
                rel={external ? 'noreferrer noopener' : undefined}
                {...props}
              >
                {children}
              </a>
            );
          },
          code: ({ className: codeClass, children, ...props }) => {
            const block = Boolean(codeClass);
            return block ? (
              <code className={codeClass} {...props}>
                {children}
              </code>
            ) : (
              <code {...props}>{children}</code>
            );
          },
        }}
      >
        {normalizeMarkdown(source)}
      </ReactMarkdown>
    </div>
  );
}
