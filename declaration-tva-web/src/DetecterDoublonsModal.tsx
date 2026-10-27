import { useEffect, useState, useMemo } from 'react';
import { X, AlertTriangle, CheckCircle2, Copy } from 'lucide-react';
import { getDiagnosticDoublons, type DiagnosticDoublonsResultat, type GroupeDoublonDto } from './api';
import { ApbsGrid } from './grid/ApbsGrid';
import type { ColDef } from 'ag-grid-community';

interface DetecterDoublonsModalProps {
  declarationId: string;
  declarationNumero?: string;
  onClose: () => void;
}

export function DetecterDoublonsModal({
  declarationId,
  declarationNumero,
  onClose,
}: DetecterDoublonsModalProps) {
  const [data, setData] = useState<DiagnosticDoublonsResultat | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    setLoading(true);
    setError(null);
    getDiagnosticDoublons(declarationId)
      .then((res) => {
        if (alive) setData(res);
      })
      .catch((e: any) => {
        if (alive) {
          setError(e?.response?.data?.Message || e?.response?.data?.message || 'Erreur lors de la détection des doublons.');
        }
      })
      .finally(() => {
        if (alive) setLoading(false);
      });
    return () => {
      alive = false;
    };
  }, [declarationId]);

  const columnDefs = useMemo<ColDef<GroupeDoublonDto>[]>(() => [
    {
      headerName: 'N° Facture',
      field: 'numeroFacture',
      width: 130,
      pinned: 'left',
    },
    {
      headerName: 'Tiers',
      field: 'tiersNom',
      width: 180,
    },
    {
      headerName: 'N° Règlement',
      field: 'numeroRapprochement',
      width: 130,
    },
    {
      headerName: 'Domaine',
      field: 'domaine',
      width: 120,
    },
    {
      headerName: 'Taux',
      field: 'taux',
      width: 90,
      type: 'rightAligned',
      valueFormatter: (p) => (p.value !== undefined && p.value !== null ? `${p.value} %` : '—'),
    },
    {
      headerName: 'Nb lignes',
      field: 'nombreLignes',
      width: 95,
      type: 'rightAligned',
      cellStyle: { fontWeight: 700, color: 'var(--status-blocking-text, #b91c1c)' },
    },
    {
      headerName: 'Montant affecté',
      field: 'montantAffecte',
      width: 130,
      type: 'rightAligned',
      valueFormatter: (p) => (p.value !== undefined && p.value !== null ? p.value.toLocaleString('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—'),
    },
    {
      headerName: 'TVA cumulée',
      field: 'tvaCumulee',
      width: 130,
      type: 'rightAligned',
      valueFormatter: (p) => (p.value !== undefined && p.value !== null ? p.value.toLocaleString('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—'),
    },
    {
      headerName: 'TVA facture',
      field: 'tvaFacture',
      width: 130,
      type: 'rightAligned',
      valueFormatter: (p) => (p.value !== undefined && p.value !== null && p.value > 0 ? p.value.toLocaleString('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—'),
    },
    {
      headerName: 'TVA en trop',
      field: 'surdeclarationGroupe',
      width: 130,
      type: 'rightAligned',
      cellStyle: { fontWeight: 700, color: 'var(--status-blocking-text, #b91c1c)' },
      valueFormatter: (p) => (p.value !== undefined && p.value !== null ? p.value.toLocaleString('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—'),
    },
  ], []);

  return (
    <div
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        width: '100%',
        height: '100%',
        background: 'rgba(0,0,0,0.5)',
        zIndex: 1100,
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        padding: '1.5rem',
      }}
      onClick={onClose}
    >
      <div
        style={{
          width: '980px',
          maxWidth: '100%',
          maxHeight: '90vh',
          background: 'white',
          borderRadius: 'var(--radius-lg, 10px)',
          boxShadow: 'var(--shadow-lg, 0 10px 25px rgba(0,0,0,0.2))',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div
          style={{
            padding: '1rem 1.25rem',
            borderBottom: '1px solid var(--border-color)',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            background: 'var(--bg-secondary)',
          }}
        >
          <div>
            <h3 style={{ margin: 0, fontSize: '1.15rem', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <Copy size={18} /> Détection des doublons — {declarationNumero || data?.numeroDeclaration || 'Déclaration'}
            </h3>
            <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginTop: '0.2rem' }}>
              Analyse en lecture seule des groupes (facture, règlement, taux) présents plusieurs fois dans la déclaration
            </div>
          </div>
          <button
            onClick={onClose}
            aria-label="Fermer"
            style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}
          >
            <X size={22} />
          </button>
        </div>

        {/* Content */}
        <div style={{ flex: 1, padding: '1.25rem', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {loading && (
            <div style={{ color: 'var(--text-secondary)', padding: '2rem', textAlign: 'center' }}>
              Analyse des lignes en cours…
            </div>
          )}

          {error && (
            <div
              style={{
                padding: '0.85rem 1rem',
                borderRadius: 'var(--radius-md, 6px)',
                background: 'var(--status-blocking-bg, #fef2f2)',
                color: 'var(--status-blocking-text, #b91c1c)',
                border: '1px solid var(--status-blocking-border, #fca5a5)',
                fontSize: '0.85rem',
              }}
            >
              {error}
            </div>
          )}

          {!loading && !error && data && (
            <>
              {data.totalGroupesEnDoublon === 0 ? (
                <div
                  style={{
                    padding: '1.25rem',
                    borderRadius: 'var(--radius-md, 8px)',
                    background: 'var(--status-ok-bg, #f0fdf4)',
                    color: 'var(--status-ok-text, #15803d)',
                    border: '1px solid var(--status-ok-border, #bbf7d0)',
                    display: 'flex',
                    alignItems: 'flex-start',
                    gap: '0.75rem',
                  }}
                >
                  <CheckCircle2 size={24} style={{ flexShrink: 0, marginTop: '2px' }} />
                  <div>
                    <div style={{ fontWeight: 700, fontSize: '0.95rem' }}>
                      Aucun doublon détecté dans cette déclaration
                    </div>
                    <div style={{ fontSize: '0.85rem', marginTop: '0.25rem', opacity: 0.9 }}>
                      Les {data.totalLignes} ligne(s) figées de la déclaration ne comportent aucun groupe (facture, règlement, taux) présent plus d'une fois.
                    </div>
                  </div>
                </div>
              ) : (
                <>
                  <div
                    style={{
                      padding: '1rem 1.25rem',
                      borderRadius: 'var(--radius-md, 8px)',
                      background: 'var(--status-blocking-bg, #fef2f2)',
                      color: 'var(--status-blocking-text, #b91c1c)',
                      border: '1px solid var(--status-blocking-border, #fca5a5)',
                      display: 'flex',
                      alignItems: 'flex-start',
                      gap: '0.75rem',
                    }}
                  >
                    <AlertTriangle size={24} style={{ flexShrink: 0, marginTop: '2px' }} />
                    <div style={{ flex: 1 }}>
                      <div style={{ fontWeight: 700, fontSize: '0.95rem' }}>
                        {data.totalGroupesEnDoublon} groupe(s) en doublon détecté(s) — {data.totalLignesEnTrop} ligne(s) excédentaire(s)
                      </div>
                      <div style={{ fontSize: '0.85rem', marginTop: '0.35rem' }}>
                        Surdéclaration fiscale totale :{' '}
                        <strong>{data.surdeclarationTva.toLocaleString('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} MAD</strong>.
                      </div>
                      <div style={{ fontSize: '0.78rem', marginTop: '0.35rem', opacity: 0.85 }}>
                        Outil en lecture seule : aucune donnée n'a été modifiée. Cette capture sert de preuve pour l'arbitrage du chef de projet / comptable.
                      </div>
                    </div>
                  </div>

                  <div style={{ height: '320px', width: '100%' }}>
                    <ApbsGrid<GroupeDoublonDto>
                      rowData={data.groupes}
                      columnDefs={columnDefs}
                      storageKey="grf.declarations.diagnostic_doublons"
                      showExportButton={true}
                      exportFileName={`doublons_${declarationNumero || 'declaration'}.xlsx`}
                      height="320px"
                    />
                  </div>
                </>
              )}
            </>
          )}
        </div>

        {/* Footer */}
        <div
          style={{
            padding: '0.75rem 1.25rem',
            borderTop: '1px solid var(--border-color)',
            display: 'flex',
            justifyContent: 'flex-end',
            background: 'var(--bg-secondary)',
          }}
        >
          <button
            onClick={onClose}
            style={{
              padding: '0.45rem 1rem',
              borderRadius: 'var(--radius-md, 6px)',
              border: '1px solid var(--border-color)',
              background: 'white',
              color: 'var(--text-primary)',
              fontWeight: 600,
              fontSize: '0.8125rem',
              cursor: 'pointer',
            }}
          >
            Fermer
          </button>
        </div>
      </div>
    </div>
  );
}
