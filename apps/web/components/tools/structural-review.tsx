'use client';

import { useEffect, useMemo, useState } from 'react';

type Tab =
  | 'sapata'
  | 'base'
  | 'bloco'
  | 'viga'
  | 'pilar'
  | 'laje'
  | 'aco'
  | 'ancoragem'
  | 'cobrimento'
  | 'quantitativo';
type Values = Record<string, number>;
type QuantityItem = {
  id: string;
  type: 'viga' | 'pilar' | 'laje' | 'sapata' | 'bloco';
  description: string;
  d1: number;
  d2: number;
  d3: number;
  quantity: number;
  steel: number;
};

const bars = [5, 6.3, 8, 10, 12.5, 16, 20, 25, 32];
const barArea = (diameter: number) => (Math.PI * (diameter / 10) ** 2) / 4;
const fmt = (value: number, digits = 2) =>
  Number.isFinite(value)
    ? value.toLocaleString('pt-BR', {
        maximumFractionDigits: digits,
        minimumFractionDigits: digits,
      })
    : '—';

const initial: Values = {
  fck: 25,
  caa: 2,
  aggregate: 19,
  footingLoad: 800,
  soil: 300,
  columnA: 25,
  columnB: 30,
  footingA: 0,
  footingB: 0,
  footingH: 0,
  depth: 150,
  replacement: 50,
  workGap: 30,
  slope: 1,
  swell: 25,
  piles: 2,
  pileLoad: 600,
  pileDiameter: 30,
  pileSpacing: 90,
  blockD: 60,
  beamB: 20,
  beamH: 50,
  beamCover: 5,
  beamMoment: 60,
  beamShear: 80,
  colB: 20,
  colH: 40,
  colLength: 2.8,
  colLoad: 800,
  slabLx: 4,
  slabLy: 5,
  slabH: 12,
  slabFinish: 1,
  slabLive: 2,
  steelArea: 4.2,
  steelBars: 4,
  steelDiameter: 12.5,
  steelLength: 6,
  bond: 1,
  hook: 1,
  lap: 1.5,
};

function Field({
  label,
  unit,
  value,
  onChange,
  step = 'any',
  min = 0,
}: {
  label: string;
  unit?: string;
  value: number;
  onChange: (value: number) => void;
  step?: number | 'any';
  min?: number;
}) {
  return (
    <label className="atlas-field">
      <span>{label}</span>
      <div>
        <input
          type="number"
          value={value}
          min={min}
          step={step}
          onChange={(event) => onChange(Number(event.target.value))}
        />
        {unit && <small>{unit}</small>}
      </div>
    </label>
  );
}

function Results({ items }: { items: Array<[string, string, string?]> }) {
  return (
    <div className="atlas-results">
      {items.map(([label, value, note]) => (
        <div className="atlas-result" key={label}>
          <span>{label}</span>
          <strong>{value}</strong>
          {note && <small>{note}</small>}
        </div>
      ))}
    </div>
  );
}

