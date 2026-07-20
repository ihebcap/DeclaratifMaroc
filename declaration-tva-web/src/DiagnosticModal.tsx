import { useEffect, useState } from 'react';
import { X, AlertTriangle, Search, CheckCircle2, XCircle, Info, RefreshCw } from 'lucide-react';
import { getDiagnosticLigne, recalculerLigneDepuisCache, type DiagnosticLigneDto } from './api';

// TASK-144 — Panneau « Diagnostiquer » d'une ligne en anomalie.
//
// Objectif PO : le comptable comprend PAR LUI-MÊME, dans l'app, pourquoi une ligne est en anomalie
// et quoi faire — sans revenir vers un assistant. On affiche 3 blocs SÉPARÉS :
//   1. l'échéance Sage précise en cause (EC_Id / n° pièce / tiers),
//   2. le résultat réel de la lecture OM + sa traduction métier + l'action recommandée,
//   3. le contrôle collision DO_Numero (verdict F_DOCREGL), présenté comme une information
//      INDÉPENDANTE — jamais comme la cause automatique de l'anomalie (cf. cas réel FA2600106).

const card: React.CSSProperties = {
  border: '1px solid var(--border-color)',
  borderRadius: '8px',
  overflow: 'hidden',
  marginBottom: '1rem',
};
const cardHeader: React.CSSProperties = {
  padding: '0.6rem 0.9rem',
  fontSize: '0.82rem',
  fontWeight: 700,
  display: 'flex',
  alignItems: 'center',
  gap: '0.45rem',
  borderBottom: '1px solid var(--border-color)',
  background: 'var(--bg-secondary)',
};
const cardBody: React.CSSProperties = { padding: '0.85rem 0.9rem', fontSize: '0.85rem', lineHeight: 1.55 };
const kvRow: React.CSSProperties = { display: 'flex', gap: '0.5rem', padding: '0.15rem 0' };
const kvKey: React.CSSProperties = { minWidth: '150px', color: 'var(--text-secondary)', fontSize: '0.8rem' };
const kvVal: React.CSSProperties = { fontWeight: 600, fontFamily: 'monospace', fontSize: '0.82rem' };

function KV({ k, v }: { k: string; v: React.ReactNode }) {
  return (
    <div style={kvRow}>
      <span style={kvKey}>{k}</span>
      <span style={kvVal}>{v ?? '—'}</span>
    </div>
  );
}

