# VERIFY — Campagne de test comptable réel, déclarations TVA1-2026-01 à 06

## Ordre réel suivi
Les 5 tasks (TASK-168 → 169 → 167 → 166 → 165) ont été livrées et vérifiées en premier, dans
l'ordre imposé, chacune avec son propre `VERIFY/TASK-16X_verify.md`. Cette campagne a été menée
**ensuite**, sur l'instance de dev (port 5299 puis 5000 en fin de session, jamais :5280),
`GR_EMA_DISTRIBUTION` réelle, soId=1.

## Constat de démarrage important
`TVA1-2026-06` était trouvée **déjà Clôturée** au tout début de cette session (statut=1), état
hérité d'une session antérieure (probablement les tests TASK-155/160), pas causé par ce worker.
Rouverte immédiatement (`POST {id}/reouverture`, `204`, confirmé `GET` → `EnCours`) avant de
commencer la campagne, pour repartir d'un état conforme aux préconditions attendues.

`TVA1-2026-02` à `05` n'existaient pas — créées (`POST /declarations`, période EnCours, exercice
2026) puis figées : la sélection de règlements (étape ① normalement pilotée par le front) a dû être
reconstituée manuellement via `GET /rapprochement?declare=false` (tous les règlements non encore
déclarés de la période) → `POST {id}/selection` (numéros complets) → `GET {id}/lignes` (déclenche
`ChargerCandidatesSiNecessaireAsync`, vraie lecture Sage réelle, sous le verrou `soId` TASK-156).
**Aucun outil d'automatisation navigateur disponible dans cette session** — toute l'interaction a
donc été faite directement contre l'API réelle (mêmes endpoints que le front), pas au clavier/souris
dans un vrai navigateur. Voir réserve en fin de rapport.

## Tableau récapitulatif par période

| Période | Lignes (Déc./Enc.) | Équilibre | Bloquants avant | Bloquants après investigation | XML généré |
|---|---|---|---|---|---|
| TVA1-2026-01 | 1237 (mixte, préexistant) | Écart −24 462,00 MAD (expliqué) | 2 | **2** (réels, non résolubles) | ❌ Non |
| TVA1-2026-02 | 174 / 582 | Équilibré (0) | 0 | 0 | ✅ Oui |
| TVA1-2026-03 | 201 / 359 | Écart −20 294,92 MAD (expliqué) | 1 | **1** (réel, non résoluble) | ❌ Non |
| TVA1-2026-04 | 229 / 794 | Écart −6 651,99 MAD (expliqué) | 2 | **2** (voir bug découvert) | ❌ Non |
| TVA1-2026-05 | 157 / 578 | Équilibré (0) | 0 | 0 | ✅ Oui |
| TVA1-2026-06 | 252 / 585 | Équilibré (0) | 0 | 0 | ✅ Oui |

Toutes les périodes ont par ailleurs des avertissements non bloquants (`REGLEMENT_EXCLU`, motif
« Autre (90) » — règlements hors périmètre TVA, non déclarables mais non bloquants) : 7/10/13/19/20/33
respectivement pour 01→06 — cohérent avec le constat déjà documenté par TASK-166.

## Détail par période

### TVA1-2026-01 — 2 bloquants réels, non résolus (données Sage, pas un bug)
`FACTURE_NON_VENTILEE` sur `FC2501717` (EC_Id=21473) et `FC2501667` (EC_Id=20650) : incohérence
Sage `Σ(HT net+TVA+Parafiscale) ≠ TTC`. Diagnostiqué via `POST .../lignes/resynchroniser` (relecture
Sage réelle, action introduite par TASK-167) : **`resolue:false` dans les deux cas**, confirmé par
le log serveur (`[VALO] EC_Id=... pièce ... non mise en cache — OM en erreur : Incohérence Sage :
Σ(HT net+TVA+Parafiscale)=2064301,44 ≠ TTC=20700,00 ...` / `...=3960,00 ≠ TTC=3762,00 ...`) —
**cause racine confirmée réelle côté Sage** (déjà le même verdict que TASK-072/164 sur ce même
`EC_Id=21473`), pas un défaut applicatif. XML non généré pour cette période (2 bloquants restants) —
conforme à la règle de la campagne.

