import { useState, useMemo } from 'react';
import { ExcelFilter } from './ExcelFilter';
import { formatMoney } from './utils';
import type { DeclarationModele, LigneDeclarationEnrichie } from './mockData';
import * as XLSX from 'xlsx';
import { Download } from 'lucide-react';

type SortConfig = { key: keyof LigneDeclarationEnrichie, desc: boolean } | null;

export function ControlGrid({ data }: { data: DeclarationModele }) {
  const [filters, setFilters] = useState<Record<string, string | string[]>>({});
  const [sortConfig, setSortConfig] = useState<SortConfig>(null);

  const columns: { key: keyof LigneDeclarationEnrichie, label: string, filterType: 'list' | 'text' | 'number' | 'date' }[] = [
    { key: 'factureNumero', label: 'N° Facture', filterType: 'text' },
    { key: 'designation', label: 'Désignation', filterType: 'text' },
    { key: 'tiers', label: 'Tiers', filterType: 'text' },
    { key: 'identifiantFiscal', label: 'IF', filterType: 'text' },
    { key: 'ice', label: 'ICE', filterType: 'text' },
    { key: 'montantHT', label: 'Montant HT', filterType: 'number' },
    { key: 'tauxTVA', label: 'Taux TVA', filterType: 'list' },
    { key: 'montantTVA', label: 'Montant TVA', filterType: 'number' },
    { key: 'montantTTC', label: 'Montant TTC', filterType: 'number' },
    { key: 'modePaiement', label: 'Mode Paiement', filterType: 'list' },
    { key: 'datePaiement', label: 'Date Paiement', filterType: 'date' },
    { key: 'dateFacture', label: 'Date Facture', filterType: 'date' },
    { key: 'source', label: 'Source', filterType: 'list' }
  ];

  const handleFilterChange = (key: string, val: any) => {
    setFilters(prev => {
      const next = { ...prev };
      if (val === '' || (Array.isArray(val) && val.length === 0)) {
        delete next[key];
      } else {
        next[key] = val;
      }
      return next;
    });
  };

  const handleSort = (key: keyof LigneDeclarationEnrichie) => {
    setSortConfig(prev => {
      if (prev?.key === key) {
        if (prev.desc) return null; // remove sort
        return { key, desc: true };
      }
      return { key, desc: false };
    });
  };

  // Distinct values for list filters
  const distincts = useMemo(() => {
    const res: Record<string, {label: string, value: string}[]> = {};
    columns.filter(c => c.filterType === 'list').forEach(col => {
      const vals = Array.from(new Set(data.lignes.map(l => String(l[col.key] || ''))));
      res[col.key] = vals.map(v => ({ label: v, value: v }));
    });
    return res;
  }, [data.lignes, columns]);

  // Apply filters and sorting
  const filteredData = useMemo(() => {
    let result = data.lignes.filter(row => {
      for (const [key, filterVal] of Object.entries(filters)) {
        const rowVal = row[key as keyof LigneDeclarationEnrichie];
        
        if (Array.isArray(filterVal)) {
          // List filter
          if (!filterVal.includes(String(rowVal || ''))) return false;
        } else if (typeof filterVal === 'string' && filterVal.includes('~')) {
          // Range filter (number or date)
          const [min, max] = filterVal.split('~');
          if (typeof rowVal === 'number') {
            if (min && rowVal < Number(min)) return false;
            if (max && rowVal > Number(max)) return false;
          } else {
            // string date
            if (min && (rowVal as string) < min) return false;
            if (max && (rowVal as string) > max) return false;
          }
        } else {
          // Text filter
          if (!String(rowVal || '').toLowerCase().includes(String(filterVal).toLowerCase())) return false;
        }
      }
      return true;
    });

    if (sortConfig) {
      result = result.sort((a, b) => {
        const valA = a[sortConfig.key];
        const valB = b[sortConfig.key];
        if (valA < valB) return sortConfig.desc ? 1 : -1;
        if (valA > valB) return sortConfig.desc ? -1 : 1;
        return 0;
      });
    }

    return result;
  }, [data.lignes, filters, sortConfig]);

  const exportExcel = () => {
    const exportData = filteredData.map(row => {
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

  return (
    <div style={{ background: 'white', borderRadius: '8px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)', display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      
      {/* Header / Actions */}
      <div style={{ padding: '1rem', borderBottom: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h3 style={{ fontSize: '1rem', fontWeight: 600, color: 'var(--text-primary)', margin: 0 }}>
          Détail des Lignes ({filteredData.length})
        </h3>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          {Object.keys(filters).length > 0 && (
            <button className="btn" onClick={() => setFilters({})} style={{ background: 'transparent', border: '1px solid var(--border-color)', color: 'var(--text-secondary)', padding: '0.5rem 1rem', fontSize: '0.875rem' }}>
              Effacer les filtres
            </button>
          )}
          <button className="btn btn-primary" onClick={exportExcel} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', background: 'var(--accent-primary)', color: 'white', padding: '0.5rem 1rem', border: 'none', borderRadius: '4px', cursor: 'pointer' }}>
            <Download size={16} /> Exporter Excel
          </button>
          <button className="btn" onClick={() => alert('Mock XML Download')} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', background: 'white', color: 'var(--text-primary)', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px', cursor: 'pointer' }}>
            <Download size={16} /> Exporter XML (EDI)
          </button>
        </div>
      </div>

      {/* Grid */}
      <div style={{ flexGrow: 1, overflow: 'auto' }}>
        <table className="table" style={{ width: '100%', minWidth: '1200px', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
          <thead style={{ position: 'sticky', top: 0, background: 'var(--bg-secondary)', zIndex: 10 }}>
            <tr>
              {columns.map(col => (
                <th key={col.key} style={{ padding: '0.5rem 1rem', borderBottom: '2px solid var(--border-color)', textAlign: ['montantHT', 'montantTVA', 'montantTTC', 'tauxTVA'].includes(col.key) ? 'right' : 'left', cursor: 'pointer', userSelect: 'none' }} onClick={() => handleSort(col.key)}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: ['montantHT', 'montantTVA', 'montantTTC', 'tauxTVA'].includes(col.key) ? 'flex-end' : 'flex-start', gap: '0.25rem' }}>
                    {col.label}
                    {sortConfig?.key === col.key && (
                      <span style={{ fontSize: '0.7rem', color: 'var(--text-secondary)' }}>{sortConfig.desc ? '▼' : '▲'}</span>
                    )}
                    <ExcelFilter 
                      filterType={col.filterType} 
                      options={distincts[col.key]} 
                      selectedValues={Array.isArray(filters[col.key]) ? filters[col.key] as string[] : []}
                      textValue={typeof filters[col.key] === 'string' ? filters[col.key] as string : ''}
                      onChange={(val) => handleFilterChange(col.key, val)} 
                    />
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {filteredData.map(row => (
              <tr key={row.id} style={{ borderBottom: '1px solid var(--border-color)', backgroundColor: 'white' }}>
                {columns.map(col => (
                  <td key={col.key} style={{ padding: '0.5rem 1rem', textAlign: ['montantHT', 'montantTVA', 'montantTTC', 'tauxTVA'].includes(col.key) ? 'right' : 'left', whiteSpace: 'nowrap' }}>
                    {renderCell(col.key, row[col.key])}
                  </td>
                ))}
              </tr>
            ))}
            {filteredData.length === 0 && (
              <tr>
                <td colSpan={columns.length} style={{ padding: '2rem', textAlign: 'center', color: 'var(--text-secondary)' }}>
                  Aucune ligne ne correspond aux filtres.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
