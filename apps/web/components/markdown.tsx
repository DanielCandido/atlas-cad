import Link from 'next/link';

function inline(value: string) {
  const parts = value.split(/(`[^`]+`|\[[^\]]+\]\([^)]+\)|\*\*[^*]+\*\*)/g);
  return parts.map((part, index) => {
    if (part.startsWith('`') && part.endsWith('`'))
      return <code key={index}>{part.slice(1, -1)}</code>;
    if (part.startsWith('**') && part.endsWith('**'))
      return <strong key={index}>{part.slice(2, -2)}</strong>;
    const link = part.match(/^\[([^\]]+)\]\(([^)]+)\)$/);
    if (link) {
      const href = link[2];
      if (!href.startsWith('/') && !href.startsWith('https://')) return link[1];
      return (
        <Link key={index} href={href}>
          {link[1]}
        </Link>
      );
    }
    return part;
  });
}
export function Markdown({ source }: { source: string }) {
  const lines = source.replace(/\r/g, '').split('\n');
  const content: React.ReactNode[] = [];
  let list: string[] = [];
  const flush = () => {
    if (list.length) {
      content.push(
        <ul key={`list-${content.length}`}>
          {list.map((item) => (
            <li key={item}>{inline(item)}</li>
          ))}
        </ul>,
      );
      list = [];
    }
  };
  for (const line of lines) {
    if (line.startsWith('- ')) {
      list.push(line.slice(2));
      continue;
    }
    flush();
    if (!line.trim()) continue;
    if (line.startsWith('### '))
      content.push(<h3 key={content.length}>{inline(line.slice(4))}</h3>);
    else if (line.startsWith('## '))
      content.push(<h2 key={content.length}>{inline(line.slice(3))}</h2>);
    else if (line.startsWith('# '))
      content.push(<h1 key={content.length}>{inline(line.slice(2))}</h1>);
    else content.push(<p key={content.length}>{inline(line)}</p>);
  }
  flush();
  return <>{content}</>;
}
