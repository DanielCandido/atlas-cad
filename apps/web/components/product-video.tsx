'use client';

import { useEffect, useRef, useState } from 'react';

export function ProductVideo() {
  const videoRef = useRef<HTMLVideoElement>(null);
  const frameRef = useRef<HTMLElement>(null);
  const [paused, setPaused] = useState(false);
  const [visible, setVisible] = useState(true);

  useEffect(() => {
    const video = videoRef.current;
    const frame = frameRef.current;
    if (!video || !frame) return;

    const motion = window.matchMedia('(prefers-reduced-motion: reduce)');
    const sync = () => {
      if (paused || !visible || motion.matches) video.pause();
      else void video.play().catch(() => undefined);
    };

    const observer = new IntersectionObserver(([entry]) => setVisible(entry.isIntersecting), {
      threshold: 0.2,
    });
    observer.observe(frame);
    motion.addEventListener('change', sync);
    sync();

    return () => {
      observer.disconnect();
      motion.removeEventListener('change', sync);
    };
  }, [paused, visible]);

  function pauseTemporarily() {
    videoRef.current?.pause();
  }

  function resumeAfterInteraction() {
    if (!paused && visible && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      void videoRef.current?.play().catch(() => undefined);
    }
  }

  function togglePlayback() {
    const nextPaused = !paused;
    setPaused(nextPaused);
    if (nextPaused) videoRef.current?.pause();
    else void videoRef.current?.play().catch(() => undefined);
  }

  return (
    <figure
      ref={frameRef}
      className="hm-video"
      onPointerEnter={pauseTemporarily}
      onPointerLeave={resumeAfterInteraction}
      onFocusCapture={pauseTemporarily}
      onBlurCapture={resumeAfterInteraction}
      data-slot="steelflow-demo"
      data-kind="product"
      data-alt="SteelFlow executando uma automação dentro do AutoCAD"
    >
      <video
        ref={videoRef}
        autoPlay
        muted
        loop
        playsInline
        preload="auto"
        poster="/videos/steel-flow-poster.jpg"
        aria-label="Demonstração do SteelFlow no AutoCAD"
      >
        <source src="/videos/steel-flow.mp4" type="video/mp4" />
        Seu navegador não oferece suporte a vídeo HTML.
      </video>
      <button
        className="hm-video__control"
        type="button"
        onClick={togglePlayback}
        aria-pressed={paused}
      >
        {paused ? 'Reproduzir vídeo' : 'Pausar vídeo'}
      </button>
      <figcaption>SteelFlow em execução no AutoCAD.</figcaption>
    </figure>
  );
}
