'use client';

import { useState } from 'react';

export function ToolFrame({
  src,
  title,
  description,
}: {
  src: string;
  title: string;
  description: string;
}) {
  const [loaded, setLoaded] = useState(false);

  return (
    <section className="tool-workspace">
      <div className="container tool-heading">
        <div>
          <span className="eyebrow">
            <span className="eyebrow-dot" />
            ATLAS LABS · PROJETO EM ANDAMENTO
          </span>
          <h1>{title}</h1>
          <p>{description}</p>
        </div>
        <a className="button button-outline" href={src} target="_blank" rel="noreferrer">
          Abrir em tela cheia ↗
        </a>
      </div>
      <div className="tool-frame-wrap">
        {!loaded && (
          <div className="tool-loading">
            <span className="pulse" />
            Carregando ferramenta…
          </div>
        )}
        <iframe
          className={loaded ? 'tool-frame loaded' : 'tool-frame'}
          src={src}
          title={title}
          onLoad={() => setLoaded(true)}
          allow="clipboard-write"
        />
      </div>
    </section>
  );
}