export function DiagnosticModal({
  declarationId,
  ecId,
  factureNumero,
  onClose,
  onRecalculated,
}: {
  declarationId: string;
  ecId: number;
  factureNumero?: string;
  onClose: () => void;
  /** TASK-147 : appelé après un recalcul réussi (le parent recharge lignes/checkup). */
  onRecalculated?: () => void;
}) {
  const [data, setData] = useState<DiagnosticLigneDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [recalculating, setRecalculating] = useState(false);
  const [recalculError, setRecalculError] = useState<string | null>(null);

  async function handleRecalculer() {
    setRecalculating(true);
    setRecalculError(null);
    try {
      await recalculerLigneDepuisCache(declarationId, ecId);
      onRecalculated?.();
    } catch (e: any) {
      setRecalculError(e?.response?.data?.message || 'Recalcul impossible pour cette ligne.');
    } finally {
      setRecalculating(false);
    }
  }

  useEffect(() => {
    let alive = true;
    setLoading(true);
    setError(null);
    getDiagnosticLigne(declarationId, ecId)
      .then((d) => { if (alive) setData(d); })
      .catch((e) => {
        if (alive) setError(e?.response?.data?.message || 'Diagnostic indisponible pour cette ligne.');
      })
      .finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
  }, [declarationId, ecId]);

  return (
    <div
      style={{
        position: 'fixed', top: 0, left: 0, width: '100%', height: '100%',
        background: 'rgba(0,0,0,0.5)', zIndex: 1100,
        display: 'flex', justifyContent: 'flex-end',
      }}
      onClick={onClose}
    >
      <div
        style={{
          width: '720px', maxWidth: '100%', height: '100%', background: 'white',
          boxShadow: '-4px 0 15px rgba(0,0,0,0.1)', display: 'flex', flexDirection: 'column',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <h3 style={{ margin: 0, fontSize: '1.2rem', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <Search size={19} /> Diagnostic de l'anomalie
            </h3>
            <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', marginTop: '0.2rem' }}>
              Facture <span style={{ fontFamily: 'monospace' }}>{factureNumero || data?.doNumero || '—'}</span>
              {' · '}échéance EC_Id <span style={{ fontFamily: 'monospace' }}>{ecId}</span>
            </div>
          </div>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer' }}><X size={24} /></button>
        </div>

        <div style={{ flex: 1, padding: '1.25rem 1.5rem', overflowY: 'auto' }}>
          {loading && <div style={{ color: 'var(--text-secondary)' }}>Analyse en cours…</div>}
          {error && (
            <div style={{ ...card, borderColor: 'var(--status-blocking-border)' }}>
              <div style={{ ...cardBody, color: 'var(--status-blocking-text)' }}>{error}</div>
            </div>
          )}

          {data && !loading && (
            <>
              {/* Bloc 1 — identité de l'échéance Sage en cause */}
              <div style={card}>
                <div style={cardHeader}><Info size={15} /> 1 · Échéance Sage concernée</div>
                <div style={cardBody}>
                  <KV k="Numéro de pièce" v={data.doNumero} />
                  <KV k="Tiers (RT_ECHEANCE)" v={`${data.tiersCode} — ${data.tiersIntitule}`} />
                  <KV k="EC_Id / EC_No" v={`${data.ecId} / ${data.ecNo}`} />
                  <KV k="Origine" v={data.origine} />
                  <KV k="Montant échéance (devise)" v={data.montantDevise != null ? data.montantDevise.toLocaleString('fr-FR', { minimumFractionDigits: 2 }) : '—'} />
                </div>
              </div>

              {/* Bloc 2 — résultat de la lecture OM + traduction métier + action */}
              <div style={{ ...card, borderColor: 'var(--status-warning-border)' }}>
                <div style={{ ...cardHeader, background: 'var(--status-warning-bg)', color: 'var(--status-warning-text)', borderColor: 'var(--status-warning-border)' }}>
                  <AlertTriangle size={15} /> 2 · Pourquoi cette ligne n'est pas valorisée
                </div>
                <div style={cardBody}>
                  <p style={{ margin: '0 0 0.6rem 0' }}>{data.explicationMetier}</p>
                  {data.actionRecommandee && (
                    <div style={{ background: 'var(--bg-secondary)', border: '1px solid var(--border-color)', borderRadius: '6px', padding: '0.6rem 0.7rem', margin: '0.4rem 0' }}>
                      <div style={{ fontSize: '0.75rem', fontWeight: 700, color: 'var(--text-secondary)', marginBottom: '0.25rem', textTransform: 'uppercase', letterSpacing: '0.02em' }}>Action recommandée</div>
                      <div>{data.actionRecommandee}</div>
                    </div>
                  )}
                  <details style={{ marginTop: '0.5rem' }}>
                    <summary style={{ cursor: 'pointer', fontSize: '0.78rem', color: 'var(--text-secondary)' }}>Détail technique</summary>
                    <div style={{ marginTop: '0.35rem', fontSize: '0.78rem', fontFamily: 'monospace', color: 'var(--text-secondary)', whiteSpace: 'pre-wrap' }}>
                      <div>Motif : {data.motifTechnique || '—'}</div>
                      {data.motifErreurCache && <div>Message Sage (cache) : {data.motifErreurCache}</div>}
                      <div>Code : {data.codeMotifReconnu}</div>
                    </div>
                  </details>
                </div>
              </div>

              {/* Bloc 4 (TASK-147) — cache PÉRIMÉ : Sage a relu la pièce avec succès APRÈS la
                  création de cette déclaration. INDÉPENDANT du bloc 2 (motif figé à la création). */}
              {data.cachePerime && (
                <div style={{ ...card, borderColor: 'var(--status-ok-border, #86efac)' }}>
                  <div style={{ ...cardHeader, background: 'var(--status-ok-bg, #f0fdf4)', color: 'var(--status-ok-text, #15803d)', borderColor: 'var(--status-ok-border, #86efac)' }}>
                    <RefreshCw size={15} /> 4 · Cache Sage périmé — une donnée fraîche est disponible
                  </div>
                  <div style={cardBody}>
                    <p style={{ margin: '0 0 0.6rem 0' }}>{data.cachePerimeCommentaire}</p>
                    <button
                      onClick={handleRecalculer}
                      disabled={recalculating}
                      style={{
                        display: 'inline-flex', alignItems: 'center', gap: '0.4rem',
                        padding: '0.45rem 0.8rem', borderRadius: '6px', border: '1px solid var(--status-ok-border, #86efac)',
                        background: recalculating ? 'var(--bg-secondary)' : 'var(--status-ok-bg, #f0fdf4)',
                        color: 'var(--status-ok-text, #15803d)', fontWeight: 600, fontSize: '0.82rem',
                        cursor: recalculating ? 'default' : 'pointer',
                      }}
                    >
                      <RefreshCw size={14} /> {recalculating ? 'Recalcul en cours…' : 'Recalculer cette ligne'}
                    </button>
                    {recalculError && (
                      <div style={{ marginTop: '0.5rem', fontSize: '0.8rem', color: 'var(--status-blocking-text, #b91c1c)' }}>{recalculError}</div>
                    )}
                  </div>
                </div>
              )}

              {/* Bloc 3 — contrôle collision DO_Numero (indépendant) */}
              {data.collisionDetectee ? (
                <div style={card}>
                  <div style={cardHeader}><AlertTriangle size={15} /> 3 · Contrôle « numéro de pièce partagé entre tiers »</div>
                  <div style={cardBody}>
                    <p style={{ margin: '0 0 0.6rem 0', color: 'var(--text-secondary)', fontSize: '0.82rem' }}>{data.collisionCommentaire}</p>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8rem' }}>
                      <thead>
                        <tr style={{ background: 'var(--bg-secondary)' }}>
                          <th style={{ textAlign: 'left', padding: '0.35rem 0.5rem' }}>Tiers</th>
                          <th style={{ textAlign: 'left', padding: '0.35rem 0.5rem' }}>EC_Id</th>
                          <th style={{ textAlign: 'left', padding: '0.35rem 0.5rem' }}>Document Sage</th>
                        </tr>
                      </thead>
                      <tbody>
                        {data.collisions.map((c) => (
                          <tr key={c.ecId} style={{ borderTop: '1px solid var(--border-color)', background: c.estLigneConsultee ? 'var(--bg-secondary)' : undefined }}>
                            <td style={{ padding: '0.35rem 0.5rem' }}>
                              {c.estLigneConsultee && <span style={{ fontSize: '0.68rem', fontWeight: 700, color: 'var(--accent-primary)', marginRight: '0.3rem' }}>▶</span>}
                              {c.tiersCode} — {c.tiersIntitule}
                            </td>
                            <td style={{ padding: '0.35rem 0.5rem', fontFamily: 'monospace' }}>{c.ecId}</td>
                            <td style={{ padding: '0.35rem 0.5rem' }}>
                              {c.aDocumentSage ? (
                                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem', color: 'var(--status-ok-text, #15803d)' }}>
                                  <CheckCircle2 size={14} /> {c.doPieceSage || 'oui'}
                                  {c.dateDocSage && <span style={{ color: 'var(--text-secondary)', fontSize: '0.72rem' }}>({new Date(c.dateDocSage).toLocaleDateString('fr-FR')})</span>}
                                </span>
                              ) : (
                                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem', color: 'var(--status-blocking-text, #b91c1c)' }}>
                                  <XCircle size={14} /> orpheline
                                </span>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ) : (
                <div style={card}>
                  <div style={cardHeader}><CheckCircle2 size={15} /> 3 · Contrôle « numéro de pièce partagé entre tiers »</div>
                  <div style={{ ...cardBody, color: 'var(--text-secondary)' }}>
                    Aucune collision : ce numéro de pièce n'est porté que par ce tiers. Ce contrôle n'explique donc pas l'anomalie.
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
