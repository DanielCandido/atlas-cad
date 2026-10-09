import Link from 'next/link';
export function ArrowLink({
  href,
  children,
  secondary = false,
}: {
  href: string;
  children: React.ReactNode;
  secondary?: boolean;
}) {
  return (
    <Link href={href} className={'button ' + (secondary ? 'button-outline' : 'button-primary')}>
      {children}
      <span aria-hidden>↗</span>
    </Link>
  );
}
export function Eyebrow({ children }: { children: React.ReactNode }) {
  return (
    <span className="eyebrow">
      <span className="eyebrow-dot" />
      {children}
    </span>
  );
}
export function CadVisual() {
  return (
    <div
      className="cad-frame"
      aria-label="Ilustração conceitual de conversão de linhas em MLINE"
      role="img"
    >
      <div className="cad-toolbar">
        <span />
        <span />
        <span />
        <span className="cad-file">ATLAS / MLINE VIEWER</span>
        <span className="cad-live">● LIVE PREVIEW</span>
      </div>
      <div className="cad-grid">
        <svg viewBox="0 0 660 370" className="cad-svg" aria-hidden="true">
          <defs>
            <pattern id="dots" width="22" height="22" patternUnits="userSpaceOnUse">
              <circle cx="1" cy="1" r=".65" fill="#4b6476" opacity=".5" />
            </pattern>
          </defs>
          <rect width="660" height="370" fill="url(#dots)" />
          <g stroke="#476173" strokeWidth="1" opacity=".65">
            <path d="M40 70H620M40 300H620M110 30V340M540 30V340" strokeDasharray="4 8" />
          </g>
          <g transform="translate(66 65)">
            <text x="0" y="0" fill="#8797a6" fontSize="12" letterSpacing="2">
              01 / GEOMETRIA BASE
            </text>
            <path d="M35 65 L215 185" stroke="#cbd5df" strokeWidth="2.5" />
            <circle cx="35" cy="65" r="5" fill="#0c131b" stroke="#9cb7cb" strokeWidth="2" />
            <circle cx="215" cy="185" r="5" fill="#0c131b" stroke="#9cb7cb" strokeWidth="2" />
            <text x="60" y="90" fill="#a6b6c5" fontSize="13">
              LINE
            </text>
          </g>
          <g transform="translate(330 65)">
            <text x="0" y="0" fill="#8797a6" fontSize="12" letterSpacing="2">
              02 / MLINE GERADA
            </text>
            <path d="M24 74 L214 202 M42 48 L232 176" stroke="#59d6b3" strokeWidth="2.5" />
            <path d="M33 61L223 189" stroke="#4e947f" strokeWidth="1" strokeDasharray="6 6" />
            <path d="M24 74L42 48M214 202L232 176" stroke="#59d6b3" strokeWidth="2" />
            <text x="140" y="95" fill="#79e6c8" fontSize="13">
              MLINE
            </text>
          </g>
          <rect x="23" y="320" width="220" height="30" rx="5" fill="#162632" stroke="#304552" />
          <text x="38" y="340" fill="#87e3c8" fontSize="12" fontFamily="monospace">
            STATUS: STYLE APPLIED
          </text>
        </svg>
      </div>
      <div className="cad-status">
        <span>
          <i /> MODELO CONCEITUAL
        </span>
        <span>ATLAS CAD / WORKSPACE</span>
      </div>
    </div>
  );
}
