import engine from '@/public/tools/quantitativo-engine.runtime.js';

export type DxfEngine = {
  parseDXF: (buffer: ArrayBuffer, name: string) => unknown;
  recognizeFile: (model: unknown) => any;
  buildProject: (records: any[], premises?: Record<string, number>) => any;
  compute: (project: any) => any;
  reviewAlerts: (project: any) => any[];
  defaultPrem: () => Record<string, number>;
  TELA: Record<string, number>;
};

export function getDxfEngine(): DxfEngine {
  return engine as DxfEngine;
}
