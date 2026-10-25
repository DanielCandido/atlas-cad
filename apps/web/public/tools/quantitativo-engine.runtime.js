/* Quantitativo DXF — motor de leitura e cálculo (sem dependências de interface). */
const QD = {};

  // ------------------------------------------------------------------ texto
  function decodeBuffer(buf) {
    let t = new TextDecoder('utf-8').decode(buf);
    if (t.startsWith('AutoCAD Binary DXF')) throw new Error('DXF binário não suportado. Salve como DXF ASCII.');
    const m = t.match(/\$ACADVER\s*\r?\n\s*1\s*\r?\n\s*(AC\d+)/);
    if (m && m[1] < 'AC1021') {
      try { t = new TextDecoder('windows-1252').decode(buf); } catch (e) { /* mantém utf-8 */ }
    }
    return t;
  }
  function uni(s) { return s.replace(/\\U\+([0-9A-Fa-f]{4})/g, (_, h) => String.fromCharCode(parseInt(h, 16))); }
  function plainMText(s) {
    s = uni(s);
    s = s.replace(/\\p[^;\\]*;/g, '');
    s = s.replace(/\\P/g, '\n').replace(/\\~/g, ' ');
    s = s.replace(/\\S([^;]*);/g, (_, x) => x.replace(/[#^]/g, '/'));
    s = s.replace(/\\[ACcFfHhLlOoQqTtWwKkXx][^;\\{}]*;?/g, '');
    s = s.replace(/\\[LlOoKk]/g, '');
    s = s.replace(/[{}]/g, '');
    return pct(s);
  }
  function pct(s) {
    return s.replace(/%%[cC]/g, 'Ø').replace(/%%[dD]/g, '°').replace(/%%[pP]/g, '±').replace(/%%[uUoO]/g, '').replace(/%%%/g, '%');
  }
  function norm(s) {
    return (s || '').replace(/\s*\|\s*/g, ' ').replace(/\s+/g, ' ').trim().toUpperCase();
  }
  function num(s) {
    if (s == null) return null;
    const m = String(s).replace(/\s/g, '').match(/-?\d+(?:[.,]\d+)?/);
    return m ? parseFloat(m[0].replace(',', '.')) : null;
  }
  QD.num = num;

  // ------------------------------------------------------------------ parser
  const UNIT = { 1: 0.0254, 2: 0.3048, 4: 0.001, 5: 0.01, 6: 1, 14: 0.1 };

  function tagsToPairs(text) {
    const L = text.split(/\r?\n/);
    const pairs = [];
    let i = 0;
    while (i < L.length - 1) {
      const c = L[i].trim();
      if (!/^-?\d+$/.test(c)) { // linha de continuação (textos longos quebrados por conversores)
        if (pairs.length) pairs[pairs.length - 1][1] += L[i];
        i++; continue;
      }
      pairs.push([parseInt(c, 10), L[i + 1]]);
      i += 2;
    }
    return pairs;
  }

  function parseDXF(text, fileName) {
    const P = tagsToPairs(text);
    const header = {};
    const blocks = {};
    const ents = [];
    const dimstyles = [];
    let i = 0, section = null;
    let curBlock = null;
    let cur = null;       // entidade corrente
    let owner = null;     // INSERT/POLYLINE que recebe ATTRIB/VERTEX
    function flush() {
      if (!cur) return;
      const e = cur; cur = null;
      if (e.type === 'ATTRIB' || e.type === 'VERTEX') {
        if (owner) (owner.sub = owner.sub || []).push(e);
        return;
      }
      if (e.type === 'SEQEND') { owner = null; return; }
      if (section === 'TABLES') { if (e.type === 'DIMSTYLE') dimstyles.push(e); return; }
      if (e.type === 'INSERT' || e.type === 'POLYLINE') owner = e; else owner = null;
      if (section === 'BLOCKS') { if (curBlock) curBlock.ents.push(e); }
      else if (section === 'ENTITIES') ents.push(e);
    }
    while (i < P.length) {
      const [c, vRaw] = P[i];
      const v = vRaw.trim();
      if (c === 0) {
        flush();
        if (v === 'SECTION') { section = P[i + 1] ? P[i + 1][1].trim() : null; i += 2; continue; }
        if (v === 'ENDSEC') { section = null; i++; continue; }
        if (v === 'EOF') break;
        if (section === 'BLOCKS' && v === 'BLOCK') {
          curBlock = { name: '', ents: [], bx: 0, by: 0 };
          i++;
          while (i < P.length && P[i][0] !== 0) {
            if (P[i][0] === 2) curBlock.name = P[i][1].trim();
            if (P[i][0] === 10) curBlock.bx = parseFloat(P[i][1]);
            if (P[i][0] === 20) curBlock.by = parseFloat(P[i][1]);
            i++;
          }
          blocks[curBlock.name] = curBlock;
          continue;
        }
        if (section === 'BLOCKS' && v === 'ENDBLK') { curBlock = null; i++; while (i < P.length && P[i][0] !== 0) i++; continue; }
        if (section === 'ENTITIES' || section === 'BLOCKS' || (section === 'TABLES' && v === 'DIMSTYLE')) cur = { type: v, tags: [] };
        i++; continue;
      }
      if (section === 'HEADER' && c === 9) {
        const name = v; const vals = []; i++;
        while (i < P.length && P[i][0] !== 9 && P[i][0] !== 0) { vals.push(P[i][1].trim()); i++; }
        header[name] = vals.length === 1 ? vals[0] : vals;
        continue;
      }
      if (cur) cur.tags.push([c, vRaw]);
      i++;
    }
    flush();

    // unidade
    let f = UNIT[parseInt(header.$INSUNITS, 10)] || null;
    const model = { file: fileName || 'desenho.dxf', header, f: f || 0.001, unitGuessed: !f, blocks: {}, ents: [], dimlfac: {} };
    for (const d of dimstyles) { const nm = (g(d.tags, 2) || '').trim(); if (nm) model.dimlfac[nm.toUpperCase()] = gn(d.tags, 144, 1); }
    // blocos
    for (const [name, b] of Object.entries(blocks)) {
      const isPaper = /^\*PAPER_SPACE/i.test(name);
      const list = b.ents.map(e => build(e, model.f)).filter(Boolean);
      if (isPaper) { list.forEach(e => { e.paper = true; model.ents.push(e); }); }
      else model.blocks[name] = { name, bx: b.bx * model.f, by: b.by * model.f, ents: list };
    }
    for (const e of ents) { const b = build(e, model.f); if (b) model.ents.push(b); }
    return model;
  }

  function g(t, code) { for (const [c, v] of t) if (c === code) return v; return undefined; }
  function gn(t, code, d) { const v = g(t, code); return v === undefined ? d : parseFloat(v); }
  function pairsXY(t, cx, cy) {
    const out = []; let x = null;
    for (const [c, v] of t) {
      if (c === cx) x = parseFloat(v);
      else if (c === cy && x !== null) { out.push([x, parseFloat(v)]); x = null; }
    }
    return out;
  }

  function build(e, f) {
    const t = e.tags;
    const o = { type: e.type, layer: (g(t, 8) || '0').trim(), paper: g(t, 67) === undefined ? false : parseInt(g(t, 67), 10) === 1 };
    switch (e.type) {
      case 'LINE':
        o.x1 = gn(t, 10, 0) * f; o.y1 = gn(t, 20, 0) * f; o.x2 = gn(t, 11, 0) * f; o.y2 = gn(t, 21, 0) * f; return o;
      case 'LWPOLYLINE': {
        o.pts = pairsXY(t, 10, 20).map(([x, y]) => [x * f, y * f]);
        o.closed = (parseInt(g(t, 70) || '0', 10) & 1) === 1; return o;
      }
      case 'POLYLINE': {
        o.type = 'LWPOLYLINE';
        o.pts = (e.sub || []).map(v => [gn(v.tags, 10, 0) * f, gn(v.tags, 20, 0) * f]);
        o.closed = (parseInt(g(t, 70) || '0', 10) & 1) === 1; return o.pts.length ? o : null;
      }
      case 'CIRCLE': case 'ARC':
        o.x = gn(t, 10, 0) * f; o.y = gn(t, 20, 0) * f; o.r = gn(t, 40, 0) * f;
        o.a0 = gn(t, 50, 0); o.a1 = gn(t, 51, 360); return o;
      case 'TEXT': case 'ATTRIB': {
        const al = parseInt(g(t, 72) || '0', 10) || parseInt(g(t, 73) || '0', 10) || parseInt(g(t, 74) || '0', 10);
        o.x = gn(t, 10, 0) * f; o.y = gn(t, 20, 0) * f;
        if (al && g(t, 11) !== undefined) { o.x = gn(t, 11, 0) * f; o.y = gn(t, 21, 0) * f; o.align = 'c'; }
        o.h = gn(t, 40, 2.5) * f; o.rot = gn(t, 50, 0); o.text = pct(uni(g(t, 1) || '')); o.tag = (g(t, 2) || '').trim();
        return o;
      }
      case 'MTEXT': {
        o.x = gn(t, 10, 0) * f; o.y = gn(t, 20, 0) * f; o.h = gn(t, 40, 2.5) * f;
        let raw = ''; for (const [c, v] of t) if (c === 3) raw += v; raw += g(t, 1) || '';
        o.text = plainMText(raw);
        o.rot = gn(t, 50, NaN);
        if (isNaN(o.rot)) { const dx = gn(t, 11, 1), dy = gn(t, 21, 0); o.rot = Math.atan2(dy, dx) * 180 / Math.PI; }
        o.attach = parseInt(g(t, 71) || '1', 10);
        return o;
      }
      case 'MULTILEADER': case 'MLEADER': {
        const s = g(t, 304);
        o.type = 'MULTILEADER';
        o.text = s ? plainMText(s) : '';
        o.x = gn(t, 12, gn(t, 10, 0)) * f; o.y = gn(t, 22, gn(t, 20, 0)) * f;
        o.h = gn(t, 41, gn(t, 40, 2.5)) * f; o.rot = 0; o.attach = 1;
        o.leader = pairsXY(t, 10, 20).map(([x, y]) => [x * f, y * f]);
        return o;
      }
      case 'INSERT': {
        o.name = (g(t, 2) || '').trim(); o.x = gn(t, 10, 0) * f; o.y = gn(t, 20, 0) * f;
        o.sx = gn(t, 41, 1); o.sy = gn(t, 42, 1); o.rot = gn(t, 50, 0);
        o.attribs = (e.sub || []).filter(s => s.type === 'ATTRIB').map(s => build(s, f));
        return o;
      }
      case 'DIMENSION': {
        o.mx = gn(t, 11, 0) * f; o.my = gn(t, 21, 0) * f;
        o.p0x = gn(t, 10, 0) * f; o.p0y = gn(t, 20, 0) * f;
        o.p1x = gn(t, 13, 0) * f; o.p1y = gn(t, 23, 0) * f; o.p2x = gn(t, 14, 0) * f; o.p2y = gn(t, 24, 0) * f;
        o.raw = gn(t, 42, NaN); o.meas = o.raw * f; o.angle = gn(t, 50, 0); o.dtype = parseInt(g(t, 70) || '0', 10) & 7;
        o.style = (g(t, 3) || '').trim(); o.override = uni(g(t, 1) || '');
        o.block = (g(t, 2) || '').trim();
        for (let k = 0; k < t.length - 1; k++) if (t[k][0] === 1070 && parseInt(t[k][1], 10) === 144 && t[k + 1][0] === 1040) { o.lf = parseFloat(t[k + 1][1]); break; }
        { // medida geométrica (mais confiável que o grupo 42, que alguns conversores gravam errado)
          const dx = o.p2x - o.p1x, dy = o.p2y - o.p1y;
          const geo = o.dtype === 0 ? Math.abs(dx * Math.cos(o.angle * Math.PI / 180) + dy * Math.sin(o.angle * Math.PI / 180)) : Math.hypot(dx, dy);
          if (o.dtype === 0 || o.dtype === 1) { if (geo > 0) o.meas = geo; } else if (isNaN(o.meas)) o.meas = geo;
        }
        return o;
      }
      case 'MLINE': {
        o.w = gn(t, 40, 1) * f; o.just = parseInt(g(t, 70) || '1', 10);
        o.pts = pairsXY(t, 11, 21).map(([x, y]) => [x * f, y * f]);
        o.closed = (parseInt(g(t, 71) || '0', 10) & 2) === 2;
        return o.pts.length >= 2 ? o : null;
      }
      case 'HATCH': return null;
      default: return null;
    }
  }
  QD.parseDXF = (buf, name) => parseDXF(typeof buf === 'string' ? buf : decodeBuffer(buf), name);

  // ------------------------------------------------------------------ geometria
  const d2 = (ax, ay, bx, by) => Math.hypot(ax - bx, ay - by);
  function segDist(px, py, ax, ay, bx, by) {
    const dx = bx - ax, dy = by - ay, L2 = dx * dx + dy * dy;
    let u = L2 ? ((px - ax) * dx + (py - ay) * dy) / L2 : 0; u = Math.max(0, Math.min(1, u));
    return d2(px, py, ax + u * dx, ay + u * dy);
  }
  function polyArea(p) { let s = 0; for (let i = 0; i < p.length; i++) { const [x1, y1] = p[i], [x2, y2] = p[(i + 1) % p.length]; s += x1 * y2 - x2 * y1; } return Math.abs(s) / 2; }
  function polyPerim(p, closed) { let s = 0; const n = closed ? p.length : p.length - 1; for (let i = 0; i < n; i++) { const [x1, y1] = p[i], [x2, y2] = p[(i + 1) % p.length]; s += d2(x1, y1, x2, y2); } return s; }
  function inPoly(x, y, p) { let c = false; for (let i = 0, j = p.length - 1; i < p.length; j = i++) { const [xi, yi] = p[i], [xj, yj] = p[j]; if (((yi > y) !== (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi)) c = !c; } return c; }
  function dedupPts(p) { const o = []; for (const q of p) { const l = o[o.length - 1]; if (!l || d2(l[0], l[1], q[0], q[1]) > 1e-6) o.push(q); } if (o.length > 2 && d2(o[0][0], o[0][1], o[o.length - 1][0], o[o.length - 1][1]) < 1e-6) o.pop(); return o; }
  function asRect(e) {
    if (e.type !== 'LWPOLYLINE') return null;
    const p = dedupPts(e.pts);
    const closedish = e.closed || (e.pts.length >= 5 && d2(e.pts[0][0], e.pts[0][1], e.pts[e.pts.length - 1][0], e.pts[e.pts.length - 1][1]) < 1e-4);
    if (!closedish || p.length !== 4) return null;
    const xs = p.map(q => q[0]), ys = p.map(q => q[1]);
    const x0 = Math.min(...xs), x1 = Math.max(...xs), y0 = Math.min(...ys), y1 = Math.max(...ys);
    // eixos alinhados
    for (const [x, y] of p) if (!((Math.abs(x - x0) < 1e-3 || Math.abs(x - x1) < 1e-3) && (Math.abs(y - y0) < 1e-3 || Math.abs(y - y1) < 1e-3))) return null;
    return { x0, y0, x1, y1, w: x1 - x0, h: y1 - y0, cx: (x0 + x1) / 2, cy: (y0 + y1) / 2, layer: e.layer, e };
  }
  function clipSegRect(ax, ay, bx, by, r) { // comprimento do trecho do segmento DENTRO do retângulo
    let t0 = 0, t1 = 1; const dx = bx - ax, dy = by - ay;
    const pq = [[-dx, ax - r.x0], [dx, r.x1 - ax], [-dy, ay - r.y0], [dy, r.y1 - ay]];
    for (const [p, q] of pq) {
      if (Math.abs(p) < 1e-12) { if (q < 0) return 0; continue; }
      const t = q / p; if (p < 0) { if (t > t1) return 0; if (t > t0) t0 = t; } else { if (t < t0) return 0; if (t < t1) t1 = t; }
    }
    return Math.max(0, t1 - t0) * Math.hypot(dx, dy);
  }
  function bboxOf(pts) { const xs = pts.map(p => p[0]), ys = pts.map(p => p[1]); return [Math.min(...xs), Math.min(...ys), Math.max(...xs), Math.max(...ys)]; }
  QD.geo = { polyArea, polyPerim, inPoly, bboxOf };

  // ------------------------------------------------------------------ textos
  function textItems(model) {
    const out = [];
    const add = (o, s, extra) => {
      if (!s || !s.trim()) return;
      const h = o.h || 0.1;
      const lines = s.split('\n');
      const len = Math.max(...lines.map(l => l.length)) * h * 0.75;
      const rot = (o.rot || 0) * Math.PI / 180;
      const ux = Math.cos(rot), uy = Math.sin(rot), px = -uy, py = ux;
      let ox = 0, oy = 0; // deslocamento do ponto de inserção ao centro, em coordenadas locais
      const att = o.attach || (o.align === 'c' ? 5 : 7);
      const col = (att - 1) % 3, row = Math.floor((att - 1) / 3);
      const hh = lines.length * h * 1.4;
      ox = col === 0 ? len / 2 : col === 1 ? 0 : -len / 2;
      oy = row === 0 ? -hh / 2 : row === 1 ? 0 : hh / 2;
      if (o.type === 'TEXT' || o.type === 'ATTRIB') { oy = o.align === 'c' ? 0 : h / 2; ox = o.align === 'c' ? 0 : len / 2; }
      out.push(Object.assign({
        raw: s, s: norm(s), x: o.x, y: o.y, cx: o.x + ux * ox + px * oy, cy: o.y + uy * ox + py * oy,
        rot: ((o.rot || 0) % 180 + 180) % 180, h, len, layer: o.layer, paper: !!o.paper, src: o
      }, extra || {}));
    };
    for (const e of model.ents) {
      if (e.type === 'TEXT' || e.type === 'MTEXT' || e.type === 'MULTILEADER') add(e, e.text);
      if (e.type === 'INSERT' && e.attribs && e.attribs.length) {
        const joined = e.attribs.map(a => a.text).filter(Boolean).join(' ');
        for (const a of e.attribs) add(a, a.text, { attrTag: a.tag, insert: e, joined: norm(joined) });
      }
    }
    return out;
  }

  // ------------------------------------------------------------------ reconhecimento por arquivo
  const RE_PILAR = /^(P\d+[A-Z]?)\s+(\d+(?:[.,]\d+)?)\s*X\s*(\d+(?:[.,]\d+)?)\s*(.*)$/;
  const RE_VIGA = /^(V[A-Z]{0,2}\d+[A-Z]?)?\s*(?:([A-Z])\s*:)?\s*(\d+(?:[.,]\d+)?)\s*X\s*(\d+(?:[.,]\d+)?)$/;
  const RE_LAJE = /^(L\d+[A-Z]?)\s*H\s*=\s*(\d+(?:[.,]\d+)?)\s*CM$/;
  const RE_SAP = /^(S\d+[A-Z]?)$/;
  const RE_DET = /DET\.?\s*((?:S\d+[A-Z]?\s*=?\s*)+)\s*\((\d+)\s*X\)/;
  const RE_EL = /EL\.?\s*:?\s*(\d{2,4}[.,]\d{1,3})/g;

  function recognizeFile(model) {
    const R = { file: model.file, model, alerts: [], pilares: [], vigas: [], lajes: [], aberturas: [], niveis: [], sapatas: [], detalhes: [], piso: null, canaleta: null, resumos: [], notes: [] };
    const T = textItems(model);
    R.texts = T;
    const ms = model.ents.filter(e => !e.paper);
    const Tm = T.filter(t => !t.paper);
    const rects = ms.map(asRect).filter(Boolean);
    const alert = (level, msg, where) => R.alerts.push({ level, msg, where, file: model.file });

    if (model.unitGuessed) alert('warn', 'Unidade do desenho ($INSUNITS) não informada — assumido milímetro.');
    const cotasCm = T.some(t => /COTAS EM CENT/.test(t.s));
    // valor da cota em metros, como o desenhista lê (considera DIMLFAC do estilo)
    const dimVal = dm => dm.meas;
    R.dimVal = dimVal;

    // ---- pilares
    const pilarLabels = [];
    for (const t of Tm) {
      const m = t.s.match(RE_PILAR);
      if (m) pilarLabels.push({ t, name: m[1], a: num(m[2]) / 100, b: num(m[3]) / 100, nasce: /NASCE/.test(m[4]) });
    }
    const secs = pilarLabels.map(p => [p.a, p.b]);
    const isPilarSize = r => secs.some(([a, b]) => (Math.abs(r.w - a) < 0.006 && Math.abs(r.h - b) < 0.006) || (Math.abs(r.w - b) < 0.006 && Math.abs(r.h - a) < 0.006));
    const pilarRects0 = rects.filter(isPilarSize);
    const pilarRects = pilarRects0.filter(r => !pilarRects0.some(o => o !== r && o.w * o.h > r.w * r.h && r.x0 >= o.x0 - 1e-4 && r.x1 <= o.x1 + 1e-4 && r.y0 >= o.y0 - 1e-4 && r.y1 <= o.y1 + 1e-4));
    for (const pl of pilarLabels) {
      let best = null, bd = 1.6;
      for (const r of pilarRects) {
        const ok = (Math.abs(r.w - pl.a) < 0.006 && Math.abs(r.h - pl.b) < 0.006) || (Math.abs(r.w - pl.b) < 0.006 && Math.abs(r.h - pl.a) < 0.006);
        if (!ok) continue;
        const dd = Math.min(d2(pl.t.x, pl.t.y, r.cx, r.cy), d2(pl.t.cx, pl.t.cy, r.cx, r.cy));
        if (dd < bd) { bd = dd; best = r; }
      }
      R.pilares.push({ name: pl.name, a: pl.a, b: pl.b, nasce: pl.nasce, rect: best, x: best ? best.cx : pl.t.x, y: best ? best.cy : pl.t.y, file: model.file });
    }

    const nasceRects = new Set(R.pilares.filter(p => p.nasce && p.rect).map(p => p.rect));
    const solidSecs = pilarLabels.filter(p => !p.nasce).map(p => [p.a, p.b]);
    const deductRects = pilarRects.filter(r => !nasceRects.has(r) && solidSecs.some(([a, b]) => (Math.abs(r.w - a) < 0.006 && Math.abs(r.h - b) < 0.006) || (Math.abs(r.w - b) < 0.006 && Math.abs(r.h - a) < 0.006)));
    // ---- vigas (MLINE)
    const mlines = ms.filter(e => e.type === 'MLINE');
    const vigaLabels = [];
    for (const t of Tm) {
      const m = t.s.match(RE_VIGA);
      if (m && (m[1] || m[2])) vigaLabels.push({ t, name: m[1] || null, seg: m[2] || null, b: num(m[3]) / 100, h: num(m[4]) / 100 });
    }
    const vigas = mlines.map((e, idx) => {
      let len = 0; for (let k = 0; k < e.pts.length - 1; k++) len += d2(e.pts[k][0], e.pts[k][1], e.pts[k + 1][0], e.pts[k + 1][1]);
      const [ax, ay] = e.pts[0], [bx, by] = e.pts[e.pts.length - 1];
      const ang = ((Math.atan2(by - ay, bx - ax) * 180 / Math.PI) % 180 + 180) % 180;
      return { id: model.file + '#' + idx, e, pts: e.pts, w: e.w, len, ang, labels: [], file: model.file };
    });
    // Só MLINEs em planta (cortes costumam ter MLINE para vigas vistas de lado): exige ao menos um rótulo próximo.
    for (const vl of vigaLabels) {
      let best = null, bd = 1.0;
      for (const v of vigas) {
        let da = Math.abs(v.ang - vl.t.rot); da = Math.min(da, 180 - da);
        if (da > 15) continue;
        for (let k = 0; k < v.pts.length - 1; k++) {
          const dd = Math.min(segDist(vl.t.cx, vl.t.cy, v.pts[k][0], v.pts[k][1], v.pts[k + 1][0], v.pts[k + 1][1]),
            segDist(vl.t.x, vl.t.y, v.pts[k][0], v.pts[k][1], v.pts[k + 1][0], v.pts[k + 1][1]) + 0.05);
          if (dd < bd) { bd = dd; best = v; }
        }
      }
      if (best) best.labels.push(vl); else vl.orphan = true;
    }
    // Rótulos só de trecho ("B:25X50") herdam o nome do rótulo nomeado colinear mais próximo
    const named = vigaLabels.filter(l => l.name);
    for (const v of vigas) {
      if (!v.labels.length) continue;
      let nm = (v.labels.find(l => l.name) || {}).name;
      if (!nm) {
        let bd = 1e9;
        for (const l of named) {
          let da = Math.abs(v.ang - l.t.rot); da = Math.min(da, 180 - da); if (da > 15) continue;
          // colinear: distância perpendicular à reta da viga pequena
          const [ax, ay] = v.pts[0], [bx, by] = v.pts[v.pts.length - 1];
          const L = Math.hypot(bx - ax, by - ay) || 1;
          const perp = Math.abs((bx - ax) * (ay - l.t.cy) - (ax - l.t.cx) * (by - ay)) / L;
          if (perp > 0.8) continue;
          const dd = Math.min(d2(l.t.cx, l.t.cy, ax, ay), d2(l.t.cx, l.t.cy, bx, by));
          if (dd < bd) { bd = dd; nm = l.name; }
        }
        if (nm) v.inherited = true;
      }
      v.name = nm || 'V?';
      const segs = v.labels.map(l => l.seg).filter(Boolean).sort();
      v.segs = [...new Set(segs)];
      v.label = v.name + (v.segs.length ? ' ' + v.segs.join('+') : '');
      const secL = v.labels[0];
      v.b = secL.b; v.h = secL.h;
      const distinct = [...new Set(v.labels.map(l => l.b + 'x' + l.h))];
      if (distinct.length > 1) alert('warn', `${v.label}: rótulos com seções diferentes na mesma linha (${distinct.map(s => s.replace(/0\./g, '.')).join(', ')}). Usada a primeira.`, [v.pts[0][0], v.pts[0][1]]);
      if (Math.abs(v.w - v.b) > 0.006) alert('warn', `${v.label}: rótulo diz largura ${cm(v.b)} cm, mas está desenhada com ${cm(v.w)} cm. Adotado o rótulo.`, mid(v.pts));
      // desconto de pilares atravessados
      v.desc = []; v.descLen = 0;
      for (const r of deductRects) {
        let ov = 0; for (let k = 0; k < v.pts.length - 1; k++) ov += clipSegRect(v.pts[k][0], v.pts[k][1], v.pts[k + 1][0], v.pts[k + 1][1], r);
        if (ov > 0.02) {
          const p = R.pilares.find(q => q.rect === r);
          v.desc.push({ name: p ? p.name : 'pilar', len: ov }); v.descLen += ov;
        }
      }
      v.L = v.len - v.descLen;
    }
    const labeled = vigas.filter(v => v.labels.length);
    // grupo
    const grp = n => /^VF/.test(n) ? 'baldrame' : /^VC/.test(n) ? 'cobertura' : /^VL/.test(n) ? 'caixa' : /^VB/.test(n) ? 'baldrame' : null;
    for (const v of labeled) v.group = grp(v.name);
    for (const v of labeled.filter(v => !v.group)) {
      let bd = 1e9, gsel = null; const [mx, my] = mid(v.pts);
      for (const u of labeled) { if (!u.group) continue; const [ux, uy] = mid(u.pts); const dd = d2(mx, my, ux, uy); if (dd < bd) { bd = dd; gsel = u.group; } }
      v.group = gsel || 'cobertura';
    }
    // apara pontas que terminam dentro de outra viga (nó contado uma vez só)
    for (const v of labeled) {
      for (const endIdx of [0, v.pts.length - 1]) {
        const [ex, ey] = v.pts[endIdx];
        for (const u of labeled) {
          if (u === v) continue;
          let da = Math.abs(u.ang - v.ang); da = Math.min(da, 180 - da); if (da < 60) continue;
          for (let k = 0; k < u.pts.length - 1; k++) {
            const [ax, ay] = u.pts[k], [bx, by] = u.pts[k + 1];
            const dd = segDist(ex, ey, ax, ay, bx, by);
            if (dd < u.w / 2 - 0.005) {
              // ponta dentro da faixa de u (e não além das pontas de u)
              const L2 = (bx - ax) ** 2 + (by - ay) ** 2, t = ((ex - ax) * (bx - ax) + (ey - ay) * (by - ay)) / L2;
              if (t < -0.001 || t > 1.001) continue;
              const cut = u.w / 2 - dd;
              v.desc.push({ name: u.name, len: cut }); v.descLen += cut; v.L -= cut;
            }
          }
        }
      }
    }
    R.vigas = labeled;
    for (const vl of vigaLabels.filter(l => l.orphan)) {
      // rótulo sem MLINE (em corte é normal: só avisa quando parece planta)
      if (vl.name && vl.seg === null && !mlines.length) continue;
      alert('info', `Rótulo "${vl.t.raw.replace(/\n/g, ' ')}" sem viga desenhada como MLINE por perto — confira se precisa lançar à mão.`, [vl.t.x, vl.t.y]);
    }

    // ---- lajes
    const lajeLabels = [];
    for (const t of Tm) { const m = t.s.match(RE_LAJE); if (m) lajeLabels.push({ t, name: m[1], h: num(m[2]) / 100 }); }
    if (lajeLabels.length) {
      const closed = ms.filter(e => e.type === 'LWPOLYLINE' && (e.closed || e.pts.length >= 4)).map(e => ({ e, p: dedupPts(e.pts) })).filter(o => o.p.length >= 3);
      let best = null, ba = 0;
      for (const o of closed) {
        const n = lajeLabels.filter(l => inPoly(l.t.x, l.t.y, o.p)).length;
        if (n < Math.max(1, lajeLabels.length * 0.6)) continue;
        const a = polyArea(o.p); if (a > ba && a < 20000) { ba = a; best = o; }
      }
      const hs = [...new Set(lajeLabels.map(l => l.h))];
      if (hs.length > 1) alert('warn', `Lajes com espessuras diferentes (${hs.map(cm).join(', ')} cm). Usada a mais frequente para a laje inteira — confira.`);
      const h = mode(lajeLabels.map(l => l.h));
      if (best) {
        const lj = { name: [...new Set(lajeLabels.map(l => l.name))].join(', '), h, pts: best.p, area: polyArea(best.p), perim: polyPerim(best.p, true), bbox: bboxOf(best.p), file: model.file };
        // aberturas: retângulos internos com X (duas diagonais)
        const lines = ms.filter(e => e.type === 'LINE');
        for (const r of rects) {
          if (r.w > 3 || r.h > 3 || r.w < 0.2 || r.h < 0.2) continue;
          if (!inPoly(r.cx, r.cy, best.p)) continue;
          const diag = lines.filter(l => {
            const a = [[r.x0, r.y0, r.x1, r.y1], [r.x0, r.y1, r.x1, r.y0]];
            return a.some(([x0, y0, x1, y1]) => (d2(l.x1, l.y1, x0, y0) < 0.02 && d2(l.x2, l.y2, x1, y1) < 0.02) || (d2(l.x2, l.y2, x0, y0) < 0.02 && d2(l.x1, l.y1, x1, y1) < 0.02));
          });
          if (diag.length >= 2) lj.aberturas = (lj.aberturas || []).concat([{ w: r.w, h: r.h, rect: r }]);
        }
        lj.aberturas = lj.aberturas || [];
        lj.pilarArea = pilarRects.filter(r => inPoly(r.cx, r.cy, best.p) && !R.pilares.some(p => p.rect === r && p.nasce)).reduce((s, r) => s + r.w * r.h, 0);
        R.lajes.push(lj);
      } else alert('warn', 'Achei rótulos de laje mas não o contorno fechado da laje — lance a área à mão.');
    }

    // ---- níveis
    for (const t of T) {
      const src = t.joined || t.s;
      if (t.attrTag && t.joined && t !== T.find(q => q.insert === t.insert && /EL/.test(q.s))) continue;
      let m; RE_EL.lastIndex = 0;
      while ((m = RE_EL.exec(src))) {
        const v = num(m[1]);
        const before = src.slice(Math.max(0, m.index - 40), m.index), after = src.slice(m.index + m[0].length, m.index + m[0].length + 14);
        const ctx = before + ' ' + after;
        let kind = 'outro';
        if (/TOPO DAS VIGAS/.test(before)) kind = 'topo-vigas';
        else if (/PLATIBANDA|CAIXA/.test(before)) kind = 'caixa';
        else if (/LAJE|COBERTURA/.test(ctx)) kind = 'laje';
        else if (/PISO/.test(ctx)) kind = 'piso';
        R.niveis.push({ v, kind, txt: src.slice(0, 80), paper: t.paper, x: t.x, y: t.y, file: model.file });
      }
    }

    // ---- sapatas (planta)
    const sLabels = Tm.filter(t => RE_SAP.test(t.s)).map(t => ({ t, name: t.s }));
    if (sLabels.length) {
      const big = rects.filter(r => r.w > 0.5 && r.h > 0.5 && r.w < 8 && r.h < 8);
      for (const r of big) {
        const pin = pilarRects.find(p => p.cx > r.x0 && p.cx < r.x1 && p.cy > r.y0 && p.cy < r.y1);
        if (!pin) continue;
        // evita o colarinho (retângulo logo acima do pilar)
        if (r.w < pin.w + 0.2) continue;
        let sl = null, sd = 1.2;
        for (const l of sLabels) {
          const dx = Math.max(r.x0 - l.t.x, 0, l.t.x - r.x1), dy = Math.max(r.y0 - l.t.y, 0, l.t.y - r.y1);
          const dd = Math.hypot(dx, dy); if (dd < sd) { sd = dd; sl = l; }
        }
        if (!sl) continue;
        const p = R.pilares.find(q => q.rect === pin);
        R.sapatas.push({ name: sl ? sl.name : 'S?', pilar: p ? p.name : '?', A: r.w, B: r.h, rect: r, pin, file: model.file });
      }
      // dedup por nome (retângulos duplicados)
      const seen = {}; R.sapatas = R.sapatas.filter(s => { const k = s.name + s.pilar; if (seen[k]) return false; seen[k] = 1; return true; });
      for (const l of sLabels) if (!R.sapatas.some(s => s.name === l.name)) alert('warn', `Sapata ${l.name}: rótulo sem contorno reconhecido.`, [l.t.x, l.t.y]);
    }
    // ---- detalhes de sapata
    const dets = [];
    for (const t of Tm) { const m = t.s.match(RE_DET); if (m) dets.push({ t, names: m[1].split(/[=\s]+/).filter(Boolean), n: parseInt(m[2], 10) }); }
    if (dets.length) {
      dets.sort((a, b) => a.t.x - b.t.x);
      const dims = ms.filter(e => e.type === 'DIMENSION');
      for (let k = 0; k < dets.length; k++) {
        const d = dets[k];
        const xl = Math.max(d.t.x - 2, k ? dets[k - 1].t.x + 0.5 : -1e9), xr = k < dets.length - 1 ? dets[k + 1].t.x - 2 : d.t.x + 9;
        const yb = d.t.y - 0.6, yt = d.t.y + 9;
        const inR = (x, y) => x > xl && x < xr && y > yb && y < yt;
        const planta = Tm.find(t => t.s === 'PLANTA' && inR(t.x, t.y));
        const yCut = planta ? planta.y : yt;
        const V = dims.filter(dm => inR(dm.mx, dm.my) && dm.my < yCut && isVertical(dm)).map(dm => ({ v: dimVal(dm), y0: Math.min(dm.p1y, dm.p2y), y1: Math.max(dm.p1y, dm.p2y) }));
        const det = { names: d.names, n: d.n, x: d.t.x, y: d.t.y, ok: false };
        if (V.length) {
          const tot = Math.max(...V.map(q => q.v));
          const bottom = Math.min(...V.map(q => q.y0));
          const anch = V.filter(q => Math.abs(q.y0 - bottom) < 0.02 && Math.abs(q.v - tot) > 0.005).map(q => q.v).sort((a, b) => a - b);
          const u = [...new Set(anch.map(v => Math.round(v * 1000) / 1000))];
          if (u.length >= 2) { det.h0 = u[0]; det.h = u[u.length - 1]; det.ok = true; }
          else if (u.length === 1) { det.h = u[0]; det.h0 = null; det.ok = true; }
          det.prof = tot;
        }
        const mg = Tm.find(t => /CONCRETO MAGRO/.test(t.s) && inR(t.x, t.y));
        if (mg) { const m = mg.s.match(/E\s*=\s*(\d+(?:[.,]\d+)?)\s*CM/); if (m) det.magro = num(m[1]) / 100; }
        if (!det.ok) alert('warn', `DET. ${d.names.join('=')}: não consegui ler as alturas nas cotas — preencha h e h0.`, [d.t.x, d.t.y]);
        R.detalhes.push(det);
      }
    }

    // ---- piso
    const pisoTags = Tm.filter(t => /PISO INTERNO/.test(t.s) && !/PLANTA/.test(t.s));
    if (pisoTags.length) {
      let h = null;
      for (const t of pisoTags) { const m = t.s.match(/H\s*=\s*(\d+(?:[.,]\d+)?)\s*CM/); if (m) h = num(m[1]) / 100; }
      if (!h) { const near = Tm.find(t => /^H\s*=\s*\d/.test(t.s) && pisoTags.some(p => d2(p.x, p.y, t.x, t.y) < 1)); if (near) h = num(near.s.match(/(\d+(?:[.,]\d+)?)/)[1]) / 100; }
      const closed = ms.filter(e => e.type === 'LWPOLYLINE').map(e => ({ e, p: dedupPts(e.pts) })).filter(o => o.p.length >= 4 && (o.e.closed || o.e.pts.length >= 5));
      let best = null, ba = 0;
      for (const o of closed) { if (!pisoTags.every(t => inPoly(t.x, t.y, o.p))) continue; const a = polyArea(o.p); if (a > ba && a < 20000) { ba = a; best = o; } }
      if (best) R.piso = { h: h || 0.15, hFound: !!h, pts: best.p, area: polyArea(best.p), perim: polyPerim(best.p, true), bbox: bboxOf(best.p), file: model.file };
    }
    // notas do piso (qualquer espaço)
    const allS = T.map(t => t.s).join('\n');
    let m;
    if ((m = allS.match(/JUNTAS SERRADAS[^=\n]*=\s*(\d+(?:[.,]\d+)?)\s*M/))) R.notes.push({ k: 'js', v: num(m[1]) });
    if ((m = allS.match(/TELA\s+(?:SOLDADA\s+|SUPERIOR\s+)?(Q-?\d{2,3})/))) R.notes.push({ k: 'tela', v: m[1].replace('Q', 'Q-').replace('--', '-') });
    if ((m = allS.match(/(\d+)\s*BUZINOTES/))) R.notes.push({ k: 'buzNota', v: parseInt(m[1], 10) });
    const buzLabels = T.filter(t => /BUZINOTE/.test(t.s) && !/\d+\s*BUZINOTES/.test(t.s)).length;
    if (buzLabels) R.notes.push({ k: 'buzPlanta', v: buzLabels });
    if ((m = allS.match(/(\d+(?:[.,]\d+)?)\s*M\s*LINEARES DE PLATIBANDA[^\n]*?(\d+(?:[.,]\d+)?)\s*M DE ALTURA/))) R.notes.push({ k: 'platNota', v: [num(m[1]), num(m[2])] });

    // ---- canaleta
    const canTags = Tm.filter(t => /CANALETA/.test(t.s));
    if (canTags.length) {
      const lines = ms.filter(e => e.type === 'LINE' && d2(e.x1, e.y1, e.x2, e.y2) > 2 && canTags.some(t => segDist(t.x, t.y, e.x1, e.y1, e.x2, e.y2) < 2.5));
      let L = 0, where = null;
      for (const l of lines) {
        const len = d2(l.x1, l.y1, l.x2, l.y2);
        const mates = lines.filter(o => o !== l && Math.abs(d2(o.x1, o.y1, o.x2, o.y2) - len) < 0.02 && segDist(o.x1, o.y1, l.x1, l.y1, l.x2, l.y2) < 0.8 && segDist(o.x2, o.y2, l.x1, l.y1, l.x2, l.y2) < 0.8);
        if (mates.length && len > L) { L = len; where = l; }
      }
      const arcs = ms.filter(e => e.type === 'ARC' && canTags.some(t => d2(t.x, t.y, e.x, e.y) < 3));
      const rs = arcs.map(a => a.r).sort((a, b) => a - b);
      R.canaleta = { L: L || null, ri: rs.length ? rs[0] : null, re: rs.length ? rs[rs.length - 1] : null, line: where, file: model.file };
    }

    // ---- quadro "Resumo" do desenho (espaço do papel ou modelo)
    const heads = T.filter(t => /VOL\.?\s*DE\s*CONCRETO/.test(t.s));
    for (const hd of heads) {
      const fh = T.find(t => /AREA DE FORMAS|ÁREA DE FORMAS/.test(t.s) && t.paper === hd.paper && Math.abs(t.y - hd.y) < hd.h * 3);
      const rowNames = ['SAPATAS', 'BLOCOS', 'ESTACAS', 'PILARES', 'VIGAS BALDRAME', 'VIGAS SUPRA', 'VIGAS COBERTURA', 'LAJES', 'LAJE', 'CONCRETO MAGRO', 'TOTAL'];
      const rows = [];
      for (const t of T) {
        if (t.paper !== hd.paper || !rowNames.includes(t.s) || t.y > hd.y || hd.y - t.y > hd.h * 40) continue;
        if (Math.abs(t.x - hd.x) > hd.h * 60) continue;
        const nums = T.filter(q => q.paper === hd.paper && Math.abs(q.y - t.y) < t.h * 0.9 && /^-?[\d.,]+$|^-$/.test(q.s) && q !== t);
        const pick = (hx) => { let b = null, bd = 1e9; for (const q of nums) { const dd = Math.abs(q.cx - hx); if (dd < bd) { bd = dd; b = q; } } return b; };
        const c = pick(hd.cx), fo = fh ? pick(fh.cx) : null;
        rows.push({ k: t.s, conc: c && c.s !== '-' ? num(c.s) : null, forma: fo && fo.s !== '-' && fo !== c ? num(fo.s) : null });
      }
      if (rows.length >= 3) R.resumos.push({ rows, file: model.file });
    }
    R.pilarRects = pilarRects;
    return R;
  }
  function isVertical(dm) {
    if (dm.dtype === 0) { const s = Math.abs(Math.sin(dm.angle * Math.PI / 180)); return s > 0.9; }
    return Math.abs(dm.p2x - dm.p1x) < Math.abs(dm.p2y - dm.p1y) * 0.2;
  }
  function mid(p) { return [(p[0][0] + p[p.length - 1][0]) / 2, (p[0][1] + p[p.length - 1][1]) / 2]; }
  function mode(a) { const c = {}; let b = null, bn = 0; for (const x of a) { c[x] = (c[x] || 0) + 1; if (c[x] > bn) { bn = c[x]; b = x; } } return b; }
  const cm = v => String(Math.round(v * 1000) / 10).replace('.', ',');
  QD.recognizeFile = recognizeFile;

  // ------------------------------------------------------------------ projeto (vários arquivos)
  QD.defaultPrem = () => ({ perda: 0.05, fundoVF: 0, fundoVL: 1, fColar: 0.025, eMagro: 0.05, fMagro: 0.05, fEsc: 0.30, descPilarPiso: 1, subBase: 0.15, perdaTela: 0.30, hBaldrameRef: 0.50 });
  const TELA = { 'Q-61': 0.97, 'Q-75': 1.21, 'Q-92': 1.48, 'Q-113': 1.8, 'Q-138': 2.2, 'Q-159': 2.52, 'Q-196': 3.11, 'Q-246': 3.91, 'Q-283': 4.48, 'Q-335': 5.37, 'Q-396': 6.28, 'Q-503': 7.97, 'Q-636': 10.09 };
  QD.TELA = TELA;

  QD.buildProject = function (recs, prem) {
    const P = { prem: Object.assign(QD.defaultPrem(), prem || {}), files: recs.map(r => r.file), alerts: [], recs };
    const A = (level, msg, file, where) => P.alerts.push({ level, msg, file, where });
    recs.forEach(r => r.alerts.forEach(a => P.alerts.push(a)));

    // pilares: principais (não nascem, não em planta da caixa), caixa
    const byName = {};
    for (const r of recs) for (const p of r.pilares) (byName[p.name] = byName[p.name] || []).push(p);
    // pilar da caixa: fica perto de vigas VL no mesmo arquivo
    const nearVL = (p, r) => r.vigas.some(v => v.group === 'caixa' && Math.min(d2(p.x, p.y, v.pts[0][0], v.pts[0][1]), d2(p.x, p.y, v.pts[v.pts.length - 1][0], v.pts[v.pts.length - 1][1])) < 0.6);
    const main = [], cx = [], nasceNames = new Set();
    for (const r of recs) for (const p of r.pilares) { if (p.nasce) nasceNames.add(p.name); if (nearVL(p, r)) p.caixa = true; }
    const cxNames = new Set(); for (const r of recs) for (const p of r.pilares) if (p.caixa) cxNames.add(p.name);
    for (const [n, list] of Object.entries(byName)) {
      const p = list[0];
      const secs = [...new Set(list.map(q => cm(q.a) + 'x' + cm(q.b)))];
      if (secs.length > 1) A('warn', `${n}: seção diferente entre plantas (${secs.join(' / ')}).`);
      if (cxNames.has(n) || nasceNames.has(n)) continue;
      main.push({ name: n, a: p.a, b: p.b, src: list });
    }
    main.sort((a, b) => natural(a.name, b.name));
    for (const n of cxNames) { const p = byName[n][0]; cx.push({ name: n, a: p.a, b: p.b }); }
    if (!cx.length) for (const n of nasceNames) { const p = byName[n][0]; cx.push({ name: n, a: p.a, b: p.b }); }
    cx.sort((a, b) => natural(a.name, b.name));
    if (cxNames.size && nasceNames.size) {
      const a = [...nasceNames].sort(natural), b = [...cxNames].sort(natural);
      if (a.join() !== b.join()) A('warn', `Pilares que nascem na cobertura (${a.join(', ')}) têm nomes diferentes na planta da caixa d'água (${b.join(', ')}).`);
    }
    P.pilares = main; P.pilaresCx = cx;

    // vigas (dedup por nome+grupo entre arquivos: vale o último)
    const vmap = new Map();
    for (const r of recs) for (const v of r.vigas) {
      const k = v.group + '|' + v.label;
      if (vmap.has(k) && vmap.get(k).file !== v.file) A('info', `${v.label} aparece em ${vmap.get(k).file} e ${v.file} — usada a de ${v.file}.`);
      vmap.set(k, v);
    }
    P.vigas = [...vmap.values()].map(v => ({ id: v.id, label: v.label, name: v.name, group: v.group, b: v.b, h: v.h, L: round(v.L, 4), len: v.len, desc: v.desc, file: v.file, pts: v.pts, w: v.w, qtd: 1 }));
    P.vigas.sort((a, b) => ['baldrame', 'cobertura', 'caixa'].indexOf(a.group) - ['baldrame', 'cobertura', 'caixa'].indexOf(b.group) || natural(a.label, b.label));

    // laje
    const lajes = recs.flatMap(r => r.lajes);
    if (lajes.length > 1) A('info', `Mais de um contorno de laje encontrado — usado o de ${lajes[lajes.length - 1].file}.`);
    P.laje = lajes.length ? Object.assign({}, lajes[lajes.length - 1]) : null;

    // níveis
    const nv = recs.flatMap(r => r.niveis);
    const pick = k => nv.filter(n => n.kind === k).map(n => n.v);
    const lajeV = pick('laje'), pisoV = pick('piso'), cxV = pick('caixa'), outros = pick('outro');
    const lev = {};
    lev.laje = mode(lajeV); lev.piso = mode(pisoV);
    const all = nv.map(n => n.v);
    lev.caixa = cxV.length ? Math.max(...cxV) : null;
    if (lev.laje != null) {
      const acima = outros.filter(v => v > lev.laje);
      if (!lev.caixa && acima.length) lev.caixa = Math.max(...acima);
      const plat = acima.filter(v => v < (lev.caixa || 1e9)); lev.plat = plat.length ? mode(plat) : null;
      const abaixo = outros.filter(v => v < lev.laje); lev.terreno = abaixo.length ? mode(abaixo) : null;
    }
    if (lev.terreno == null && lev.piso != null) lev.terreno = round(lev.piso - 0.15, 3);
    const uniq = a => [...new Set(a)];
    if (uniq(lajeV).length > 1) A('warn', `Nível da laje aparece com valores diferentes: ${uniq(lajeV).map(fmtEL).join(', ')}. Adotado ${fmtEL(lev.laje)}.`);
    if (uniq(pisoV).length > 1) A('warn', `Nível do piso aparece com valores diferentes: ${uniq(pisoV).map(fmtEL).join(', ')}. Adotado ${fmtEL(lev.piso)}.`);
    for (const n of nv.filter(n => n.kind === 'topo-vigas')) if (lev.laje != null && Math.abs(n.v - lev.laje) > 0.005) A('warn', `Texto diz "topo das vigas na EL ${fmtEL(n.v)}", mas a laje está na ${fmtEL(lev.laje)}.`, n.file);
    P.niveis = lev;
    if (lev.laje == null) A('warn', 'Não achei o nível da laje — preencha nas premissas.');

    // espessura da laje e VL
    P.hLaje = P.laje ? P.laje.h : 0.20;
    const vl = P.vigas.filter(v => v.group === 'caixa');
    P.hVL = vl.length ? vl[0].h : 0.20;

    // sapatas
    const detBy = {};
    const detList = recs.flatMap(r => r.detalhes);
    for (const d of detList) for (const n of d.names) detBy[n] = d;
    const saps = recs.flatMap(r => r.sapatas);
    P.sapatas = saps.map(s => {
      const d = detBy[s.name] || {};
      return { name: s.name, pilar: s.pilar, a: s.pin.w, b: s.pin.h, A: round(s.A, 4), B: round(s.B, 4), h: d.h != null ? d.h : null, h0: d.h0 != null ? d.h0 : null, prof: d.prof != null ? d.prof : null, qtd: 1, file: s.file, rect: s.rect, det: d.names ? 'DET. ' + d.names.join('=') : null };
    }).sort((a, b) => natural(a.name, b.name));
    // colar orientação do pilar à orientação A/B
    for (const s of P.sapatas) { const p = main.find(q => q.name === s.pilar); if (p) { s.a = s.a; } }
    for (const d of detList) {
      const using = P.sapatas.filter(s => d.names.includes(s.name)).length;
      if (using && using !== d.n) A('warn', `DET. ${d.names.join('=')} diz (${d.n}X), mas há ${using} sapata(s) com esses nomes na planta.`);
      if (!using) A('info', `DET. ${d.names.join('=')} não corresponde a nenhuma sapata da planta.`);
    }
    for (const s of P.sapatas) if (!s.det) A('warn', `Sapata ${s.name} sem detalhe — preencha as alturas.`);
    const magros = detList.map(d => d.magro).filter(Boolean); if (magros.length) P.prem.eMagro = mode(magros);
    for (const p of main) if (P.sapatas.length && !P.sapatas.some(s => s.pilar === p.name)) A('warn', `Pilar ${p.name} sem sapata associada na planta de fundação.`);

    // piso
    const pisos = recs.map(r => r.piso).filter(Boolean);
    P.piso = pisos.length ? Object.assign({}, pisos[pisos.length - 1]) : null;
    const notes = recs.flatMap(r => r.notes);
    const note = k => { const n = notes.find(q => q.k === k); return n ? n.v : null; };
    if (P.piso) {
      P.piso.js = note('js') || 0; P.piso.tela = note('tela') || 'Q-196';
      P.piso.nPilares = main.length;
    }
    // canaleta
    const can = recs.map(r => r.canaleta).filter(Boolean).find(c => c.L) || recs.map(r => r.canaleta).find(Boolean);
    if (can && !can.L) A('warn', 'Canaleta indicada, mas não achei o comprimento — preencha.');
    P.canaleta = can ? { L: can.L || 0, ri: can.ri || 0.20, e: can.re && can.ri ? can.re - can.ri : 0.04 } : null;
    // buzinotes
    const bn = note('buzNota'), bp = notes.filter(q => q.k === 'buzPlanta').reduce((s, q) => Math.max(s, q.v), 0);
    P.buzinotes = bn || bp || 0;
    if (bn && bp && bn !== bp) A('warn', `Buzinotes: a nota diz ${bn} un, a planta indica ${bp}.`);
    P.platNota = note('platNota');
    P.resumos = recs.flatMap(r => r.resumos);
    return P;
  };
  function round(v, n) { const k = Math.pow(10, n); return Math.round(v * k) / k; }
  function natural(a, b) { return String(a).localeCompare(String(b), 'pt-BR', { numeric: true }); }
  function fmtEL(v) { return v == null ? '—' : v.toFixed(3).replace('.', ','); }
  QD.fmtEL = fmtEL;

  // ------------------------------------------------------------------ cálculo
  QD.compute = function (P) {
    const pr = P.prem, lev = P.niveis, hL = P.hLaje;
    const out = { rows: {}, comp: {}, check: [] };
    const fundoLaje = (lev.laje ?? 0) - hL;
    // sapatas
    let sc = 0, sf = 0, sm = 0, se = 0, sr = 0;
    for (const s of P.sapatas) {
      const q = s.qtd || 0, ok = s.A && s.B && s.h;
      const a0 = s.a + 2 * pr.fColar, b0 = s.b + 2 * pr.fColar;
      s.vol = !ok ? 0 : q * ((s.h0 == null || s.h0 >= s.h) ? s.A * s.B * s.h : s.A * s.B * s.h0 + (s.h - s.h0) / 3 * (s.A * s.B + a0 * b0 + Math.sqrt(s.A * s.B * a0 * b0)));
      s.forma = !ok ? 0 : q * 2 * (s.A + s.B) * (s.h0 == null ? s.h : s.h0);
      s.magro = !ok ? 0 : q * (s.A + 2 * pr.fMagro) * (s.B + 2 * pr.fMagro) * pr.eMagro;
      s.topo = (lev.terreno != null && s.prof != null && s.h) ? lev.terreno - s.prof + s.h : null;
      s.esc = (ok && s.prof != null) ? q * (s.A + 2 * pr.fEsc) * (s.B + 2 * pr.fEsc) * (s.prof + pr.eMagro) : 0;
      s.reat = s.esc ? s.esc - s.vol - s.magro - q * s.a * s.b * (lev.terreno - s.topo) : 0;
      sc += s.vol; sf += s.forma; sm += s.magro; se += s.esc; sr += s.reat;
    }
    out.rows.sapatas = { label: 'Sapatas', qtd: P.sapatas.reduce((s, x) => s + (x.qtd || 0), 0), un: 'un', conc: sc, forma: sf };
    // pilares
    let pc = 0, pf = 0;
    const baseDefault = (lev.terreno ?? 0) - pr.hBaldrameRef;
    for (const p of P.pilares) {
      const s = P.sapatas.find(x => x.pilar === p.name);
      p.base = p.baseManual != null ? p.baseManual : (s && s.topo != null ? s.topo : baseDefault);
      p.semBase = !(s && s.topo != null) && p.baseManual == null;
      p.H = fundoLaje - p.base; p.vol = p.a * p.b * p.H; p.forma = 2 * (p.a + p.b) * p.H; pc += p.vol; pf += p.forma;
    }
    out.rows.pilares = { label: 'Pilares', qtd: P.pilares.length, un: 'un', conc: pc, forma: pf };
    let xc = 0, xf = 0; const Hc = (lev.caixa ?? 0) - P.hVL - (lev.laje ?? 0);
    for (const p of P.pilaresCx) { p.H = Hc; p.vol = p.a * p.b * Hc; p.forma = 2 * (p.a + p.b) * Hc; xc += p.vol; xf += p.forma; }
    out.rows.pilaresCx = { label: "Pilares da caixa d'água", qtd: P.pilaresCx.length, un: 'un', conc: xc, forma: xf };
    // vigas
    const G = { baldrame: [0, 0, 0], cobertura: [0, 0, 0], caixa: [0, 0, 0] };
    let occ = 0, impVF = 0;
    for (const v of P.vigas) {
      const q = v.qtd || 0;
      if (v.group === 'cobertura') { v.hu = v.h - hL; v.vol = q * v.b * v.hu * v.L; v.forma = q * (2 * v.hu + v.b) * v.L; occ += q * v.b * v.L; }
      else if (v.group === 'caixa') { v.hu = v.h; v.vol = q * v.b * v.h * v.L; v.forma = q * (2 * v.h + pr.fundoVL * v.b) * v.L; }
      else { v.hu = v.h; v.vol = q * v.b * v.h * v.L; v.forma = q * (2 * v.h + pr.fundoVF * v.b) * v.L; impVF += q * (2 * v.h + v.b) * v.L; }
      const g = G[v.group] || G.cobertura; g[0] += q * v.L; g[1] += v.vol; g[2] += v.forma;
    }
    out.rows.vf = { label: 'Vigas baldrame', qtd: G.baldrame[0], un: 'm', conc: G.baldrame[1], forma: G.baldrame[2] };
    out.rows.vc = { label: 'Vigas de cobertura', qtd: G.cobertura[0], un: 'm', conc: G.cobertura[1], forma: G.cobertura[2] };
    // laje
    if (P.laje) {
      const L = P.laje; const ab = (L.aberturas || []).reduce((s, a) => s + a.w * a.h, 0), abP = (L.aberturas || []).reduce((s, a) => s + 2 * (a.w + a.h), 0);
      L.conc = (L.area - ab) * hL; L.ffundo = L.area - ab - occ - (L.pilarArea || 0); L.fborda = (L.perim + abP) * hL; L.forma = L.ffundo + L.fborda; L.abArea = ab;
      out.rows.laje = { label: `Laje ${L.name || ''} h=${cm(hL)}`.replace(/\s+/g, ' '), qtd: L.area, un: 'm²', conc: L.conc, forma: L.forma };
    }
    out.rows.vl = { label: "Vigas da caixa d'água", qtd: G.caixa[0], un: 'm', conc: G.caixa[1], forma: G.caixa[2] };
    // piso
    if (P.piso) {
      const S = P.piso; const ap = P.pilares.reduce((s, p) => s + p.a * p.b, 0);
      S.al = S.area - pr.descPilarPiso * ap; S.conc = S.al * S.h; S.forma = S.perim * S.h;
      out.rows.piso = { label: `Piso h=${cm(S.h)}`, qtd: S.al, un: 'm²', conc: S.conc, forma: S.forma };
      out.comp.subBase = S.area * pr.subBase; out.comp.lona = S.area; out.comp.tela = S.area * (TELA[S.tela] || 3.11) * (1 + pr.perdaTela); out.comp.js = S.js;
    }
    if (P.canaleta && P.canaleta.L) {
      const c = P.canaleta; c.sec = Math.PI / 2 * ((c.ri + c.e) ** 2 - c.ri ** 2); c.conc = c.L * c.sec;
      out.rows.canaleta = { label: `Canaleta meia-cana Ø${cm(2 * c.ri)}`, qtd: c.L, un: 'm', conc: c.conc, forma: 0 };
    }
    out.comp.magro = sm; out.comp.esc = se; out.comp.reat = sr; out.comp.impVF = impVF;
    if (P.laje) { out.comp.impLaje = P.laje.area - P.laje.abArea + P.laje.perim * 0.30; out.comp.ping = P.laje.perim; }
    out.comp.buz = P.buzinotes;
    const order = ['sapatas', 'pilares', 'pilaresCx', 'vf', 'vc', 'laje', 'vl', 'piso', 'canaleta'];
    out.list = order.filter(k => out.rows[k] && (out.rows[k].conc > 0 || out.rows[k].qtd > 0)).map(k => Object.assign({ key: k }, out.rows[k]));
    out.totConc = out.list.reduce((s, r) => s + r.conc, 0); out.totForma = out.list.reduce((s, r) => s + r.forma, 0);
    out.perda = out.totConc * pr.perda; out.totComPerda = out.totConc + out.perda;

    // conferência com quadros "Resumo" do desenho
    for (const rz of P.resumos) {
      const map = { SAPATAS: 'sapatas', PILARES: 'pilares+', 'VIGAS BALDRAME': 'vf', LAJES: 'laje', LAJE: 'laje', 'CONCRETO MAGRO': 'magro', 'VIGAS COBERTURA': 'vc' };
      const hasCob = rz.rows.some(r => r.k === 'VIGAS COBERTURA');
      const rows = [];
      let sumRows = 0;
      for (const r of rz.rows) {
        let k = map[r.k]; if (r.k === 'VIGAS SUPRA') k = hasCob ? 'vl' : 'vc';
        if (r.k === 'TOTAL') continue;
        if (r.conc != null) sumRows += r.conc;
        let mine = null, mineF = null;
        if (k === 'pilares+') { mine = (out.rows.pilares.conc || 0) + (out.rows.pilaresCx.conc || 0); mineF = (out.rows.pilares.forma || 0) + (out.rows.pilaresCx.forma || 0); }
        else if (k === 'magro') mine = sm;
        else if (k && out.rows[k]) { mine = out.rows[k].conc; mineF = out.rows[k].forma; }
        rows.push({ k: r.k, conc: r.conc, forma: r.forma, mine, mineF });
      }
      const tot = rz.rows.find(r => r.k === 'TOTAL');
      rz.cmp = rows; rz.sumRows = sumRows; rz.total = tot ? tot.conc : null; rz.totalForma = tot ? tot.forma : null;
    }
    return out;
  };

  QD.reviewAlerts = function (P, R) {
    const out = [];
    for (const rz of P.resumos) {
      if (rz.total != null && Math.abs(rz.sumRows - rz.total) > 0.05) out.push({ level: 'warn', msg: `Quadro "Resumo" do desenho: o TOTAL (${br(rz.total, 3)} m³) não bate com a soma das linhas (${br(rz.sumRows, 3)} m³).`, file: rz.file });
      for (const r of rz.cmp) {
        if (r.conc == null || r.mine == null || r.mine === 0 && r.conc === 0) continue;
        const dv = r.mine ? (r.conc - r.mine) / r.mine : 1;
        if (Math.abs(dv) > 0.03) out.push({ level: 'warn', msg: `Quadro "Resumo" do desenho: ${r.k} = ${br(r.conc, 3)} m³; calculado aqui ${br(r.mine, 3)} m³ (${dv > 0 ? '+' : ''}${br(dv * 100, 1)}%).`, file: rz.file });
        if (r.forma != null && r.mineF) { const df = (r.forma - r.mineF) / r.mineF; if (Math.abs(df) > 0.05) out.push({ level: 'info', msg: `Quadro "Resumo": forma de ${r.k} = ${br(r.forma, 2)} m²; calculado aqui ${br(r.mineF, 2)} m² (${df > 0 ? '+' : ''}${br(df * 100, 0)}%).`, file: rz.file }); }
      }
    }
    if (P.platNota && P.laje) {
      const [Lp, hp] = P.platNota; const hc = P.niveis.plat != null && P.niveis.laje != null ? P.niveis.plat - P.niveis.laje : null;
      if (Math.abs(Lp - P.laje.perim) > 0.5 || (hc && Math.abs(hp - hc) > 0.02)) out.push({ level: 'warn', msg: `Nota de platibanda: ${br(Lp, 1)} m com ${br(hp, 2)} m de altura; o contorno da laje tem ${br(P.laje.perim, 1)} m${hc ? ' e os níveis dão ' + br(hc, 2) + ' m' : ''}.` });
    }
    for (const p of P.pilares) if (p.semBase && P.sapatas.length) out.push({ level: 'warn', msg: `${p.name}: sem topo de fundação — altura contada até o fundo da viga baldrame.` });
    return out;
  };
  function br(v, d) { return (v == null || isNaN(v)) ? '—' : v.toLocaleString('pt-BR', { minimumFractionDigits: d, maximumFractionDigits: d }); }
  QD.br = br;

export default QD;
