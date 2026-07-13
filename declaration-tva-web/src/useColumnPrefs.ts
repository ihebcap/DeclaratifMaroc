import { useState, useCallback, useMemo } from 'react';

// TASK-068 — préférence de colonnes visibles, persistée en localStorage sous une clé
// propre à l'écran (`storageKey`). Repli sûr : clé absente/corrompue/vide ⇒ toutes
// colonnes visibles (jamais un écran vide au premier chargement).

function readPrefs(storageKey: string, allKeys: string[]): Set<string> {
  try {
    const raw = localStorage.getItem(storageKey);
    if (!raw) return new Set(allKeys);
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return new Set(allKeys);
    const filtered = parsed.filter((k): k is string => typeof k === 'string' && allKeys.includes(k));
    if (filtered.length === 0) return new Set(allKeys);
    return new Set(filtered);
  } catch {
    return new Set(allKeys);
  }
}

function writePrefs(storageKey: string, keys: Set<string>) {
  try {
    localStorage.setItem(storageKey, JSON.stringify([...keys]));
  } catch {
    // quota dépassé / accès refusé (mode privé) : préférence non persistée, non bloquant.
  }
}

export function useColumnPrefs<T extends { key: string }>(storageKey: string, allColumns: T[]) {
  const allKeys = useMemo(() => allColumns.map(c => c.key), [allColumns]);

  const [visibleKeys, setVisibleKeys] = useState<Set<string>>(() => readPrefs(storageKey, allKeys));

  const visibleColumns = useMemo(
    () => allColumns.filter(c => visibleKeys.has(c.key)),
    [allColumns, visibleKeys],
  );

  const toggle = useCallback((key: string) => {
    setVisibleKeys(prev => {
      if (prev.has(key) && prev.size <= 1) return prev; // au moins 1 colonne visible en permanence
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      writePrefs(storageKey, next);
      return next;
    });
  }, [storageKey]);

  const reset = useCallback(() => {
    const next = new Set(allKeys);
    writePrefs(storageKey, next);
    setVisibleKeys(next);
  }, [storageKey, allKeys]);

  return { visibleColumns, visibleKeys, toggle, reset };
}
