// Ces écrans interrogent le serveur (pagination + filtres envoyés en paramètres de requête) — les
// filtres AG Grid natifs (icônes d'en-tête) ne font QUE du filtrage client sur la page déjà chargée
// s'ils ne sont pas explicitement reliés à l'état qui construit la requête. Cette fonction convertit
// le modèle de filtre AG Grid (`api.getFilterModel()`) vers la forme `Record<string, string | string[]>`
// déjà consommée par les écrans (CustomListFilter → tableau de valeurs ; texte → chaîne ; nombre/date
// → chaîne "min~max").
export type LegacyFilterValue = string | string[];

export function agFilterModelToLegacy(model: Record<string, any>): Record<string, LegacyFilterValue> {
  const result: Record<string, LegacyFilterValue> = {};

  for (const [colId, colModel] of Object.entries(model)) {
    if (!colModel) continue;

    if (colModel.filterType === 'customList' && Array.isArray(colModel.values)) {
      if (colModel.values.length > 0) result[colId] = colModel.values;
      continue;
    }

    if (colModel.filterType === 'text') {
      if (typeof colModel.filter === 'string' && colModel.filter.trim() !== '') {
        result[colId] = colModel.filter.trim();
      }
      continue;
    }

    if (colModel.filterType === 'number') {
      const range = numberRangeFromModel(colModel);
      if (range) result[colId] = range;
      continue;
    }

    if (colModel.filterType === 'date') {
      const range = dateRangeFromModel(colModel);
      if (range) result[colId] = range;
      continue;
    }
  }

  return result;
}

function numberRangeFromModel(m: any): string | null {
  switch (m.type) {
    case 'inRange':
      return `${m.filter ?? ''}~${m.filterTo ?? ''}`;
    case 'equals':
      return `${m.filter ?? ''}~${m.filter ?? ''}`;
    case 'greaterThan':
    case 'greaterThanOrEqual':
      return `${m.filter ?? ''}~`;
    case 'lessThan':
    case 'lessThanOrEqual':
      return `~${m.filter ?? ''}`;
    default:
      return m.filter !== undefined && m.filter !== null ? `${m.filter}~${m.filter}` : null;
  }
}

function dateRangeFromModel(m: any): string | null {
  const from = m.dateFrom ? String(m.dateFrom).slice(0, 10) : '';
  const to = m.dateTo ? String(m.dateTo).slice(0, 10) : '';
  switch (m.type) {
    case 'inRange':
      return `${from}~${to}`;
    case 'equals':
      return `${from}~${from}`;
    case 'greaterThan':
      return `${from}~`;
    case 'lessThan':
      return `~${from}`;
    default:
      return from ? `${from}~${from}` : null;
  }
}
