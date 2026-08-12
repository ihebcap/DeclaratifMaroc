import { useMemo, useState } from 'react';
import type { CustomFilterProps } from 'ag-grid-react';
import { useGridFilter } from 'ag-grid-react';

export interface CustomListFilterModel {
  filterType: 'customList';
  values: string[];
}

export interface CustomListFilterParams {
  options?: { label: string; value: string }[];
  /**
   * Écrans server-side (pagination + filtres envoyés en paramètres de requête) : appelé
   * directement à chaque changement de sélection, en plus du modèle AG Grid standard
   * (`onModelChange` → `getFilterModel()`). Évite de dépendre de ce cycle pour mettre à jour
   * l'état qui construit la requête serveur.
   */
  onSelectionChange?: (values: string[]) => void;
}

// AG Grid 36 (ag-grid-react) attend des filtres React construits avec le hook `useGridFilter` —
// les props exposent directement `model`/`onModelChange` (AG Grid porte l'état du modèle), et
// `filterChangedCallback` n'existe plus (le wrapper interne le supprime des props). L'ancien
// patron `forwardRef` + `useImperativeHandle` (getModel/setModel/isFilterActive/doesFilterPass)
// ne déclenchait donc jamais rien : `isFilterActive()` restait toujours `false` côté wrapper,
// `doesFilterPass` n'était jamais appelé.
export function CustomListFilter(
  props: CustomFilterProps<any, any, CustomListFilterModel> & CustomListFilterParams
) {
  const { model, onModelChange, options, onSelectionChange, getValue, api } = props;
  const [search, setSearch] = useState('');
  const selectedValues = model?.values ?? [];

  const distinctOptions = useMemo(() => {
    if (options && options.length > 0) {
      return options;
    }
    const set = new Set<string>();
    api.forEachNode((node) => {
      if (node.data) {
        const val = getValue(node);
        if (val !== undefined && val !== null && val !== '') {
          set.add(String(val));
        }
      }
    });
    return Array.from(set).map((v) => ({ label: v, value: v }));
  }, [api, options, getValue]);

  const filteredOptions = useMemo(() => {
    if (!search.trim()) return distinctOptions;
    const lower = search.toLowerCase();
    return distinctOptions.filter((opt) => opt.label.toLowerCase().includes(lower));
  }, [distinctOptions, search]);

  useGridFilter({
    doesFilterPass(params) {
      if (selectedValues.length === 0) return true;
      const val = getValue(params.node);
      const strVal = val !== undefined && val !== null ? String(val) : '';
      return selectedValues.includes(strVal);
    },
  });

  const applySelection = (next: string[]) => {
    onModelChange(next.length > 0 ? { filterType: 'customList', values: next } : null);
    onSelectionChange?.(next);
  };

  const toggleValue = (val: string) => {
    const next = selectedValues.includes(val)
      ? selectedValues.filter((v) => v !== val)
      : [...selectedValues, val];
    applySelection(next);
  };

  const selectAll = () => {
    applySelection(filteredOptions.map((o) => o.value));
  };

  const clearAll = () => {
    applySelection([]);
  };

  return (
    <div style={{ padding: '8px', minWidth: '180px', maxWidth: '260px' }}>
      <input
        type="text"
        placeholder="Rechercher..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        style={{
          width: '100%',
          padding: '4px 8px',
          marginBottom: '6px',
          fontSize: '12px',
          border: '1px solid #ccc',
          borderRadius: '3px',
        }}
      />
      <div style={{ display: 'flex', gap: '6px', marginBottom: '6px' }}>
        <button
          type="button"
          onClick={selectAll}
          style={{ fontSize: '11px', padding: '2px 6px', cursor: 'pointer' }}
        >
          Tout cocher
        </button>
        <button
          type="button"
          onClick={clearAll}
          style={{ fontSize: '11px', padding: '2px 6px', cursor: 'pointer' }}
        >
          Effacer
        </button>
      </div>
      <div style={{ maxHeight: '180px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '4px' }}>
        {filteredOptions.map((opt) => (
          <label key={opt.value} style={{ fontSize: '12px', display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={selectedValues.includes(opt.value)}
              onChange={() => toggleValue(opt.value)}
            />
            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{opt.label}</span>
          </label>
        ))}
      </div>
    </div>
  );
}
