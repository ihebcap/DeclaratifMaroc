export type StatutDeclaration = 'EnCours' | 'Clôturée' | 'Générée';
export type DomaineTVA = 'Décaissement' | 'Encaissement' | 'Dépense' | 'Frais bancaire';
export type StatutLigne = 'Proposée' | 'Intégrée' | 'Exclue' | 'Reportée' | 'Écartée';

export interface LigneDeclaration {
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
  source: string;
  domaine: DomaineTVA;
  statutLigne: StatutLigne;
  motif?: string;
  codeActivite?: string;
}

export interface DeclarationStore {
  id: string;
  numero: string; // TVA{Societe}-{Exercice}-{Periode}
  societeId: number;
  societeName: string;
  exercice: number;
  periode: string; // ex: "06" ou "T2"
  type: 'Mensuel' | 'Trimestriel';
  statut: StatutDeclaration;
  lignes: LigneDeclaration[];
}

export interface CheckupAlert {
  type: 'bloquant' | 'avertissement';
  message: string;
  ligneId?: string;
  domaine?: DomaineTVA;
  filtre?: Record<string, string>;
}

export interface DeclarationAvancement {
  id: string;
  numero: string;
  statut: StatutDeclaration;
  exercice: number;
  periode: string;
  type: string;
  domaines: Record<DomaineTVA, { integrees: number; proposees: number; totalMontantTTC: number }>;
}

class MockServer {
  private declarations: DeclarationStore[] = [];

  constructor() {
    this.seedData();
  }

  private generateLignes(count: number): LigneDeclaration[] {
    const lignes: LigneDeclaration[] = [];
    const domaines: DomaineTVA[] = ['Décaissement', 'Encaissement', 'Dépense', 'Frais bancaire'];
    const sources = ['VENTE', 'ACHAT', 'CAISSE', 'BANQUE'];
    
    const motifsEcartement = [
      "Règlement non rapproché",
      "Rapproché hors de la période",
      "Règlement non comptabilisé",
      "Facture introuvable",
      "Ligne de taxe non éligible (type ≠ taux)"
    ];
    const activites = ['Activite A', 'Activite B', 'Activite C'];

    for (let i = 0; i < count; i++) {
      const domaine = domaines[i % 4];
      const source = sources[i % 4];
      const montantHT = Math.round(Math.random() * 10000);
      const tauxTVA = 20;
      const montantTVA = montantHT * (tauxTVA / 100);
      
      const isEcartee = Math.random() < 0.1; // 10% de lignes écartées
      const motif = isEcartee ? motifsEcartement[Math.floor(Math.random() * motifsEcartement.length)] : undefined;
      const statutLigne = isEcartee ? 'Écartée' : 'Proposée';

      lignes.push({
        id: `LIGNE-${i + 1}`,
        factureNumero: `FAC-2026-${String(i + 1).padStart(4, '0')}`,
        designation: `Prestation / Bien ${i + 1}`,
        tiers: `Tiers ${Math.floor(Math.random() * 50)}`,
        identifiantFiscal: `IF-${Math.floor(Math.random() * 10000)}`,
        ice: `ICE${String(Math.floor(Math.random() * 1000000000000000)).padStart(15, '0')}`,
        montantHT,
        tauxTVA,
        montantTVA,
        montantTTC: montantHT + montantTVA,
        modePaiement: ['Virement', 'Chèque', 'Espèce'][Math.floor(Math.random() * 3)],
        datePaiement: `2026-06-${String(Math.floor(Math.random() * 28) + 1).padStart(2, '0')}`,
        dateFacture: `2026-06-${String(Math.floor(Math.random() * 28) + 1).padStart(2, '0')}`,
        source,
        domaine,
        statutLigne,
        motif,
        codeActivite: activites[Math.floor(Math.random() * activites.length)]
      });
    }
    return lignes;
  }

