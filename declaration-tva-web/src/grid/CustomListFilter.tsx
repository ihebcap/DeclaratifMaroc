import { forwardRef, useImperativeHandle, useState, useMemo } from 'react';
import type { IFilterParams } from 'ag-grid-community';

export interface CustomListFilterParams extends IFilterParams {
  options?: { label: string; value: string }[];
}

export const CustomListFilter = forwardRef((props: CustomListFilterParams, ref) => {
  const [selectedValues, setSelectedValues] = useState<string[]>([]);
  const [search, setSearch] = useState('');

  const distinctOptions = useMemo(() => {
    if (props.options && props.options.length > 0) {
      return props.options;
    }
    const set = new Set<string>();
    if (props.api) {
      props.api.forEachNode((node) => {
        if (node.data) {
          const val = props.getValue(node);
          if (val !== undefined && val !== null && val !== '') {
            set.add(String(val));
          }
        }
      });
    }
    return Array.from(set).map((v) => ({ label: v, value: v }));
  }, [props.api, props.options, props.getValue]);

  const filteredOptions = useMemo(() => {
    if (!search.trim()) return distinctOptions;
    const lower = search.toLowerCase();
    return distinctOptions.filter((opt) => opt.label.toLowerCase().includes(lower));
  }, [distinctOptions, search]);

  useImperativeHandle(ref, () => ({
    isFilterActive() {
      return selectedValues.length > 0;
    },
    doesFilterPass(params: any) {
      if (selectedValues.length === 0) return true;
      const val = props.getValue(params.node);
      const strVal = val !== undefined && val !== null ? String(val) : '';
      return selectedValues.includes(strVal);
    },
    getModel() {
      if (selectedValues.length === 0) return null;
      return { filterType: 'customList', values: selectedValues };
    },
    setModel(model: any) {
      if (model && Array.isArray(model.values)) {
        setSelectedValues(model.values);
      } else {
        setSelectedValues([]);
      }
    },
  }));

  const toggleValue = (val: string) => {
    const next = selectedValues.includes(val)
      ? selectedValues.filter((v) => v !== val)
      : [...selectedValues, val];
    setSelectedValues(next);
    props.filterChangedCallback();
  };

  const selectAll = () => {
    setSelectedValues(filteredOptions.map((o) => o.value));
    props.filterChangedCallback();
  };

  const clearAll = () => {
    setSelectedValues([]);
    props.filterChangedCallback();
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
});
CustomListFilter.displayName = 'CustomListFilter';
