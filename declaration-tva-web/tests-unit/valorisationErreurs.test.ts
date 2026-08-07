// TASK-060 : tests unitaires purs (aucun réseau, aucun DOM) de l'agrégation d'erreurs de
// valorisation. Exécution : `node --test tests-unit/` (runner natif Node, aucune dépendance
// supplémentaire nécessaire).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { agregerErreursValorisation, CODE_METADATA_VALORISATION } from '../src/valorisationErreurs.ts';

test('regroupe par code et compte correctement', () => {
  const erreurs = [
    { code: 'TIERS_SANS_ICE', message: 'x', refLigne: 'FA001' },
    { code: 'TIERS_SANS_ICE', message: 'x', refLigne: 'FA002' },
    { code: 'ERREUR_FGR', message: 'y', refLigne: 'FA003' },
  ];
  const grouped = agregerErreursValorisation(erreurs);
  const sansIce = grouped.find(g => g.code === 'TIERS_SANS_ICE');
  const fgr = grouped.find(g => g.code === 'ERREUR_FGR');
  assert.equal(sansIce?.count, 2);
  assert.equal(fgr?.count, 1);
  assert.equal(sansIce?.qualiteDonnees, true);
  assert.equal(fgr?.qualiteDonnees, false);
});

test('invariant : la somme des counts == nombre total d\'erreurs (livrable de preuve TASK-060 #2)', () => {
  const erreurs = Array.from({ length: 240 }, (_, i) => ({
    code: i % 3 === 0 ? 'TIERS_SANS_ICE' : (i % 3 === 1 ? 'ERREUR_FGR' : 'FACTURE_ILLISIBLE_OM'),
    message: 'm',
    refLigne: `FA${i}`,
  }));
  const grouped = agregerErreursValorisation(erreurs);
  const somme = grouped.reduce((acc, g) => acc + g.count, 0);
  assert.equal(somme, erreurs.length);
  assert.equal(somme, 240);
});

test('code non catalogué : repli sur le message brut, classé anomalie applicative', () => {
  const grouped = agregerErreursValorisation([{ code: 'CODE_INCONNU_XYZ', message: 'msg brut', refLigne: 'FA1' }]);
  assert.equal(grouped[0].label, 'msg brut');
  assert.equal(grouped[0].qualiteDonnees, false);
});

test('exemples de RefLigne plafonnés à 5 et dédupliqués', () => {
  const erreurs = Array.from({ length: 10 }, () => ({ code: 'ERREUR_FGR', message: 'm', refLigne: 'FA-MEME' }));
  const grouped = agregerErreursValorisation(erreurs);
  assert.equal(grouped[0].count, 10);
  assert.deepEqual(grouped[0].exemples, ['FA-MEME']);
});

test('métadonnées de codes couvrent la taxonomie ConstructeurDeclaration.cs', () => {
  const attendus = [
    'REGLEMENT_NON_AFFECTE', 'CODE_TAXE_INCONNU', 'ERREUR_FGR', 'FACTURE_INTROUVABLE',
    'FACTURE_ILLISIBLE_OM', 'TIERS_SANS_ICE', 'ICE_INVALIDE', 'TIERS_SANS_IF', 'IF_INVALIDE',
  ];
  for (const code of attendus) {
    assert.ok(CODE_METADATA_VALORISATION[code], `code manquant : ${code}`);
  }
});