export function StructuralReview() {
  const [tab, setTab] = useState<Tab>('sapata');
  const [v, setValues] = useState<Values>(initial);
  const [items, setItems] = useState<QuantityItem[]>([]);
  const [draft, setDraft] = useState<QuantityItem>({
    id: '',
    type: 'viga',
    description: '',
    d1: 20,
    d2: 50,
    d3: 5,
    quantity: 1,
    steel: 110,
  });
  const set = (key: string, value: number) =>
    setValues((current) => ({ ...current, [key]: value }));

  useEffect(() => {
    try {
      const saved = localStorage.getItem('atlas-structural-quantities-v1');
      if (saved) setItems(JSON.parse(saved));
    } catch {
      /* armazenamento opcional */
    }
  }, []);
  useEffect(() => {
    try {
      localStorage.setItem('atlas-structural-quantities-v1', JSON.stringify(items));
    } catch {
      /* armazenamento opcional */
    }
  }, [items]);

  const material = useMemo(() => {
    const fcd = v.fck / 1.4 / 10;
    return {
      fcd,
      fyd: 50 / 1.15,
      fctm: 0.3 * v.fck ** (2 / 3),
      cover: [20, 25, 35, 45][v.caa - 1] || 25,
    };
  }, [v.fck, v.caa]);

  const footing = useMemo(() => {
    const required = (1.05 * v.footingLoad) / v.soil;
    const ratio = Math.max(v.columnA / Math.max(v.columnB, 1), 0.4);
    const autoB = Math.sqrt(required / ratio) * 100;
    const B = v.footingB > 0 ? v.footingB : Math.ceil(autoB / 5) * 5;
    const A = v.footingA > 0 ? v.footingA : Math.ceil((B * ratio) / 5) * 5;
    const hMin = Math.max((A - v.columnA) / 3, (B - v.columnB) / 3, 25);
    const h = v.footingH > 0 ? v.footingH : Math.ceil(hMin / 5) * 5;
    const pressure = (1.05 * v.footingLoad) / ((A * B) / 10000);
    return { A, B, h, pressure, required, volume: (A * B * h) / 1e6 };
  }, [v]);

  const quantityTotals = useMemo(
    () =>
      items.reduce(
        (total, item) => {
          let concrete = 0;
          let form = 0;
          if (item.type === 'viga' || item.type === 'pilar') {
            concrete = (((item.d1 / 100) * item.d2) / 100) * item.d3;
            form =
              item.type === 'viga'
                ? ((2 * item.d2 + item.d1) / 100) * item.d3
                : ((2 * (item.d1 + item.d2)) / 100) * item.d3;
          }
          if (item.type === 'laje') {
            concrete = (item.d1 / 100) * item.d2 * item.d3;
            form = item.d2 * item.d3;
          }
          if (item.type === 'sapata' || item.type === 'bloco') {
            concrete = ((((item.d1 / 100) * item.d2) / 100) * item.d3) / 100;
            form = (((2 * (item.d1 + item.d2)) / 100) * item.d3) / 100;
          }
          concrete *= item.quantity;
          form *= item.quantity;
          return {
            concrete: total.concrete + concrete,
            form: total.form + form,
            steel: total.steel + concrete * item.steel,
          };
        },
        { concrete: 0, form: 0, steel: 0 },
      ),
    [items],
  );

  const tabs: Array<[Tab, string]> = [
    ['sapata', 'Sapata'],
    ['base', 'Base'],
    ['bloco', 'Bloco'],
    ['viga', 'Viga'],
    ['pilar', 'Pilar'],
    ['laje', 'Laje'],
    ['aco', 'Aço'],
    ['ancoragem', 'Ancoragem'],
    ['cobrimento', 'Cobrimento'],
    ['quantitativo', 'Quantitativo'],
  ];

  function exportQuantities() {
    const rows = [
      [
        'Elemento',
        'Tipo',
        'Quantidade',
        'Dimensão 1',
        'Dimensão 2',
        'Dimensão 3',
        'Taxa de aço (kg/m³)',
      ],
      ...items.map((item) => [
        item.description,
        item.type,
        item.quantity,
        item.d1,
        item.d2,
        item.d3,
        item.steel,
      ]),
      [],
      ['Totais', '', '', '', '', '', ''],
      ['Concreto (m³)', quantityTotals.concrete],
      ['Forma (m²)', quantityTotals.form],
      ['Aço estimado (kg)', quantityTotals.steel],
    ];
    const csv =
      '\ufeff' +
      rows
        .map((row) => row.map((cell) => `"${String(cell ?? '').replaceAll('"', '""')}"`).join(';'))
        .join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'quantitativo-estrutural-atlas.csv';
    anchor.click();
    URL.revokeObjectURL(url);
  }

  return (
    <ToolShell
      title="Conferência Estrutural"
      description="Pré-dimensionamento e conferência preliminar de elementos de concreto armado."
    >
      <div className="atlas-tool-settings">
        <Field label="Concreto" unit="MPa" value={v.fck} onChange={(n) => set('fck', n)} step={5} />
        <label className="atlas-field">
          <span>Classe de agressividade</span>
          <select value={v.caa} onChange={(event) => set('caa', Number(event.target.value))}>
            <option value={1}>CAA I</option>
            <option value={2}>CAA II</option>
            <option value={3}>CAA III</option>
            <option value={4}>CAA IV</option>
          </select>
        </label>
        <Field
          label="Agregado máximo"
          unit="mm"
          value={v.aggregate}
          onChange={(n) => set('aggregate', n)}
        />
      </div>
      <nav className="atlas-tool-tabs" aria-label="Módulos de cálculo">
        {tabs.map(([key, label]) => (
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

      {tab === 'sapata' && (
        <ToolPanel title="Sapata isolada">
          <div className="atlas-field-grid">
            <Field
              label="Carga característica"
              unit="kN"
              value={v.footingLoad}
              onChange={(n) => set('footingLoad', n)}
            />
            <Field
              label="Tensão admissível"
              unit="kPa"
              value={v.soil}
              onChange={(n) => set('soil', n)}
            />
            <Field
              label="Pilar a"
              unit="cm"
              value={v.columnA}
              onChange={(n) => set('columnA', n)}
            />
            <Field
              label="Pilar b"
              unit="cm"
              value={v.columnB}
              onChange={(n) => set('columnB', n)}
            />
            <Field
              label="Sapata A (0 = automático)"
              unit="cm"
              value={v.footingA}
              onChange={(n) => set('footingA', n)}
            />
            <Field
              label="Sapata B (0 = automático)"
              unit="cm"
              value={v.footingB}
              onChange={(n) => set('footingB', n)}
            />
            <Field
              label="Altura (0 = automático)"
              unit="cm"
              value={v.footingH}
              onChange={(n) => set('footingH', n)}
            />
          </div>
          <Results
            items={[
              [
                'Dimensões',
                `${fmt(footing.A, 0)} × ${fmt(footing.B, 0)} cm`,
                'arredondadas em 5 cm',
              ],
              ['Altura', `${fmt(footing.h, 0)} cm`],
              [
                'Tensão no solo',
                `${fmt(footing.pressure, 0)} kPa`,
                footing.pressure <= v.soil
                  ? 'Dentro do limite informado'
                  : 'Acima do limite informado',
              ],
              ['Concreto', `${fmt(footing.volume, 3)} m³`],
            ]}
          />
          <Notice kind={footing.pressure <= v.soil ? 'ok' : 'warning'}>
            {footing.pressure <= v.soil
              ? 'A área atende à tensão admissível informada.'
              : 'Aumente as dimensões da sapata.'}
          </Notice>
        </ToolPanel>
      )}

      {tab === 'base' && <BasePanel v={v} set={set} footing={footing} />}
      {tab === 'bloco' && <BlockPanel v={v} set={set} material={material} />}
      {tab === 'viga' && <BeamPanel v={v} set={set} material={material} />}
      {tab === 'pilar' && <ColumnPanel v={v} set={set} material={material} />}
      {tab === 'laje' && <SlabPanel v={v} set={set} material={material} />}
      {tab === 'aco' && <SteelPanel v={v} set={set} />}
      {tab === 'ancoragem' && <AnchoragePanel v={v} set={set} material={material} />}
      {tab === 'cobrimento' && <CoverPanel caa={v.caa} aggregate={v.aggregate} />}
      {tab === 'quantitativo' && (
        <ToolPanel title="Quantitativo manual">
          <Notice>
            Nenhum exemplo é carregado. A lista começa vazia e contém somente os itens adicionados
            por você.
          </Notice>
          <div className="atlas-field-grid">
            <label className="atlas-field">
              <span>Elemento</span>
              <select
                value={draft.type}
                onChange={(event) =>
                  setDraft({ ...draft, type: event.target.value as QuantityItem['type'] })
                }
              >
                <option value="viga">Viga</option>
                <option value="pilar">Pilar</option>
                <option value="laje">Laje</option>
                <option value="sapata">Sapata</option>
                <option value="bloco">Bloco</option>
              </select>
            </label>
            <label className="atlas-field">
              <span>Descrição</span>
              <input
                value={draft.description}
                placeholder="Ex.: V1"
                onChange={(event) => setDraft({ ...draft, description: event.target.value })}
              />
            </label>
            <Field
              label={draft.type === 'laje' ? 'Espessura' : 'Dimensão 1'}
              unit="cm"
              value={draft.d1}
              onChange={(n) => setDraft({ ...draft, d1: n })}
            />
            <Field
              label={draft.type === 'laje' ? 'Comprimento' : 'Dimensão 2'}
              unit={draft.type === 'laje' ? 'm' : 'cm'}
              value={draft.d2}
              onChange={(n) => setDraft({ ...draft, d2: n })}
            />
            <Field
              label={
                draft.type === 'viga' || draft.type === 'pilar' || draft.type === 'laje'
                  ? 'Comprimento / largura'
                  : 'Altura'
              }
              unit={draft.type === 'sapata' || draft.type === 'bloco' ? 'cm' : 'm'}
              value={draft.d3}
              onChange={(n) => setDraft({ ...draft, d3: n })}
            />
            <Field
              label="Quantidade"
              value={draft.quantity}
              min={1}
              step={1}
              onChange={(n) => setDraft({ ...draft, quantity: n })}
            />
            <Field
              label="Taxa estimada de aço"
              unit="kg/m³"
              value={draft.steel}
              onChange={(n) => setDraft({ ...draft, steel: n })}
            />
          </div>
          <div className="quantity-toolbar">
            <button
              className="btn primary"
              type="button"
              onClick={() => {
                if (draft.d1 > 0 && draft.d2 > 0 && draft.d3 > 0) {
                  setItems((current) => [
                    ...current,
                    {
                      ...draft,
                      id: crypto.randomUUID(),
                      description: draft.description.trim() || draft.type,
                    },
                  ]);
                  setDraft((current) => ({ ...current, description: '' }));
                }
              }}
            >
              Adicionar elemento
            </button>
            {items.length > 0 && (
              <button className="btn" type="button" onClick={exportQuantities}>
                Exportar para Excel (.csv)
              </button>
            )}
          </div>
          <Results
            items={[
              ['Concreto', `${fmt(quantityTotals.concrete, 3)} m³`],
              ['Forma', `${fmt(quantityTotals.form, 2)} m²`],
              ['Aço estimado', `${fmt(quantityTotals.steel, 1)} kg`],
            ]}
          />
          {items.length ? (
            <div className="atlas-table-wrap">
              <table className="atlas-table">
                <thead>
                  <tr>
                    <th>Elemento</th>
                    <th>Qtd.</th>
                    <th>Concreto</th>
                    <th>Forma</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.id}>
                      <td>{item.description}</td>
                      <td>{item.quantity}</td>
                      <td>{item.type}</td>
                      <td>
                        {item.d1} × {item.d2} × {item.d3}
                      </td>
                      <td>
                        <button
                          className="atlas-link-button"
                          type="button"
                          onClick={() =>
                            setItems((current) => current.filter(({ id }) => id !== item.id))
                          }
                        >
                          Remover
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="atlas-empty">Adicione o primeiro elemento para iniciar o quantitativo.</p>
          )}
        </ToolPanel>
      )}
    </ToolShell>
  );
}

function ToolShell({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <section className="engineering-page">
      <div className="container engineering-heading">
        <div>
          <span className="eyebrow">
            <span className="eyebrow-dot" />
            ATLAS LABS · PROJETO EM ANDAMENTO
          </span>
          <h1>{title}</h1>
          <p>{description}</p>
        </div>
        <span className="status status-active">Cálculo local</span>
      </div>
      <div className="container engineering-workspace">
        <Notice kind="warning">
          Ferramenta de apoio para pré-dimensionamento. A validação e a responsabilidade técnica
          permanecem com profissional habilitado.
        </Notice>
        {children}
      </div>
    </section>
  );
}
function ToolPanel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="atlas-tool-panel">
      <h2>{title}</h2>
      {children}
    </section>
  );
}
function Notice({
  children,
  kind = 'info',
}: {
  children: React.ReactNode;
  kind?: 'info' | 'ok' | 'warning';
}) {
  return <div className={`atlas-notice ${kind}`}>{children}</div>;
}