  private seedData() {
    this.declarations.push({
      id: 'DEC-1',
      numero: 'TVAEMA-2026-05',
      societeId: 1,
      societeName: 'EMA',
      exercice: 2026,
      periode: '05',
      type: 'Mensuel',
      statut: 'Clôturée',
      lignes: this.generateLignes(50).map(l => ({ ...l, statutLigne: 'Intégrée' }))
    });
  }

  public getDeclarations(societeId: number) {
    return this.declarations.filter(d => d.societeId === societeId).map(d => ({
      id: d.id,
      numero: d.numero,
      exercice: d.exercice,
      periode: d.periode,
      type: d.type,
      statut: d.statut
    }));
  }

  public createDeclaration(data: { societeId: number, societeName: string, exercice: number, periode: string, type: 'Mensuel' | 'Trimestriel' }) {
    const numero = `TVA${data.societeName}-${data.exercice}-${data.periode}`;
    if (this.declarations.some(d => d.numero === numero)) {
      throw new Error('Declaration already exists');
    }

    const newDec: DeclarationStore = {
      id: `DEC-${Date.now()}`,
      numero,
      ...data,
      statut: 'EnCours',
      lignes: this.generateLignes(2500) // Generate 2500 lines for volume testing
    };

    this.declarations.push(newDec);
    return { id: newDec.id, numero: newDec.numero, statut: newDec.statut };
  }

  public getDeclarationInfo(id: string): DeclarationAvancement {
    const d = this.declarations.find(x => x.id === id);
    if (!d) throw new Error('Not found');

    const domaines: Record<DomaineTVA, { integrees: number; proposees: number; totalMontantTTC: number }> = {
      'Décaissement': { integrees: 0, proposees: 0, totalMontantTTC: 0 },
      'Encaissement': { integrees: 0, proposees: 0, totalMontantTTC: 0 },
      'Dépense': { integrees: 0, proposees: 0, totalMontantTTC: 0 },
      'Frais bancaire': { integrees: 0, proposees: 0, totalMontantTTC: 0 }
    };

    for (const l of d.lignes) {
      if (l.statutLigne === 'Proposée') domaines[l.domaine].proposees++;
      if (l.statutLigne === 'Intégrée') {
        domaines[l.domaine].integrees++;
        domaines[l.domaine].totalMontantTTC += l.montantTTC;
      }
    }

    return {
      id: d.id,
      numero: d.numero,
      statut: d.statut,
      exercice: d.exercice,
      periode: d.periode,
      type: d.type,
      domaines
    };
  }

  public getLignes(
    id: string, 
    domaine: DomaineTVA, 
    page: number, 
    size: number, 
    sortConfig: { key: string; desc: boolean } | null, 
    filters: Record<string, any>
  ) {
    const d = this.declarations.find(x => x.id === id);
    if (!d) throw new Error('Not found');

    let lines = d.lignes.filter(l => l.domaine === domaine);

    // Apply filters
    for (const [key, filterVal] of Object.entries(filters)) {
      lines = lines.filter(row => {
        const rowVal = (row as any)[key];
        
        if (Array.isArray(filterVal)) {
          if (filterVal.length > 0 && !filterVal.includes(String(rowVal || ''))) return false;
        } else if (typeof filterVal === 'string' && filterVal.includes('~')) {
          const [min, max] = filterVal.split('~');
          if (typeof rowVal === 'number') {
            if (min && rowVal < Number(min)) return false;
            if (max && rowVal > Number(max)) return false;
          } else {
            if (min && (rowVal as string) < min) return false;
            if (max && (rowVal as string) > max) return false;
          }
        } else if (filterVal) {
          if (key === 'id') {
            if (String(rowVal) !== String(filterVal)) return false;
          } else {
            if (!String(rowVal || '').toLowerCase().includes(String(filterVal).toLowerCase())) return false;
          }
        }
        return true;
      });
    }

    // Apply sorting
    if (sortConfig) {
      lines = lines.sort((a, b) => {
        const valA = (a as any)[sortConfig.key];
        const valB = (b as any)[sortConfig.key];
        if (valA < valB) return sortConfig.desc ? 1 : -1;
        if (valA > valB) return sortConfig.desc ? -1 : 1;
        return 0;
      });
    }

    const total = lines.length;
    const paginated = lines.slice((page - 1) * size, page * size);
    
    // Also calculate distincts for the current filtered view for list filters (Taux, Statut, etc)
    const distincts: Record<string, string[]> = {};
    ['tauxTVA', 'modePaiement', 'source', 'statutLigne'].forEach(k => {
      distincts[k] = Array.from(new Set(lines.map(l => String((l as any)[k] || ''))));
    });

    return { data: paginated, total, distincts };
  }

