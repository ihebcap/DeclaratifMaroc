import { useEffect, useState } from 'react';
import { AlertTriangle, Loader2, X } from 'lucide-react';
import { formatDate } from './utils';
import {
  getDateMiseEnRouteDdp, setDateMiseEnRouteDdp,
} from './api';

// ─── TASK-134 — Paramétrage « date de mise en route » (TASK-128, critère revu TASK-220) ─────────
//
// POURQUOI CET ÉCRAN EXISTE (ce n'est pas un ajout de confort) : la date de mise en route détermine
// quelles factures sont exclues du contrôle DDP (DoDate < DateMiseEnRouteSociete, TASK-220). Tant
// que cette date n'est pas configurée, aucune exclusion n'est appliquée (position la plus sûre :
// calcul automatique actif pour toutes les échéances).
//
// Choix retenu (documenté en VERIFY) : une MODALE dédiée réutilisée par les deux écrans DDP (fiche
// déclaration + écran de contrôle) plutôt qu'un écran de paramétrage à part entière — le branchement
// d'un écran au menu est explicitement TASK-136, hors périmètre ici. Les DEUX endpoints consommés
// existaient déjà (TASK-128, aucun front ne les appelait) : rien n'est ajouté côté back.

export function MiseEnRouteDelaiPaiementModal({ societeId, onClose, onSaved }: {
  societeId: number,
  onClose: () => void,
  onSaved: () => void,
}) {
  const [dateActuelle, setDateActuelle] = useState<string | null>(null);
  const [date, setDate] = useState('');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [erreur, setErreur] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      setLoading(true);
      try {
        const res = await getDateMiseEnRouteDdp(societeId);
        setDateActuelle(res.dateMiseEnRoute);
        if (res.dateMiseEnRoute) setDate(res.dateMiseEnRoute.slice(0, 10));
      } catch (e: any) {
        setErreur(e?.response?.data?.Message || 'Impossible de lire le paramétrage de mise en route.');
      } finally {
        setLoading(false);
      }
    })();
  }, [societeId]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErreur(null);
    if (!date) { setErreur('La date de mise en route est obligatoire.'); return; }

    setSubmitting(true);
    try {
      await setDateMiseEnRouteDdp(societeId, date);
      onSaved();
    } catch (err: any) {
      setErreur(err?.response?.data?.Message || err?.response?.data?.message || 'Erreur lors de l\'enregistrement.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ModalShell titre="Date de mise en route — Délai de Paiement" onClose={onClose} maxWidth="520px">
      <form onSubmit={handleSubmit} style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        {erreur && <BandeauErreur message={erreur} />}

        <div style={{ fontSize: '0.82rem', color: 'var(--text-secondary)', lineHeight: 1.45 }}>
          Toute facture dont la <strong>date de facture</strong> est antérieure à cette date est
          exclue du contrôle DDP (non affichée, non intégrable), même si son échéance légale tombe
          après. Tant que cette date n'est pas configurée, aucune exclusion n'est appliquée.
        </div>

        {loading ? (
          <Loader2 size={18} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />
        ) : (
          <>
            <div style={{ fontSize: '0.82rem' }}>
              Valeur actuelle : {dateActuelle
                ? <strong>{formatDate(dateActuelle)}</strong>
                : <span style={{ color: 'var(--status-blocking-text)', fontWeight: 600 }}>non configurée</span>}
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
              <label style={{ fontSize: '0.8rem', fontWeight: 500 }}>Date de mise en route</label>
              <input type="date" value={date} onChange={e => setDate(e.target.value)} className="form-input" required />
            </div>
          </>
        )}

        <BoutonsModale onClose={onClose} submitting={submitting} libelle="Enregistrer" />
      </form>
    </ModalShell>
  );
}

// ─── Petits éléments partagés (mêmes styles que ConventionsDelaiPaiementPanel, TASK-130) ────────

export function ModalShell({ titre, onClose, maxWidth, children }: {
  titre: string,
  onClose: () => void,
  maxWidth: string,
  children: React.ReactNode,
}) {
  return (
    <div style={{ position: 'fixed', inset: 0, zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(0,0,0,0.5)' }}>
      <div style={{ background: 'white', borderRadius: '8px', width: '100%', maxWidth, maxHeight: '90vh', display: 'flex', flexDirection: 'column', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1), 0 10px 10px -5px rgba(0,0,0,0.04)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '1rem 1.5rem', borderBottom: '1px solid var(--border-color)', flexShrink: 0 }}>
          <h3 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>{titre}</h3>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}><X size={20} /></button>
        </div>
        <div style={{ overflowY: 'auto', flex: 1, display: 'flex', flexDirection: 'column' }}>
          {children}
        </div>
      </div>
    </div>
  );
}

export function BandeauErreur({ message }: { message: string }) {
  return (
    <div style={{ padding: '0.75rem 1rem', background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)', borderRadius: '4px', fontSize: '0.85rem', display: 'flex', gap: '0.5rem' }}>
      <AlertTriangle size={16} style={{ flexShrink: 0, marginTop: '1px' }} />
      <span style={{ whiteSpace: 'pre-wrap' }}>{message}</span>
    </div>
  );
}

export function BoutonsModale({ onClose, submitting, libelle }: { onClose: () => void, submitting: boolean, libelle: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem', marginTop: '0.5rem' }}>
      <button type="button" onClick={onClose} className="btn" style={{ background: 'transparent', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px' }}>
        Annuler
      </button>
      <button type="submit" disabled={submitting} className="btn btn-primary" style={{ background: 'var(--accent-primary)', color: 'white', border: 'none', padding: '0.5rem 1.5rem', borderRadius: '4px', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
        {submitting && <Loader2 size={16} className="animate-spin" />}
        {libelle}
      </button>
    </div>
  );
}