function BasePanel({
  v,
  set,
  footing,
}: {
  v: Values;
  set: (k: string, n: number) => void;
  footing: { A: number; B: number; h: number; volume: number };
}) {
  const H = v.depth / 100 + 0.05;
  const bottomA = (footing.A + 2 * v.workGap) / 100;
  const bottomB = (footing.B + 2 * v.workGap) / 100;
  const topA = bottomA + 2 * H * v.slope;
  const topB = bottomB + 2 * H * v.slope;
  const excavation =
    (H / 6) *
    (bottomA * bottomB + topA * topB + 4 * ((bottomA + topA) / 2) * ((bottomB + topB) / 2));
  const backfill = Math.max(excavation - footing.volume, 0);
  return (
    <ToolPanel title="Escavação e base">
      <div className="atlas-field-grid">
        <Field label="Profundidade" unit="cm" value={v.depth} onChange={(n) => set('depth', n)} />
        <Field
          label="Substituição"
          unit="cm"
          value={v.replacement}
          onChange={(n) => set('replacement', n)}
        />
        <Field
          label="Folga de trabalho"
          unit="cm"
          value={v.workGap}
          onChange={(n) => set('workGap', n)}
        />
        <Field label="Talude H:1V" value={v.slope} step={0.1} onChange={(n) => set('slope', n)} />
        <Field label="Empolamento" unit="%" value={v.swell} onChange={(n) => set('swell', n)} />
      </div>
      <Results
        items={[
          ['Escavação', `${fmt(excavation, 2)} m³`],
          ['Reaterro aproximado', `${fmt(backfill, 2)} m³`],
          ['Bota-fora empolado', `${fmt((excavation - backfill) * (1 + v.swell / 100), 2)} m³`],
        ]}
      />
    </ToolPanel>
  );
}
function BlockPanel({
  v,
  set,
  material,
}: {
  v: Values;
  set: (k: string, n: number) => void;
  material: { fcd: number; fyd: number };
}) {
  const n = Math.max(2, Math.round(v.piles));
  const spacing = v.pileSpacing || 3 * v.pileDiameter;
  const reaction = (1.4 * v.pileLoad) / n;
  const tie = (1.4 * v.pileLoad * spacing) / (8 * Math.max(v.blockD, 1));
  const steel = tie / material.fyd;
  return (
    <ToolPanel title="Bloco sobre estacas">
      <div className="atlas-field-grid">
        <Field
          label="Número de estacas"
          value={v.piles}
          min={2}
          step={1}
          onChange={(x) => set('piles', x)}
        />
        <Field label="Carga" unit="kN" value={v.pileLoad} onChange={(x) => set('pileLoad', x)} />
        <Field
          label="Diâmetro da estaca"
          unit="cm"
          value={v.pileDiameter}
          onChange={(x) => set('pileDiameter', x)}
        />
        <Field
          label="Entre eixos"
          unit="cm"
          value={v.pileSpacing}
          onChange={(x) => set('pileSpacing', x)}
        />
        <Field
          label="Altura útil d"
          unit="cm"
          value={v.blockD}
          onChange={(x) => set('blockD', x)}
        />
      </div>
      <Results
        items={[
          ['Reação por estaca', `${fmt(reaction, 1)} kN`],
          ['Tirante estimado', `${fmt(tie, 1)} kN`],
          ['Aço necessário', `${fmt(steel, 2)} cm²`],
        ]}
      />
    </ToolPanel>
  );
}
function BeamPanel({
  v,
  set,
  material,
}: {
  v: Values;
  set: (k: string, n: number) => void;
  material: { fcd: number; fyd: number; fctm: number; cover: number };
}) {
  const d = v.beamH - v.beamCover;
  const md = 1.4 * v.beamMoment * 100;
  const k = md / Math.max(0.425 * v.beamB * d * d * material.fcd, 0.001);
  const x = k <= 1 ? 1.25 * d * (1 - Math.sqrt(Math.max(0, 1 - k))) : Infinity;
  const calculated = md / Math.max(material.fyd * (d - 0.4 * x), 0.001);
  const minimum = 0.0015 * v.beamB * v.beamH;
  const steel = Math.max(calculated, minimum);
  const shear = 1.4 * v.beamShear;
  return (
    <ToolPanel title="Viga retangular">
      <div className="atlas-field-grid">
        <Field label="Largura b" unit="cm" value={v.beamB} onChange={(n) => set('beamB', n)} />
        <Field label="Altura h" unit="cm" value={v.beamH} onChange={(n) => set('beamH', n)} />
        <Field
          label="Distância d'"
          unit="cm"
          value={v.beamCover}
          onChange={(n) => set('beamCover', n)}
        />
        <Field
          label="Momento Mk"
          unit="kN·m"
          value={v.beamMoment}
          onChange={(n) => set('beamMoment', n)}
        />
        <Field
          label="Cortante Vk"
          unit="kN"
          value={v.beamShear}
          onChange={(n) => set('beamShear', n)}
        />
      </div>
      <Results
        items={[
          ['Altura útil', `${fmt(d, 1)} cm`],
          ['Linha neutra x/d', fmt(x / d, 3)],
          ['Armadura longitudinal', `${fmt(steel, 2)} cm²`],
          ['Cortante de cálculo', `${fmt(shear, 1)} kN`],
        ]}
      />
      <Notice kind={x / d <= 0.45 ? 'ok' : 'warning'}>
        {x / d <= 0.45
          ? 'Seção dentro do limite simplificado de ductilidade.'
          : 'Revise a seção: pode ser necessária armadura dupla ou aumento da altura.'}
      </Notice>
    </ToolPanel>
  );
}
function ColumnPanel({
  v,
  set,
  material,
}: {
  v: Values;
  set: (k: string, n: number) => void;
  material: { fcd: number; fyd: number };
}) {
  const b = Math.min(v.colB, v.colH),
    h = Math.max(v.colB, v.colH),
    area = b * h,
    nd = 1.4 * v.colLoad * (b < 19 ? 1.95 - 0.05 * b : 1);
  const slenderness = (3.46 * v.colLength * 100) / b;
  const minimum = Math.max((0.15 * nd) / material.fyd, 0.004 * area);
  const loadRatio = nd / Math.max(area * material.fcd, 0.001);
  return (
    <ToolPanel title="Pilar retangular">
      <div className="atlas-field-grid">
        <Field label="Menor lado" unit="cm" value={v.colB} onChange={(n) => set('colB', n)} />
        <Field label="Maior lado" unit="cm" value={v.colH} onChange={(n) => set('colH', n)} />
        <Field
          label="Comprimento de flambagem"
          unit="m"
          value={v.colLength}
          step={0.05}
          onChange={(n) => set('colLength', n)}
        />
        <Field label="Carga Nk" unit="kN" value={v.colLoad} onChange={(n) => set('colLoad', n)} />
      </div>
      <Results
        items={[
          ['Carga de cálculo', `${fmt(nd, 0)} kN`],
          ['Esbeltez λ', fmt(slenderness, 1)],
          ['Força normal reduzida', fmt(loadRatio, 2)],
          ['Armadura mínima', `${fmt(minimum, 2)} cm²`],
        ]}
      />
      <Notice kind={b >= 14 && slenderness <= 90 ? 'ok' : 'warning'}>
        {b < 14
          ? 'O menor lado está abaixo do limite construtivo.'
          : slenderness > 90
            ? 'O método simplificado não é adequado para esta esbeltez.'
            : 'Geometria compatível com a verificação simplificada.'}
      </Notice>
    </ToolPanel>
  );
}
function SlabPanel({
  v,
  set,
  material,
}: {
  v: Values;
  set: (k: string, n: number) => void;
  material: { fyd: number; cover: number };
}) {
  const short = Math.min(v.slabLx, v.slabLy),
    long = Math.max(v.slabLx, v.slabLy),
    ratio = long / short,
    load = (25 * v.slabH) / 100 + v.slabFinish + v.slabLive,
    moment = (load * short ** 2) / (ratio > 2 ? 8 : 12);
  const d = v.slabH - material.cover / 10 - 0.5;
  const steel = Math.max(
    (1.4 * moment * 100) / Math.max(material.fyd * 0.9 * d, 0.001),
    0.0015 * 100 * v.slabH,
  );
  return (
    <ToolPanel title="Laje maciça">
      <div className="atlas-field-grid">
        <Field
          label="Vão lx"
          unit="m"
          value={v.slabLx}
          step={0.05}
          onChange={(n) => set('slabLx', n)}
        />
        <Field
          label="Vão ly"
          unit="m"
          value={v.slabLy}
          step={0.05}
          onChange={(n) => set('slabLy', n)}
        />
        <Field label="Espessura" unit="cm" value={v.slabH} onChange={(n) => set('slabH', n)} />
        <Field
          label="Revestimento"
          unit="kN/m²"
          value={v.slabFinish}
          step={0.1}
          onChange={(n) => set('slabFinish', n)}
        />
        <Field
          label="Sobrecarga"
          unit="kN/m²"
          value={v.slabLive}
          step={0.1}
          onChange={(n) => set('slabLive', n)}
        />
      </div>
      <Results
        items={[
          ['Carga total', `${fmt(load, 2)} kN/m²`],
          ['Relação ly/lx', fmt(ratio, 2), ratio > 2 ? 'Uma direção' : 'Duas direções'],
          ['Momento positivo estimado', `${fmt(moment, 2)} kN·m/m`],
          ['Armadura principal', `${fmt(steel, 2)} cm²/m`],
        ]}
      />
      <Notice kind="warning">
        Flecha, fissuração e punção não estão verificadas neste pré-dimensionamento.
      </Notice>
    </ToolPanel>
  );
}
function SteelPanel({ v, set }: { v: Values; set: (k: string, n: number) => void }) {
  const area = barArea(v.steelDiameter);
  return (
    <ToolPanel title="Conversor de armaduras">
      <div className="atlas-field-grid">
        <Field
          label="Área necessária"
          unit="cm²"
          value={v.steelArea}
          step={0.1}
          onChange={(n) => set('steelArea', n)}
        />
        <Field
          label="Quantidade de barras"
          value={v.steelBars}
          step={1}
          onChange={(n) => set('steelBars', n)}
        />
        <label className="atlas-field">
          <span>Bitola</span>
          <select
            value={v.steelDiameter}
            onChange={(event) => set('steelDiameter', Number(event.target.value))}
          >
            {bars.map((bar) => (
              <option value={bar} key={bar}>
                Ø {bar} mm
              </option>
            ))}
          </select>
        </label>
        <Field
          label="Comprimento"
          unit="m"
          value={v.steelLength}
          step={0.1}
          onChange={(n) => set('steelLength', n)}
        />
      </div>
      <Results
        items={[
          ['Barras necessárias', `${Math.ceil(v.steelArea / area)} Ø ${v.steelDiameter}`],
          ['Área fornecida', `${fmt(v.steelBars * area, 2)} cm²`],
          [
            'Peso aproximado',
            `${fmt(v.steelBars * 0.006165 * v.steelDiameter ** 2 * v.steelLength, 1)} kg`,
          ],
        ]}
      />
    </ToolPanel>
  );
}
function AnchoragePanel({
  v,
  set,
  material,
}: {
  v: Values;
  set: (k: string, n: number) => void;
  material: { fyd: number; fctm: number };
}) {
  const fbd = 2.25 * v.bond * ((0.21 * v.fck ** (2 / 3)) / 1.4);
  return (
    <ToolPanel title="Ancoragem e traspasse">
      <div className="atlas-field-grid">
        <label className="atlas-field">
          <span>Aderência</span>
          <select value={v.bond} onChange={(e) => set('bond', Number(e.target.value))}>
            <option value={1}>Boa</option>
            <option value={0.7}>Má</option>
          </select>
        </label>
        <label className="atlas-field">
          <span>Ponta da barra</span>
          <select value={v.hook} onChange={(e) => set('hook', Number(e.target.value))}>
            <option value={1}>Reta</option>
            <option value={0.7}>Com gancho</option>
          </select>
        </label>
        <Field
          label="Coeficiente de traspasse"
          value={v.lap}
          step={0.1}
          onChange={(n) => set('lap', n)}
        />
      </div>
      <div className="atlas-table-wrap">
        <table className="atlas-table">
          <thead>
            <tr>
              <th>Bitola</th>
              <th>Ancoragem necessária</th>
              <th>Traspasse</th>
            </tr>
          </thead>
          <tbody>
            {bars.map((diameter) => {
              const basic = ((diameter / 4) * (material.fyd * 10)) / fbd;
              const needed = Math.max(v.hook * basic, 10 * diameter, 100);
              return (
                <tr key={diameter}>
                  <td>Ø {diameter}</td>
                  <td>{fmt(Math.ceil(needed / 10), 0)} cm</td>
                  <td>{fmt(Math.ceil((v.lap * needed) / 10), 0)} cm</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </ToolPanel>
  );
}
function CoverPanel({ caa, aggregate }: { caa: number; aggregate: number }) {
  const covers = { laje: [20, 25, 35, 45], estrutura: [25, 30, 40, 50], solo: [30, 30, 40, 50] };
  return (
    <ToolPanel title="Cobrimentos nominais">
      <div className="atlas-table-wrap">
        <table className="atlas-table">
          <thead>
            <tr>
              <th>Elemento</th>
              <th>CAA {['I', 'II', 'III', 'IV'][caa - 1]}</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>Laje</td>
              <td>{covers.laje[caa - 1]} mm</td>
            </tr>
            <tr>
              <td>Viga / pilar</td>
              <td>{covers.estrutura[caa - 1]} mm</td>
            </tr>
            <tr>
              <td>Contato com o solo</td>
              <td>{covers.solo[caa - 1]} mm</td>
            </tr>
            <tr>
              <td>Espaçamento horizontal mínimo</td>
              <td>{Math.max(20, 1.2 * aggregate)} mm</td>
            </tr>
          </tbody>
        </table>
      </div>
      <Notice>
        Valores de referência devem ser confirmados conforme classe, controle de execução e edição
        normativa aplicável.
      </Notice>
    </ToolPanel>
  );
}
