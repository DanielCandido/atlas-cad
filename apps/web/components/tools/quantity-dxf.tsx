'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import { getDxfEngine } from '@/lib/tools/quantitativo-engine';

type Tab = 'resumo' | 'divergencias' | 'elementos' | 'premissas';
type RecordItem = any;
type Premises = Record<string, number>;

const number = (value: unknown, digits = 2) =>
  typeof value === 'number' && Number.isFinite(value)
    ? value.toLocaleString('pt-BR', {
        minimumFractionDigits: digits,
        maximumFractionDigits: digits,
      })
    : '—';

export function QuantityDxf() {
  const [records, setRecords] = useState<RecordItem[]>([]);
  const [premises, setPremises] = useState<Premises>({});
  const [tab, setTab] = useState<Tab>('resumo');
  const [activeFile, setActiveFile] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);
  const addInputRef = useRef<HTMLInputElement>(null);

  const analysis = useMemo(() => {
    if (!records.length) return null;
    const engine = getDxfEngine();
    const project = engine.buildProject(records, premises);
    const output = engine.compute(project);
    const alerts = [...project.alerts, ...engine.reviewAlerts(project)];
    return { project, output, alerts };
  }, [records, premises]);

  async function openFiles(fileList: FileList | File[], append = false) {
    setBusy(true);
    setError('');
    try {
      const engine = getDxfEngine();
      const accepted = Array.from(fileList).filter((file) => /\.dxf$/i.test(file.name));
      if (!accepted.length)
        throw new Error(
          'Selecione ao menos um arquivo DXF ASCII. Arquivos DWG devem ser exportados para DXF.',
        );
      const parsed: RecordItem[] = [];
      for (const file of accepted) {
        const model = engine.parseDXF(await file.arrayBuffer(), file.name);
        parsed.push(engine.recognizeFile(model));
      }
      setRecords((current) => (append ? [...current, ...parsed] : parsed));
      if (!append) {
        setPremises({});
        setActiveFile(0);
      }
      setTab('resumo');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Não foi possível ler os arquivos.');
    } finally {
      setBusy(false);
    }
  }

  function exportSpreadsheet() {
    if (!analysis) return;
    const rows: Array<Array<string | number>> = [
      ['Elemento', 'Quantidade', 'Unidade', 'Concreto (m³)', 'Forma (m²)'],
    ];
    for (const item of analysis.output.list)
      rows.push([item.label, item.qtd, item.un, item.conc, item.forma]);
    rows.push(
      [],
      ['Total líquido', '', '', analysis.output.totConc, analysis.output.totForma],
      ['Perda', '', '', analysis.output.perda, ''],
      ['Total com perda', '', '', analysis.output.totComPerda, analysis.output.totForma],
    );
    const csv =
      '\ufeff' +
      rows
        .map((row) => row.map((cell) => `"${String(cell ?? '').replaceAll('"', '""')}"`).join(';'))
        .join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `quantitativo-${records[0]?.file?.replace(/\.dxf$/i, '') || 'atlas'}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  return (
    <section className="engineering-page">
      <div className="container engineering-heading">
        <div>
          <span className="eyebrow">
            <span className="eyebrow-dot" />
            ATLAS LABS · PROJETO EM ANDAMENTO
          </span>
          <h1>Quantitativo DXF</h1>
          <p>
            Leitura de pranchas estruturais, conferência visual e quantitativos calculados no
            navegador.
          </p>
        </div>
        <span className="status status-active">Processamento local</span>
      </div>
      <div className="container engineering-workspace quantity-workspace">
        <div className="atlas-notice warning">
          A extração é automática e deve ser conferida por profissional habilitado. Os arquivos
          permanecem no navegador e não são enviados ao servidor.
        </div>
        <div className="quantity-toolbar">
          <input
            ref={inputRef}
            hidden
            type="file"
            accept=".dxf"
            multiple
            onChange={(event) => {
              if (event.target.files) void openFiles(event.target.files);
              event.target.value = '';
            }}
          />
          <button
            className="btn primary"
            type="button"
            disabled={busy}
            onClick={() => inputRef.current?.click()}
          >
            {busy ? 'Lendo arquivos…' : records.length ? 'Abrir outro projeto' : 'Abrir DXF'}
          </button>
          {records.length > 0 && (
            <>
              <button
                className="btn"
                type="button"
                disabled={busy}
                onClick={() => addInputRef.current?.click()}
              >
                Adicionar prancha
              </button>
              <button className="btn" type="button" onClick={exportSpreadsheet}>
                Exportar para Excel (.csv)
              </button>
              <button
                className="atlas-link-button danger"
                type="button"
                onClick={() => {
                  setRecords([]);
                  setPremises({});
                  setError('');
                }}
              >
                Limpar projeto
              </button>
            </>
          )}
        </div>
        {/* O segundo seletor permite distinguir substituição e acréscimo sem listeners globais. */}
        <input
          ref={addInputRef}
          hidden
          type="file"
          accept=".dxf"
          multiple
          onChange={(event) => {
            if (event.target.files) void openFiles(event.target.files, true);
            event.target.value = '';
          }}
        />
        {error && (
          <div className="atlas-notice warning" role="alert">
            {error}
          </div>
        )}
        {!analysis ? (
          <EmptyDrop onFiles={(files) => void openFiles(files)} busy={busy} />
        ) : (
          <>
            <div className="quantity-file-strip">
              <span>
                {records.length}{' '}
                {records.length === 1 ? 'prancha carregada' : 'pranchas carregadas'}
              </span>
              <strong>{records.map((record) => record.file).join(' · ')}</strong>
            </div>
            <nav className="atlas-tool-tabs" aria-label="Resultados do quantitativo">
              {(
                [
                  ['resumo', 'Resumo'],
                  [
                    'divergencias',
                    `Divergências (${analysis.alerts.filter((item: any) => item.level !== 'info').length})`,
                  ],
                  ['elementos', 'Elementos'],
                  ['premissas', 'Premissas'],
                ] as Array<[Tab, string]>
              ).map(([key, label]) => (
                <button
                  key={key}
                  type="button"
                  aria-current={tab === key ? 'page' : undefined}
                  onClick={() => setTab(key)}
                >
                  {label}
                </button>
              ))}
            </nav>
            <div className="quantity-layout">
              <div className="quantity-results">
                {tab === 'resumo' && <Summary analysis={analysis} />}
                {tab === 'divergencias' && <Alerts alerts={analysis.alerts} />}
                {tab === 'elementos' && (
                  <Elements project={analysis.project} output={analysis.output} />
                )}
                {tab === 'premissas' && (
                  <PremisesEditor
                    project={analysis.project}
                    onChange={(next) => setPremises(next)}
                  />
                )}
              </div>
              <CadPreview
                record={records[activeFile]}
                files={records}
                active={activeFile}
                onActive={setActiveFile}
              />
            </div>
          </>
        )}
      </div>
    </section>
  );
}

function EmptyDrop({ onFiles, busy }: { onFiles: (files: FileList) => void; busy: boolean }) {
  return (
    <label
      className="quantity-drop"
      onDragOver={(event) => event.preventDefault()}
      onDrop={(event) => {
        event.preventDefault();
        if (event.dataTransfer.files.length) onFiles(event.dataTransfer.files);
      }}
    >
      <input
        type="file"
        accept=".dxf"
        multiple
        disabled={busy}
        onChange={(event) => event.target.files && onFiles(event.target.files)}
      />
      <span className="quantity-drop-icon">DXF</span>
      <strong>Arraste as pranchas ou selecione os arquivos</strong>
      <small>Somente DXF ASCII. Nenhum projeto de exemplo é carregado.</small>
    </label>
  );
}
function Summary({ analysis }: { analysis: any }) {
  const { output, project } = analysis;
  const c = output.comp;
  return (
    <>
      <div className="atlas-results">
        <div className="atlas-result">
          <span>Concreto com perda</span>
          <strong>{number(output.totComPerda, 3)} m³</strong>
          <small>{number(output.totConc, 3)} m³ líquido</small>
        </div>
        <div className="atlas-result">
          <span>Forma</span>
          <strong>{number(output.totForma, 2)} m²</strong>
          <small>{output.list.length} grupos</small>
        </div>
        <div className="atlas-result">
          <span>Perda adotada</span>
          <strong>{number(project.prem.perda * 100, 1)}%</strong>
        </div>
      </div>
      <DataTable
        headers={['Elemento', 'Quantidade', 'Un.', 'Concreto', 'Forma']}
        rows={output.list.map((item: any) => [
          item.label,
          number(item.qtd, item.un === 'un' ? 0 : 2),
          item.un,
          `${number(item.conc, 3)} m³`,
          `${number(item.forma, 2)} m²`,
        ])}
      />
      <section className="atlas-tool-panel compact">
        <h2>Complementares</h2>
        <DataTable
          headers={['Item', 'Quantidade']}
          rows={[
            ['Concreto magro', `${number(c.magro, 3)} m³`],
            ['Escavação', `${number(c.esc, 3)} m³`],
            ['Reaterro', `${number(c.reat, 3)} m³`],
            ['Sub-base', `${number(c.subBase, 3)} m³`],
            ['Tela', `${number(c.tela, 1)} kg`],
            ['Impermeabilização da laje', `${number(c.impLaje, 2)} m²`],
          ].filter((row) => !row[1].startsWith('—'))}
        />
      </section>
    </>
  );
}
function Alerts({ alerts }: { alerts: any[] }) {
  return (
    <section className="atlas-tool-panel">
      <h2>Divergências encontradas</h2>
      {alerts.length ? (
        <div className="atlas-alert-list">
          {alerts.map((alert, index) => (
            <div className={`atlas-alert ${alert.level || 'info'}`} key={`${alert.msg}-${index}`}>
              <span>
                {alert.level === 'crit' ? 'Erro' : alert.level === 'warn' ? 'Conferir' : 'Nota'}
              </span>
              <p>
                {alert.msg}
                {alert.file && <small>{alert.file}</small>}
              </p>
            </div>
          ))}
        </div>
      ) : (
        <div className="atlas-notice ok">Nenhuma divergência automática foi encontrada.</div>
      )}
    </section>
  );
}
function Elements({ project, output }: { project: any; output: any }) {
  return (
    <div className="atlas-stack">
      <ElementSection
        title="Pilares"
        headers={['Nome', 'Seção', 'Altura', 'Concreto', 'Forma']}
        rows={project.pilares.map((item: any) => [
          item.name,
          `${number(item.a * 100, 0)} × ${number(item.b * 100, 0)} cm`,
          `${number(item.H, 2)} m`,
          `${number(item.vol, 3)} m³`,
          `${number(item.forma, 2)} m²`,
        ])}
      />
      <ElementSection
        title="Vigas"
        headers={['Nome', 'Grupo', 'Seção', 'Comprimento', 'Concreto']}
        rows={project.vigas.map((item: any) => [
          item.label,
          item.group,
          `${number(item.b * 100, 0)} × ${number(item.h * 100, 0)} cm`,
          `${number(item.L, 2)} m`,
          `${number(item.vol, 3)} m³`,
        ])}
      />
      <ElementSection
        title="Sapatas"
        headers={['Nome', 'Pilar', 'Dimensões', 'Altura', 'Concreto']}
        rows={project.sapatas.map((item: any) => [
          item.name,
          item.pilar,
          `${number(item.A, 2)} × ${number(item.B, 2)} m`,
          `${number(item.h, 2)} m`,
          `${number(item.vol, 3)} m³`,
        ])}
      />
      <div className="atlas-notice info">
        Total reconhecido: {output.list.length} grupos de elementos. Valores ausentes no desenho
        aparecem como “—” e devem ser preenchidos ou conferidos na origem.
      </div>
    </div>
  );
}
function ElementSection({
  title,
  headers,
  rows,
}: {
  title: string;
  headers: string[];
  rows: string[][];
}) {
  return (
    <section className="atlas-tool-panel compact">
      <h2>{title}</h2>
      {rows.length ? (
        <DataTable headers={headers} rows={rows} />
      ) : (
        <p className="atlas-empty">Nenhum elemento reconhecido.</p>
      )}
    </section>
  );
}

function PremisesEditor({
  project,
  onChange,
}: {
  project: any;
  onChange: (values: Premises) => void;
}) {
  const fields: Array<[string, string, number, string]> = [
    ['perda', 'Perda de concreto', 100, '%'],
    ['fColar', 'Colarinho da sapata', 100, 'cm'],
    ['eMagro', 'Espessura do magro', 100, 'cm'],
    ['fMagro', 'Sobra do magro por lado', 100, 'cm'],
    ['fEsc', 'Folga da escavação', 100, 'cm'],
    ['subBase', 'Sub-base do piso', 100, 'cm'],
    ['perdaTela', 'Perda de tela', 100, '%'],
    ['hBaldrameRef', 'Altura de referência da baldrame', 100, 'cm'],
  ];
  return (
    <section className="atlas-tool-panel">
      <h2>Premissas de cálculo</h2>
      <div className="atlas-field-grid">
        {fields.map(([key, label, factor, unit]) => (
          <label className="atlas-field" key={key}>
            <span>{label}</span>
            <div>
              <input
                type="number"
                value={Math.round(project.prem[key] * factor * 100) / 100}
                onChange={(event) =>
                  onChange({ ...project.prem, [key]: Number(event.target.value) / factor })
                }
              />
              <small>{unit}</small>
            </div>
          </label>
        ))}
      </div>
      <div className="atlas-notice info">
        As alterações recalculam o projeto imediatamente e não modificam os arquivos DXF.
      </div>
    </section>
  );
}
function DataTable({ headers, rows }: { headers: string[]; rows: Array<Array<string | number>> }) {
  return (
    <div className="atlas-table-wrap">
      <table className="atlas-table">
        <thead>
          <tr>
            {headers.map((header) => (
              <th key={header}>{header}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, rowIndex) => (
            <tr key={rowIndex}>
              {row.map((cell, index) => (
                <td key={index}>{cell}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function CadPreview({
  record,
  files,
  active,
  onActive,
}: {
  record: any;
  files: any[];
  active: number;
  onActive: (index: number) => void;
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || !record?.model) return;
    const context = canvas.getContext('2d');
    if (!context) return;
    const entities = record.model.ents || [];
    const points: Array<[number, number]> = [];
    for (const e of entities) {
      if (e.type === 'LINE') points.push([e.x1, e.y1], [e.x2, e.y2]);
      if ((e.type === 'LWPOLYLINE' || e.type === 'POLYLINE') && e.pts) points.push(...e.pts);
      if (e.type === 'CIRCLE' || e.type === 'ARC')
        points.push([e.x - e.r, e.y - e.r], [e.x + e.r, e.y + e.r]);
    }
    const draw = () => {
      const rect = canvas.getBoundingClientRect();
      const dpr = window.devicePixelRatio || 1;
      canvas.width = Math.max(1, rect.width * dpr);
      canvas.height = Math.max(1, rect.height * dpr);
      context.setTransform(dpr, 0, 0, dpr, 0, 0);
      context.fillStyle = '#101b2b';
      context.fillRect(0, 0, rect.width, rect.height);
      if (!points.length) return;
      const xs = points.map((p) => p[0]),
        ys = points.map((p) => p[1]);
      const minX = Math.min(...xs),
        maxX = Math.max(...xs),
        minY = Math.min(...ys),
        maxY = Math.max(...ys);
      const scale = Math.min(
        (rect.width - 40) / Math.max(maxX - minX, 1),
        (rect.height - 40) / Math.max(maxY - minY, 1),
      );
      const X = (x: number) => 20 + (x - minX) * scale;
      const Y = (y: number) => rect.height - 20 - (y - minY) * scale;
      context.strokeStyle = 'rgba(187, 217, 241, .72)';
      context.lineWidth = 1;
      for (const e of entities) {
        context.beginPath();
        if (e.type === 'LINE') {
          context.moveTo(X(e.x1), Y(e.y1));
          context.lineTo(X(e.x2), Y(e.y2));
        } else if ((e.type === 'LWPOLYLINE' || e.type === 'POLYLINE') && e.pts?.length) {
          e.pts.forEach((p: [number, number], index: number) =>
            index ? context.lineTo(X(p[0]), Y(p[1])) : context.moveTo(X(p[0]), Y(p[1])),
          );
          if (e.closed) context.closePath();
        } else if (e.type === 'CIRCLE') context.arc(X(e.x), Y(e.y), e.r * scale, 0, Math.PI * 2);
        else continue;
        context.stroke();
      }
    };
    draw();
    const observer = new ResizeObserver(draw);
    observer.observe(canvas);
    return () => observer.disconnect();
  }, [record]);
  return (
    <aside className="quantity-cad">
      <div className="quantity-cadbar">
        <label>
          <span>Prancha</span>
          <select value={active} onChange={(event) => onActive(Number(event.target.value))}>
            {files.map((file, index) => (
              <option value={index} key={`${file.file}-${index}`}>
                {file.file}
              </option>
            ))}
          </select>
        </label>
      </div>
      <canvas ref={canvasRef} aria-label={`Visualização da prancha ${record?.file || ''}`} />
    </aside>
  );
}
