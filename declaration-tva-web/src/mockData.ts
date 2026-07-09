export interface LigneDeclarationEnrichie {
  id: string;
  factureNumero: string;
  designation: string;
  tiers: string;
  identifiantFiscal: string;
  ice: string;
  montantHT: number;
  tauxTVA: number;
  montantTVA: number;
  montantTTC: number;
  modePaiement: string;
  datePaiement: string;
  dateFacture: string;
  source: 'VENTE' | 'ACHAT' | 'CAISSE';
}

export interface DeclarationModele {
  lignes: LigneDeclarationEnrichie[];
  recapSource: { source: string; ht: number; tva: number; ttc: number }[];
  recapTaux: { taux: number; ht: number; tva: number; ttc: number }[];
  alertes: { type: 'bloquant' | 'avertissement'; message: string; ligneId?: string }[];
}

export const mockDeclarationData: DeclarationModele = {
  lignes: [
    { id: '1', factureNumero: 'FAC-2023-001', designation: 'Prestation de service', tiers: 'Client A', identifiantFiscal: '12345678', ice: '001122334455667', montantHT: 10000, tauxTVA: 20, montantTVA: 2000, montantTTC: 12000, modePaiement: 'Virement', datePaiement: '2023-10-15', dateFacture: '2023-10-01', source: 'VENTE' },
    { id: '2', factureNumero: 'FAC-2023-002', designation: 'Achat matériel', tiers: 'Fournisseur B', identifiantFiscal: '87654321', ice: '998877665544332', montantHT: 5000, tauxTVA: 20, montantTVA: 1000, montantTTC: 6000, modePaiement: 'Chèque', datePaiement: '2023-10-20', dateFacture: '2023-10-05', source: 'ACHAT' },
    { id: '3', factureNumero: 'TICKET-001', designation: 'Fournitures bureau', tiers: 'Supermarché', identifiantFiscal: '', ice: '', montantHT: 500, tauxTVA: 20, montantTVA: 100, montantTTC: 600, modePaiement: 'Espèce', datePaiement: '2023-10-25', dateFacture: '2023-10-25', source: 'CAISSE' },
    { id: '4', factureNumero: 'FAC-2023-003', designation: 'Exportation', tiers: 'Client Etranger', identifiantFiscal: 'INT-001', ice: '', montantHT: 50000, tauxTVA: 0, montantTVA: 0, montantTTC: 50000, modePaiement: 'Virement', datePaiement: '2023-10-28', dateFacture: '2023-10-10', source: 'VENTE' }
  ],
  recapSource: [
    { source: 'VENTE', ht: 60000, tva: 2000, ttc: 62000 },
    { source: 'ACHAT', ht: 5000, tva: 1000, ttc: 6000 },
    { source: 'CAISSE', ht: 500, tva: 100, ttc: 600 }
  ],
  recapTaux: [
    { taux: 20, ht: 15500, tva: 3100, ttc: 18600 },
    { taux: 0, ht: 50000, tva: 0, ttc: 50000 }
  ],
  alertes: [
    { type: 'avertissement', message: 'Tiers Supermarché: ICE manquant', ligneId: '3' },
    { type: 'avertissement', message: 'Tiers Client Etranger: ICE manquant', ligneId: '4' }
  ]
};