  public updateLigneStatus(id: string, payload: { ligneIds?: string[], filtres?: Record<string, any>, domaine?: DomaineTVA, statutLigne: StatutLigne }) {
    const d = this.declarations.find(x => x.id === id);
    if (!d) throw new Error('Not found');
    
    if (payload.filtres && payload.domaine) {
       // Filter logic identical to getLignes
       let lines = d.lignes.filter(l => l.domaine === payload.domaine);
       for (const [key, filterVal] of Object.entries(payload.filtres)) {
         lines = lines.filter(row => {
           const rowVal = (row as any)[key];
           if (Array.isArray(filterVal)) {
             if (filterVal.length > 0 && !filterVal.includes(String(rowVal || ''))) return false;
           } else if (typeof filterVal === 'string' && filterVal.includes('~')) {
             const [min, max] = filterVal.split('~');
             if (typeof rowVal === 'number') {
               if (min && rowVal < Number(min)) return false;
               if (max && rowVal > Number(max)) return false;
             } else {
               if (min && (rowVal as string) < min) return false;
               if (max && (rowVal as string) > max) return false;
             }
           } else if (filterVal) {
             if (key === 'id') {
               if (String(rowVal) !== String(filterVal)) return false;
             } else {
               if (!String(rowVal || '').toLowerCase().includes(String(filterVal).toLowerCase())) return false;
             }
           }
           return true;
         });
       }
       for (const l of lines) {
         l.statutLigne = payload.statutLigne;
       }
    } else if (payload.ligneIds) {
       const idsSet = new Set(payload.ligneIds);
       for (const l of d.lignes) {
         if (idsSet.has(l.id)) {
           l.statutLigne = payload.statutLigne;
         }
       }
    }
  }

