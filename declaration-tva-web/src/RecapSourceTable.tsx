import React from 'react';
import { formatMoney } from './utils';

// ─── Source labels ─────────────────────────────────────────────────────────────

const SOURCE_LABELS: Record<string, string> = {
    ACHAT: 'Décaissements fournisseurs',
    VENTE: 'Encaissements clients',
    CAISSE: 'Espèces',
    BANQUE: 'Frais bancaires',
    Décaissement: 'Décaissements',
    Encaissement: 'Encaissements',
    Dépense: 'Dépenses',
    'Frais bancaire': 'Frais bancaires',
    // TASK-112 : axe « lignes incohérentes » (TTC ≠ HT+TVA), remplace l'axe Source pour le
    // drill d'isolement de l'écart (Source était une tautologie — cf. VerifierIntegrerPanel).
    incoherente: 'Lignes incohérentes (TTC ≠ HT+TVA)',
};

function sourceLabel(s: string): string {
    return SOURCE_LABELS[s] ?? s;
}

// ─── Helpers table dense ───────────────────────────────────────────────────────

function Th({ children, right }: { children: React.ReactNode; right?: boolean }) {
    return (
        <th style={{
            padding: '0.35rem 0.65rem',
            fontSize: '0.7rem', fontWeight: 700,
            textTransform: 'uppercase', letterSpacing: '0.04em',
            color: 'var(--text-secondary)',
            textAlign: right ? 'right' : 'left',
            background: 'var(--bg-tertiary)',
            borderBottom: '1px solid var(--border-color)',
        }}>
            {children}
        </th>
    );
}

function Td({ children, right }: { children: React.ReactNode; right?: boolean }) {
    return (
        <td style={{
            padding: '0.4rem 0.65rem',
            fontSize: '0.8125rem',
            textAlign: right ? 'right' : 'left',
            color: 'var(--text-primary)',
            whiteSpace: 'nowrap',
        }}>
            {children}
        </td>
    );
}

export interface RecapLigneSource {
    source: string;
    ht: number;
    tva: number;
    ttc: number;
}

export interface RecapSourceTableProps {
    recapSource: RecapLigneSource[];
    /** TASK-107 : clic sur une ligne → drill vers les factures/règlements de cette source (option 3, réutilise TASK-016) */
    onRowClick?: (source: string, label: string) => void;
    /** TASK-112 : libellé de la 1re colonne, générique au-delà de « Source » (ex. axe incohérence) */
    columnLabel?: string;
}

export function RecapSourceTable({ recapSource, onRowClick, columnLabel = 'Source' }: RecapSourceTableProps) {
    const totalSource = recapSource.reduce((s, r) => s + r.tva, 0);
    const totalHT = recapSource.reduce((s, r) => s + r.ht, 0);
    const totalTTC = recapSource.reduce((s, r) => s + r.ttc, 0);

    return (
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
            <thead>
                <tr style={{ background: 'var(--bg-tertiary)' }}>
                    <Th>{columnLabel}</Th>
                    <Th right>HT</Th>
                    <Th right>TVA</Th>
                    <Th right>TTC</Th>
                </tr>
            </thead>
            <tbody>
                {recapSource.map((r) => (
                    <tr
                        key={r.source}
                        onClick={onRowClick ? () => onRowClick(r.source, sourceLabel(r.source)) : undefined}
                        title={onRowClick ? `Voir les factures/règlements de la source « ${sourceLabel(r.source)} »` : undefined}
                        style={{
                            borderBottom: '1px solid var(--border-color)',
                            cursor: onRowClick ? 'pointer' : undefined,
                        }}
                        onMouseEnter={onRowClick ? (e => (e.currentTarget.style.background = 'var(--bg-tertiary)')) : undefined}
                        onMouseLeave={onRowClick ? (e => (e.currentTarget.style.background = '')) : undefined}
                    >
                        <Td>{sourceLabel(r.source)}</Td>
                        <Td right>{formatMoney(r.ht)}</Td>
                        <Td right>{formatMoney(r.tva)}</Td>
                        <Td right>{formatMoney(r.ttc)}</Td>
                    </tr>
                ))}
            </tbody>
            <tfoot>
                <tr style={{ background: 'var(--bg-tertiary)', fontWeight: 700, fontSize: '0.8125rem' }}>
                    <Td>Total</Td>
                    <Td right>{formatMoney(totalHT)}</Td>
                    <Td right>{formatMoney(totalSource)}</Td>
                    <Td right>{formatMoney(totalTTC)}</Td>
                </tr>
            </tfoot>
        </table>
    );
}
