import { useMemo } from 'react';
import type { ColDef } from 'ag-grid-community';
import { ApbsGrid } from './grid/ApbsGrid';
import { CustomListFilter } from './grid/CustomListFilter';
import { formatMoney } from './utils';
import type { DeclarationModele, LigneDeclarationEnrichie } from './mockData';
import * as XLSX from 'xlsx';
import { Download } from 'lucide-react';

export function ControlGrid({ data }: { data: DeclarationModele }) {

  const columns: { key: keyof LigneDeclarationEnrichie, label: string, filterType: 'list' | 'text' | 'number' | 'date' }[] = useMemo(() => [
    { key: 'factureNumero', label: 'N° Facture', filterType: 'list' },
    { key: 'designation', label: 'Désignation', filterType: 'list' },
    { key: 'tiers', label: 'Tiers', filterType: 'list' },
    { key: 'identifiantFiscal', label: 'IF', filterType: 'list' },
    { key: 'ice', label: 'ICE', filterType: 'list' },
    { key: 'montantHT', label: 'Montant HT', filterType: 'number' },
    { key: 'tauxTVA', label: 'Taux TVA', filterType: 'list' },
    { key: 'montantTVA', label: 'Montant TVA', filterType: 'number' },
    { key: 'montantTTC', label: 'Montant TTC', filterType: 'number' },
    { key: 'modePaiement', label: 'Mode Paiement', filterType: 'list' },
    { key: 'datePaiement', label: 'Date Paiement', filterType: 'date' },
    { key: 'dateFacture', label: 'Date Facture', filterType: 'date' },
    { key: 'source', label: 'Source', filterType: 'list' }
  ], []);

  const exportExcel = () => {
    if (!data.lignes) return;
    const exportData = data.lignes.map(row => {
      const res: any = {};
      columns.forEach(c => {
        res[c.label] = row[c.key];
      });
      return res;
    });
    const worksheet = XLSX.utils.json_to_sheet(exportData);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, "ControleTVA");
    XLSX.writeFile(workbook, "Export_Controle_TVA.xlsx");
  };

  const renderCell = (key: keyof LigneDeclarationEnrichie, val: any) => {
    if (['montantHT', 'montantTVA', 'montantTTC'].includes(key)) return formatMoney(val);
    if (key === 'tauxTVA') return `${val}%`;
    if (['dateFacture', 'datePaiement'].includes(key) && val) {
      return new Date(val).toLocaleDateString('fr-FR');
    }
    return val;
  };

  const columnDefs: ColDef[] = useMemo(() => {
    return columns.map((col) => {
      const isNumeric = ['montantHT', 'montantTVA', 'montantTTC', 'tauxTVA'].includes(col.key);
      let filterComponent: any = undefined;

      if (col.filterType === 'list') {
        filterComponent = CustomListFilter;
      } else if (col.filterType === 'number') {
        filterComponent = 'agNumberColumnFilter';
      } else if (col.filterType === 'date') {
        filterComponent = 'agDateColumnFilter';
      } else {
        filterComponent = 'agTextColumnFilter';
      }

      return {
        field: col.key,
        headerName: col.label,
        width: 140,
        type: isNumeric ? 'numericColumn' : undefined,
        filter: filterComponent,
        cellRenderer: (p: any) => p.data ? renderCell(col.key, p.data[col.key]) : null,
      };
    });
  }, [columns]);

  return (
    <div style={{ background: 'white', borderRadius: '8px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)', display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      
      {/* Header / Actions */}
      <div style={{ padding: '1rem', borderBottom: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h3 style={{ fontSize: '1rem', fontWeight: 600, color: 'var(--text-primary)', margin: 0 }}>
          Détail des Lignes ({data.lignes.length})
        </h3>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button className="btn btn-primary" onClick={exportExcel} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', background: 'var(--accent-primary)', color: 'white', padding: '0.5rem 1rem', border: 'none', borderRadius: '4px', cursor: 'pointer' }}>
            <Download size={16} /> Exporter Excel
          </button>
          <button className="btn" onClick={() => alert('Mock XML Download')} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', background: 'white', color: 'var(--text-primary)', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px', cursor: 'pointer' }}>
            <Download size={16} /> Exporter XML (EDI)
          </button>
        </div>
      </div>

      {/* Grid */}
      <div style={{ flexGrow: 1, position: 'relative' }}>
        <ApbsGrid
          rowData={data.lignes}
          columnDefs={columnDefs}
          height="100%"
          showColumnSelector={true}
          showExportButton={false}
        />
      </div>
    </div>
  );
}