  public getCheckup(id: string) {
    const d = this.declarations.find(x => x.id === id);
    if (!d) throw new Error('Not found');

    const recapSource: { source: string; ht: number; tva: number; ttc: number }[] = [];
    const recapTaux: { taux: number; ht: number; tva: number; ttc: number }[] = [];
    const recapActivite: { activite: string; ht: number; tva: number; ttc: number }[] = [];
    const alertes: CheckupAlert[] = [];

    const reconciliation = {
      candidates: d.lignes.length,
      integrees: 0,
      exclues: 0,
      reportees: 0,
      proposees: 0,
      ecartees: 0
    };

    let totalSources = 0;

    for (const l of d.lignes) {
      if (l.statutLigne === 'Intégrée') reconciliation.integrees++;
      else if (l.statutLigne === 'Exclue') reconciliation.exclues++;
      else if (l.statutLigne === 'Reportée') reconciliation.reportees++;
      else if (l.statutLigne === 'Écartée') reconciliation.ecartees++;
      else if (l.statutLigne === 'Proposée') reconciliation.proposees++;

      totalSources += l.montantTTC;
    }

    const equilibre = {
      totalSources,
      totalCandidates: totalSources, // En mock, candidates == sources
      isValid: true
    };

    const integrated = d.lignes.filter(l => l.statutLigne === 'Intégrée');

    for (const l of integrated) {
      // Source recap
      let sRecap = recapSource.find(r => r.source === l.source);
      if (!sRecap) {
        sRecap = { source: l.source, ht: 0, tva: 0, ttc: 0 };
        recapSource.push(sRecap);
      }
      sRecap.ht += l.montantHT;
      sRecap.tva += l.montantTVA;
      sRecap.ttc += l.montantTTC;

      // Taux recap
      let tRecap = recapTaux.find(r => r.taux === l.tauxTVA);
      if (!tRecap) {
        tRecap = { taux: l.tauxTVA, ht: 0, tva: 0, ttc: 0 };
        recapTaux.push(tRecap);
      }
      tRecap.ht += l.montantHT;
      tRecap.tva += l.montantTVA;
      tRecap.ttc += l.montantTTC;

      // Activite recap
      const act = l.codeActivite || 'Non Renseigné';
      let aRecap = recapActivite.find(r => r.activite === act);
      if (!aRecap) {
        aRecap = { activite: act, ht: 0, tva: 0, ttc: 0 };
        recapActivite.push(aRecap);
      }
      aRecap.ht += l.montantHT;
      aRecap.tva += l.montantTVA;
      aRecap.ttc += l.montantTTC;

      // Anomalies (Mock logic)
      if (l.montantHT > 1000 && (!l.ice || l.ice === '')) {
         alertes.push({
             type: l.montantHT > 5000 ? 'bloquant' : 'avertissement',
             message: `ICE manquant pour le tiers ${l.tiers} avec un montant > 1000`,
             ligneId: l.id,
             domaine: l.domaine,
             filtre: { id: l.id }
         });
      }
    }

    // Avertissement s'il reste des lignes proposées par domaine
    const proposeesParDomaine = d.lignes.filter(l => l.statutLigne === 'Proposée').reduce((acc, l) => {
       const key = l.domaine;
       acc[key] = (acc[key] || 0) + 1;
       return acc;
    }, {} as Record<string, number>);

    for (const [domaine, count] of Object.entries(proposeesParDomaine)) {
       alertes.push({
           type: 'avertissement',
           message: `Il reste ${count} ligne(s) avec le statut "Proposée" dans le domaine ${domaine}. Elles ne seront pas déclarées.`,
           domaine: domaine as DomaineTVA,
           filtre: { statutLigne: 'Proposée' }
       });
    }

    // Avertissement pour les lignes écartées par domaine
    const ecarteesParDomaine = d.lignes.filter(l => l.statutLigne === 'Écartée').reduce((acc, l) => {
       const key = l.domaine;
       acc[key] = (acc[key] || 0) + 1;
       return acc;
    }, {} as Record<string, number>);

    for (const [domaine, count] of Object.entries(ecarteesParDomaine)) {
       alertes.push({
           type: 'avertissement',
           message: `${count} ligne(s) écartée(s) dans le domaine ${domaine}.`,
           domaine: domaine as DomaineTVA,
           filtre: { statutLigne: 'Écartée' }
       });
    }

    return { recapSource, recapTaux, recapActivite, alertes, reconciliation, equilibre };
  }

  public cloturerDeclaration(id: string) {
    const d = this.declarations.find(x => x.id === id);
    if (!d) throw new Error('Not found');
    
    const checkup = this.getCheckup(id);
    if (checkup.alertes.some(a => a.type === 'bloquant')) {
        throw new Error('Impossible de clôturer: des anomalies bloquantes existent.');
    }

    d.statut = 'Clôturée';
  }

  public genererFichiers(id: string) {
    const d = this.declarations.find(x => x.id === id);
    if (!d) throw new Error('Not found');
    d.statut = 'Générée';
    return { 
      success: true,
      fichiers: {
        xmlDecaissement: `/api/files/${id}/xml/decaissement`,
        xmlEncaissement: `/api/files/${id}/xml/encaissement`,
        excelCheckup: `/api/files/${id}/excel/checkup`,
        rapportAnomalies: `/api/files/${id}/pdf/anomalies`
      }
    };
  }
}

export const mockServer = new MockServer();