### TVA1-2026-03 — 1 bloquant réel, non résolu (données Sage, pas un bug)
`FACTURE_NON_VENTILEE` sur `FA2502718` (EC_Id=24098) : même famille d'incohérence
(`Σ(HT net+TVA+Parafiscale)=-15318,45 ≠ TTC=20294,93`, écart 35 613,38 MAD). Relecture Sage réelle
via `resynchroniser` → `resolue:false`, confirmé par log. XML non généré (1 bloquant restant).

### TVA1-2026-04 — 2 bloquants, investigation révèle un bug applicatif réel (non corrigé)
`FACTURE_NON_VENTILEE` sur `FC2600005` (EC_Id=18199) et `FC2600004` (EC_Id=18198), motif
`FACTURE_INTROUVABLE` — exactement la famille de cas ciblée par TASK-167 (cf. `FC2502094`
historique). Relecture Sage réelle via le nouveau bouton (`POST .../lignes/resynchroniser`) :
**`{"resolue":true}` renvoyé pour les deux**, mais **le motif de la ligne reste inchangé après
rechargement** (`GET /lignes` + `/checkup` toujours `FACTURE_NON_VENTILEE`/« Facture introuvable ou
non ventilée »).

**Cause racine investiguée** : le log serveur confirme une lecture OM Sage réussie sans erreur
(`[VALO] batch OM : 1 pièce(s)...` → `batch OM rendu : 1 pièce(s), 0 en erreur`), MAIS **aucune
entrée n'a été écrite dans `DM_VENTILATION_SAGE_CACHE`** (confirmé par requête SQL directe en
lecture seule : `SELECT ... WHERE EC_Id IN (18199,18198)` → 0 ligne). Hypothèse la plus probable
(code lu, pas deviné à l'aveugle) : le document OM retourné a `EnErreur=false` mais
`LignesTaxe` vide — dans ce cas, `BuildEntries` (`OrchestrateurDeclaration.cs`) produit une liste
vide, et `VentilationSageCacheRepository.UpsertEntries` retourne immédiatement sans rien écrire
(`if (list.Count == 0) return;`), sans lever d'exception ni journaliser. Le calcul de `resolue`
dans `ResynchroniserLigneAsync` (`!toujoursEnErreur.Contains(ecId)`, basé uniquement sur l'absence
de sentinelle `CodeTaxe='ERREUR'`) est alors **vrai par construction** même si rien n'a été écrit —
**le bouton "Relire depuis Sage" (TASK-167) peut donc afficher un succès trompeur dans ce cas
précis**.

**Défaut PRÉEXISTANT (logique introduite par TASK-078), pas causé par TASK-167** — TASK-167 l'a
seulement rendu visible en l'utilisant sur des lignes `FACTURE_INTROUVABLE` (jusqu'ici ce chemin
n'était exercé que sur des incohérences HT+TVA≠TTC, où un cache est toujours écrit — sentinelle
ERREUR — que la relecture réussisse ou échoue, masquant ce cas). **Non corrigé dans cette
session** : la cause ultime (pourquoi SageTaxReader renvoie 0 ligne de taxe pour une facture réelle
à montant non nul, 5 499,99 MAD et 1 152,00 MAD) nécessiterait une inspection live de l'objet Sage
(COM), hors de portée sans accès à un poste Sage interactif — deviner un correctif serait risqué.
**Recommandation pour une future task** : (1) faire remonter une exception/log explicite quand un
document OM `EnErreur=false` a `LignesTaxe` vide, plutôt que le silence actuel ; (2) faire dépendre
`resolue` de la présence réelle d'une entrée dans le cache après relecture (pas seulement de
l'absence de sentinelle ERREUR) — un vrai correctif de fond, mais qui mérite d'être discuté avec le
PO avant implémentation (comportement affiché différent pour l'utilisateur). XML non généré pour
cette période (2 bloquants restants, non résolus).

### TVA1-2026-02, 05, 06 — 0 bloquant, XML généré et vérifié conforme
Voir section suivante.

## Conformité XML — vérification point par point (02, 05, 06)

Fichiers rangés dans `VERIFY/xml-genere/TVA1-2026-0X/` (le fichier `/fichiers/xml` est en réalité
une archive ZIP — `Content-Type: application/zip` — contenant le XML réel `TVA1-2026-0X-2026-MX.xml`,
dézippé et conservé à côté du `.zip` d'origine et du `.xlsx` de contrôle).

| Critère | TVA1-2026-02 | TVA1-2026-05 | TVA1-2026-06 |
|---|---|---|---|
| Prolog `<?xml version="1.0" ...?>` + `xmlns:xsi` | ✅ | ✅ | ✅ |
| `<tx>` en fraction décimale (jamais %) | ✅ (`0.2`, `0.1`, `0.18`, `0.09`, `0`) | ✅ (`0.2`,`0.1`,`0.09`,`0`) | ✅ (`0.2`,`0.1`,`0.09`,`0`) |
| Aucune balise `<prorata>` | ✅ (0 occurrence) | ✅ (0) | ✅ (0) |
| IF/ICE non tronqués/vides | ✅ (174/174 lignes avec IF+ICE non vides) | ✅ (157/157) | ✅ (252/252) |
| Champs texte (`<nom>`) sans espace résiduel | ✅ (0 occurrence détectée) | ✅ (0) | ✅ (0) |
| Montants cohérents avec les totaux écran ② (même période) | ✅ Σ`<tva>` XML = 135 465,53 MAD = `recapSource.Decaissement.tva` exact | ✅ Σ`<tva>` = 109 187,17 MAD = `recapSource.Decaissement.tva` exact | ✅ Σ`<tva>` = 163 258,21 MAD = `recapSource.Decaissement.tva` exact |
| XML bien formé (parse sans exception) | ✅ (`xml.dom.minidom.parse`) | ✅ | ✅ |
| Excel de contrôle valide (ZIP/xlsx lisible) | ✅ | ✅ | ✅ |

Aucun schéma XSD officiel trouvé dans le dépôt pour une validation formelle contre un schéma DGI —
validation faite par comparaison structurelle avec les critères déjà validés par TASK-137 et par
parsing XML strict (équivalent à `XDocument.Parse` des tests `Declaration.Export.Xml.Tests`, déjà
13/13 verts par ailleurs).

Note sur les taux observés (`0.09`, `0.18`) : présents dans les données réelles (ex. `19.44/216 =
0.09` exactement, calcul vérifié), pas une valeur fabriquée ni un défaut de format — simplement des
taux réels au-delà des 5 taux « canoniques » (20/14/10/7/0 %) rencontrés dans ce jeu de données.
Aucun impact sur le critère vérifié (format fraction décimale, pas la valeur du taux elle-même).

**Aucune non-conformité détectée** — aucun correctif apporté à `DeclarationXmlExporter.cs` dans
cette campagne (contrairement à TASK-137 qui l'avait déjà mis en conformité).

## Bugs de code trouvés pendant la campagne

1. **`ResynchroniserLigneAsync` — succès trompeur (`resolue:true`) quand l'OM répond sans erreur
   mais avec zéro ligne de taxe** — détaillé ci-dessus (TVA1-2026-04). Préexistant (TASK-078), non
   corrigé (root cause Sage non confirmée sans inspection live). **Documenté pour une future task,
   pas implémenté** (conforme à la consigne : ne pas deviner un correctif).
2. **`DeclarationsController.Resynchroniser` ne catchait pas `InvalidOperationException`** —
   trouvé et **corrigé** pendant TASK-167 (cf. `VERIFY/TASK-167_verify.md`) : un rejet du verrou
   `soId` (TASK-156) y remontait en 500 brut au lieu d'un 409 explicite. Confirmé corrigé par test
   réel (deux appels concurrents, 200 + 409).

Aucun autre bug de code découvert pendant la revue des 6 périodes.

## Ce qui reste bloqué/ambigu — nécessite arbitrage PO

- **TVA1-2026-01, 03, 04 ne peuvent pas être signées en l'état** (2, 1, 2 lignes bloquantes
  respectivement) — toutes de cause réelle confirmée (incohérence Sage authentique pour 01/03,
  facture introuvable + bug de signalement pour 04), jamais présentées comme résolues sans preuve.
  Ces anomalies nécessitent une intervention côté ERP (correction de la donnée Sage source) que
  cette session ne peut ni ne doit simuler.
- **Le bug `resolue` trompeur (point 1 ci-dessus)** nécessite un arbitrage PO sur le comportement
  souhaité avant correctif (faire échouer explicitement, ou recalculer automatiquement la ligne
  après une relecture réussie) — non tranché ici.
- Aucun autre point ambigu identifié sur les 3 périodes propres (02/05/06).

## Confirmations explicites de sécurité/état final

- **`GET /api/declarations?societeId=1`, dernier appel de la session** : les 6 déclarations
  (`TVA1-2026-01` à `06`) sont toutes `statut=0` (EnCours) — **aucune ne reste clôturée**.
- Aucun `DT_Id` tamponné à tort : aucune clôture n'a persisté au-delà de la séquence
  clôture→génération→vérification→réouverture immédiate pour 02/05/06 ; 01/03/04 n'ont jamais été
  clôturées (bloquées avant l'étape de clôture, conformément à la règle de la campagne).
- Service Windows `DeclaratifMaroc` (port :5280, PID 65092) **jamais arrêté ni redémarré** —
  vérifié par `netstat`/liste de process à plusieurs reprises tout au long de la session ; seule
  l'instance de dev (build local, ports 5299 puis 5000 en fin de session) a été démarrée/arrêtée.
- `Declaration.API/bin/Debug/net8.0-windows/connections.json` (copie locale) restauré dans son état
  d'origine (`Port: 5000`, `WorkerExePath` du binaire `net48` d'origine) en toute fin de session.
- Incident opérationnel auto-infligé et corrigé pendant la session : après avoir restauré
  `connections.json` une première fois par anticipation, deux appels `resynchroniser`
  supplémentaires (EC_Id 20650, 24098) ont été relancés par erreur avec un chemin worker devenu
  invalide (config restaurée trop tôt) — la purge de cache préalable (toujours effectuée par
  `ResynchroniserLigneAsync`) a temporairement vidé leur sentinelle d'erreur sans pouvoir la
  réécrire (lecture OM en échec faute de binaire trouvé). Détecté immédiatement via les logs
  (chemin `net48` introuvable), corrigé en repointant temporairement vers le worker `v10` correct
  et en rejouant les deux relectures — sentinelles d'erreur réelles réécrites à l'identique
  (mêmes motifs, mêmes montants qu'avant l'incident, confirmé par re-lecture du `checkup`) avant la
  restauration finale et définitive de `connections.json`. Aucune perte de données : les deux
  lignes concernées reflètent exactement le même diagnostic réel qu'avant l'incident.

## Build + suite de tests complète (rejouée en toute fin de session)

- `dotnet build DeclarationTVA.slnx` → **0 erreur** (2 avertissements NuGet préexistants, sans
  rapport).
- `dotnet test DeclarationTVA.slnx` → **Declaration.Core.Tests 54/54**,
  **Declaration.Export.Xml.Tests 13/13**, **Declaration.Export.Excel.Tests 3/3**,
  **Declaration.Selection.Tests 59/59**, **Declaration.Orchestration.Tests 177/177**. Seul échec :
  **Declaration.Controle.Tests.ComparateurTests.GenererRapportVerification** (« Déclaration GRFN 66
  introuvable ») — **préexistant, sans rapport avec les 5 tasks ni la campagne** (test legacy
  dépendant d'un état de base spécifique absent sur ce poste ; aucun fichier de ce chemin n'a été
  touché par cette session, confirmé dans chacun des VERIFY individuels TASK-168/169/167/166/165).
- `npx tsc --noEmit` → 0 erreur.
- `npm run build` (`tsc -b && vite build`, mode projet composite — le seul qui ait détecté une
  vraie erreur de type pendant TASK-165, corrigée sur le moment) → succès, bundle généré.

## Réserves globales non bloquantes
- **Aucun outil d'automatisation navigateur disponible dans cette session** — toute la campagne
  (sélection, figeage, checkup, diagnostic, relecture Sage, clôture/génération/réouverture) a été
  exécutée directement contre l'API réelle (mêmes endpoints que ceux appelés par le front), jamais
  via un vrai navigateur. Le point « console navigateur --errors à chaque écran » demandé par la
  consigne n'a donc **pas pu être vérifié** — limitation honnêtement signalée plutôt que simulée.
  Toutes les données affichées (montants, motifs, compteurs) ont en revanche été vérifiées via les
  mêmes réponses JSON que celles consommées par le front, et la cohérence de calcul a été
  recoupée avec les XML/Excel générés réellement.
- Les 3 périodes bloquées (01/03/04) restent, par construction de la campagne, sans fichier XML —
  conforme à la règle « ne pas générer si anomalie bloquante restante ».
