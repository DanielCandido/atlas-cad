'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';

type Pairing = {
  userCode: string;
  status: string;
  machineFingerprint: string;
  detectedVersions: string[];
  expiresAt: string;
  hasActiveMachine: boolean;
  activeMachineFingerprint?: string;
};

export function PairingClient({ code }: { code: string }) {
  const router = useRouter();
  const [pairing, setPairing] = useState<Pairing | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [decision, setDecision] = useState<'approved' | 'rejected' | null>(null);

  useEffect(() => {
    if (!code) {
      setError('Código de vinculação ausente. Volte ao instalador e tente novamente.');
      return;
    }
    void (async () => {
      const response = await fetch(`/api/portal/pairings/${encodeURIComponent(code)}`);
      if (response.status === 401) {
        router.replace(`/entrar?returnTo=${encodeURIComponent(`/vincular?codigo=${code}`)}`);
        return;
      }
      const result = await response.json();
      if (!response.ok) setError(result.message || 'Não foi possível carregar a vinculação.');
      else if (result.status === 'expired')
        setError('Esta vinculação expirou. Volte ao instalador e inicie novamente.');
      else if (result.status === 'rejected') setDecision('rejected');
      else setPairing(result);
    })();
  }, [code, router]);

  async function decide(approve: boolean) {
    if (!pairing) return;
    setBusy(true);
    setError('');
    const response = await fetch(`/api/portal/pairings/${encodeURIComponent(code)}`, {
      method: approve ? 'POST' : 'DELETE',
      headers: approve ? { 'content-type': 'application/json' } : undefined,
      body: approve ? JSON.stringify({ replaceExisting: pairing.hasActiveMachine }) : undefined,
    });
    setBusy(false);
    if (!response.ok) {
      const result = await response.json();
      setError(result.message || 'Não foi possível concluir a vinculação.');
      return;
    }
    setDecision(approve ? 'approved' : 'rejected');
  }

  return (
    <section className="section page-top">
      <div className="container narrow">
        <span className="eyebrow">INSTALAÇÃO SEGURA</span>
        <h1 className="page-title">
          Vincular <span>computador.</span>
        </h1>
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        {decision ? (
          <article className="account-card pairing-card">
            <h2>{decision === 'approved' ? 'Vinculação confirmada' : 'Vinculação recusada'}</h2>
            <p>
              {decision === 'approved'
                ? 'Volte ao instalador. A ativação e o catálogo serão preparados automaticamente.'
                : 'O instalador não poderá ativar esta máquina.'}
            </p>
            <p>
              <small>Esta janela já pode ser fechada.</small>
            </p>
          </article>
        ) : pairing ? (
          <article className="account-card pairing-card">
            <h2>Confirme esta instalação</h2>
            <p>
              Código exibido pelo instalador:{' '}
              <strong className="pairing-code">{pairing.userCode}</strong>
            </p>
            <p>
              Identificador criptográfico: <code>{pairing.machineFingerprint.slice(0, 16)}…</code>
            </p>
            <p>
              AutoCAD detectado: <strong>{pairing.detectedVersions.join(', ')}</strong>
            </p>
            {pairing.hasActiveMachine && (
              <div className="notice">
                <strong>Esta assinatura já está ativa em outro computador.</strong>
                <p>
                  Ao confirmar, a ativação anterior será revogada somente quando o novo instalador
                  concluir.
                </p>
              </div>
            )}
            <p>
              A ATLAS CAD não recebe serial de hardware, usuário do Windows nem a chave privada
              deste computador.
            </p>
            <div className="pairing-actions">
              <button
                className="button button-primary"
                disabled={busy}
                onClick={() => void decide(true)}
              >
                {busy
                  ? 'Confirmando…'
                  : pairing.hasActiveMachine
                    ? 'Substituir e ativar'
                    : 'Confirmar e ativar'}
              </button>
              <button
                className="button button-outline"
                disabled={busy}
                onClick={() => void decide(false)}
              >
                Recusar
              </button>
            </div>
          </article>
        ) : !error ? (
          <p>Carregando solicitação…</p>
        ) : (
          <Link href="/minha-conta">Voltar à Área do Cliente</Link>
        )}
      </div>
    </section>
  );
}
