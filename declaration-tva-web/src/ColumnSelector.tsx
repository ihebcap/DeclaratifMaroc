import { useState, useRef, useLayoutEffect, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { Columns3 } from 'lucide-react';

// TASK-068 — bouton + popup à cases à cocher (patron visuel ExcelFilter, via createPortal)
// pour choisir les colonnes affichées d'une grille. Persistance déléguée à useColumnPrefs.

export function ColumnSelector({
  columns,
  visibleKeys,
  onToggle,
  onReset,
}: {
  columns: { key: string; label: string }[];
  visibleKeys: Set<string>;
  onToggle: (key: string) => void;
  onReset: () => void;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [pos, setPos] = useState<{ top: number; left: number }>({ top: 0, left: 0 });
  const buttonRef = useRef<HTMLButtonElement>(null);
  const popupRef = useRef<HTMLDivElement>(null);

  const POPUP_WIDTH = 220;

  const computePosition = () => {
    if (!buttonRef.current) return;
    const rect = buttonRef.current.getBoundingClientRect();
    let left = rect.right - POPUP_WIDTH;
    if (left < 8) left = 8;
    if (left + POPUP_WIDTH > window.innerWidth - 8) left = Math.max(8, window.innerWidth - POPUP_WIDTH - 8);
    setPos({ top: rect.bottom + 4, left });
  };

  useLayoutEffect(() => {
    if (isOpen) computePosition();
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) return;
    const handleClickOutside = (event: MouseEvent) => {
      const target = event.target as Node;
      if (buttonRef.current?.contains(target)) return;
      if (popupRef.current?.contains(target)) return;
      setIsOpen(false);
    };
    const handleReposition = () => computePosition();
    document.addEventListener('mousedown', handleClickOutside);
    window.addEventListener('scroll', handleReposition, true);
    window.addEventListener('resize', handleReposition);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      window.removeEventListener('scroll', handleReposition, true);
      window.removeEventListener('resize', handleReposition);
    };
  }, [isOpen]);

  const popup = isOpen ? createPortal(
    <div
      ref={popupRef}
      onClick={(e) => e.stopPropagation()}
      style={{
        position: 'fixed', top: pos.top, left: pos.left, zIndex: 1000,
        background: 'white', border: '1px solid var(--border-color)', borderRadius: '4px',
        boxShadow: '0 4px 12px rgba(0,0,0,0.15)', padding: '0.5rem', width: `${POPUP_WIDTH}px`, cursor: 'default',
      }}
    >
      <div style={{ maxHeight: '280px', overflowY: 'auto', overflowX: 'hidden' }}>
        {columns.map(col => (
          <label key={col.key} style={{ display: 'block', fontSize: '0.75rem', cursor: 'pointer', padding: '4px 0' }}>
            <input
              type="checkbox"
              style={{ marginRight: '8px', verticalAlign: 'middle' }}
              checked={visibleKeys.has(col.key)}
              onChange={() => onToggle(col.key)}
            />
            <span style={{ verticalAlign: 'middle', wordBreak: 'break-word' }}>{col.label}</span>
          </label>
        ))}
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '0.5rem', paddingTop: '0.5rem', borderTop: '1px solid var(--border-color)' }}>
        <button onClick={onReset} style={{ fontSize: '0.75rem', color: 'var(--accent-primary)', background: 'none', border: 'none', cursor: 'pointer' }}>
          Tout afficher
        </button>
      </div>
    </div>,
    document.body,
  ) : null;

  return (
    <div style={{ position: 'relative', display: 'inline-block' }} onClick={(e) => e.stopPropagation()}>
      <button
        ref={buttonRef}
        onClick={() => setIsOpen(o => !o)}
        className="btn"
        title="Choisir les colonnes affichées"
        style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.8rem', padding: '0.25rem 0.6rem' }}
      >
        <Columns3 size={14} /> Colonnes
      </button>
      {popup}
    </div>
  );
}
