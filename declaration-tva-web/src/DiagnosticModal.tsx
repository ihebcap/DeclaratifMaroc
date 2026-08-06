import { useEffect, useState } from 'react';
import { X, AlertTriangle, Search, CheckCircle2, XCircle, Info, RefreshCw, Calculator } from 'lucide-react';
import { getDiagnosticLigne, recalculerLigneDepuisCache, relireDepuisSage, enregistrerSaisieSoldeInitial, type DiagnosticLigneDto } from './api';

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
  // TASK-167 : relecture Sage RÉELLE (distincte du recalcul depuis cache ci-dessus) — action
  // manuelle explicite, jamais automatique (cf. recommandation architecte de la task : une
  // relecture Sage ouvre une session OM à chaque appel, un retry automatique en masse
  // réintroduirait la contention déjà corrigée par TASK-156).
  const [relisant, setRelisant] = useState(false);
  const [relireError, setRelireError] = useState<string | null>(null);
  const [relireResultat, setRelireResultat] = useState<string | null>(null);

  // TASK-025 (solde initial, EC_Type=4 — décision PO) : saisie manuelle taux+montant TVA.
  const [taux, setTaux] = useState<number | ''>('');
  const [montantTva, setMontantTva] = useState<number | ''>('');
  const [saisieEnCours, setSaisieEnCours] = useState(false);
  const [saisieError, setSaisieError] = useState<string | null>(null);
  const [saisieResultat, setSaisieResultat] = useState<string | null>(null);

  async function handleSaisieSoldeInitial() {
    if (montantTva === '' || Number(montantTva) < 0) { setSaisieError('Le montant de TVA est obligatoire (≥ 0).'); return; }
    setSaisieEnCours(true);
    setSaisieError(null);
    setSaisieResultat(null);
    try {
      const { resolue } = await enregistrerSaisieSoldeInitial(declarationId, ecId, Number(taux || 0), Number(montantTva));
      if (resolue) {
        onRecalculated?.();
      } else {
        setSaisieResultat('Saisie enregistrée, mais la ligne reste en anomalie (motif inchangé, voir ci-dessus).');
      }
    } catch (e: any) {
      setSaisieError(e?.response?.data?.Message || e?.response?.data?.message || "Échec de l'enregistrement de la saisie.");
    } finally {
      setSaisieEnCours(false);
    }
  }

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

  async function handleRelireSage() {
    setRelisant(true);
    setRelireError(null);
    setRelireResultat(null);
    try {
      const { resolue } = await relireDepuisSage(declarationId, ecId);
      if (resolue) {
        onRecalculated?.();
      } else {
        // TASK-167 §3 (garde-fou) : le motif réel reste affiché, bloquant — jamais de montant
        // fabriqué. On informe simplement que la relecture a eu lieu mais n'a rien résolu.
        setRelireResultat('Relecture Sage effectuée — la pièce est toujours en anomalie (motif réel inchangé, voir ci-dessus).');
      }
    } catch (e: any) {
      // TASK-156 : un rejet 409 signifie qu'un autre traitement OM est déjà en cours pour cette
      // société (verrou soId partagé) — message serveur explicite, jamais une file d'attente
      // silencieuse.
      setRelireError(e?.response?.data?.Message || e?.response?.data?.message || 'Échec de la relecture Sage.');
    } finally {
      setRelisant(false);
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

              {/* Bloc (TASK-167) — relecture Sage RÉELLE, action manuelle explicite. Distinct du
                  bloc cache périmé ci-dessous (TASK-147, qui ne relit jamais Sage) : ici, quand le
                  cache n'est pas simplement périmé mais absent/en erreur (le cas le plus fréquent,
                  ex. FC2502094 « Facture introuvable »), aucune autre action n'existait jusqu'ici
                  dans cet écran pour relancer une vraie lecture Sage sur cette seule pièce. */}
              {data.codeMotifReconnu === 'SOLDE_INITIAL_SAISIE_REQUISE' ? (
                // TASK-025 : ce motif ne se résout jamais par une relecture Sage (aucun détail
                // HT/TVA/taux n'existe côté Sage pour un solde initial) — remplace le bloc
                // « Relire depuis Sage » par la saisie manuelle attendue par le comptable.
                <div style={{ ...card, borderColor: 'var(--accent-primary)' }}>
                  <div style={cardHeader}><Calculator size={15} /> Solde initial — saisie du taux et du montant de TVA</div>
                  <div style={cardBody}>
                    <p style={{ margin: '0 0 0.6rem 0', color: 'var(--text-secondary)', fontSize: '0.82rem' }}>
                      Le solde n'a aucun détail de TVA côté Sage (montant connu en TTC seul, {data.montantDevise != null ? data.montantDevise.toLocaleString('fr-FR', { minimumFractionDigits: 2 }) : '—'} MAD).
                      Saisissez le taux et le montant de TVA pour l'intégrer à la déclaration (HT = TTC − TVA).
                    </p>
                    <div style={{ display: 'flex', gap: '1rem', marginBottom: '0.75rem' }}>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.35rem', flex: 1 }}>
                        <label style={{ fontSize: '0.78rem', fontWeight: 500 }}>Taux de TVA (%)</label>
                        <input type="number" step="0.01" min={0} className="form-input" value={taux} onChange={e => setTaux(e.target.value === '' ? '' : Number(e.target.value))} />
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.35rem', flex: 1 }}>
                        <label style={{ fontSize: '0.78rem', fontWeight: 500 }}>Montant de TVA</label>
                        <input type="number" step="0.01" min={0} className="form-input" value={montantTva} onChange={e => setMontantTva(e.target.value === '' ? '' : Number(e.target.value))} />
                      </div>
                    </div>
                    <button
                      onClick={handleSaisieSoldeInitial}
                      disabled={saisieEnCours}
                      style={{
                        display: 'inline-flex', alignItems: 'center', gap: '0.4rem',
                        padding: '0.45rem 0.8rem', borderRadius: '6px', border: '1px solid var(--accent-primary)',
                        background: saisieEnCours ? 'var(--bg-secondary)' : 'var(--accent-primary)',
                        color: saisieEnCours ? 'var(--text-primary)' : 'white', fontWeight: 600, fontSize: '0.82rem',
                        cursor: saisieEnCours ? 'default' : 'pointer',
                      }}
                    >
                      <Calculator size={14} />
                      {saisieEnCours ? 'Enregistrement…' : 'Enregistrer et intégrer'}
                    </button>
                    {saisieResultat && (
                      <div style={{ marginTop: '0.5rem', fontSize: '0.8rem', color: 'var(--status-warning-text-alt)' }}>{saisieResultat}</div>
                    )}
                    {saisieError && (
                      <div style={{ marginTop: '0.5rem', fontSize: '0.8rem', color: 'var(--status-blocking-text, #b91c1c)' }}>{saisieError}</div>
                    )}
                  </div>
                </div>
              ) : !data.cachePerime && (
                <div style={{ ...card, borderColor: 'var(--border-color)' }}>
                  <div style={cardHeader}><RefreshCw size={15} /> Relancer une lecture Sage réelle</div>
                  <div style={cardBody}>
                    <p style={{ margin: '0 0 0.6rem 0', color: 'var(--text-secondary)', fontSize: '0.82rem' }}>
                      Si la facture existe bien sur Sage (ex. après correction côté ERP, ou pour
                      écarter un aléa de lecture ponctuel), cette action relit directement l'objet
                      métier Sage pour cette seule pièce et réécrit le cache de valorisation. Action
                      manuelle explicite — aucune relecture automatique n'est déclenchée ailleurs.
                    </p>
                    <button
                      onClick={handleRelireSage}
                      disabled={relisant}
                      style={{
                        display: 'inline-flex', alignItems: 'center', gap: '0.4rem',
                        padding: '0.45rem 0.8rem', borderRadius: '6px', border: '1px solid var(--border-color)',
                        background: relisant ? 'var(--bg-secondary)' : 'white',
                        color: 'var(--text-primary)', fontWeight: 600, fontSize: '0.82rem',
                        cursor: relisant ? 'default' : 'pointer',
                      }}
                    >
                      <RefreshCw size={14} className={relisant ? 'animate-spin' : undefined} />
                      {relisant ? 'Relecture Sage en cours…' : 'Relire depuis Sage'}
                    </button>
                    {relireResultat && (
                      <div style={{ marginTop: '0.5rem', fontSize: '0.8rem', color: 'var(--status-warning-text-alt)' }}>{relireResultat}</div>
                    )}
                    {relireError && (
                      <div style={{ marginTop: '0.5rem', fontSize: '0.8rem', color: 'var(--status-blocking-text, #b91c1c)' }}>{relireError}</div>
                    )}
                  </div>
                </div>
              )}

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
