# TASK-184 Verify — Suppression du bloc « Contrôle d'équilibre » + colonne Domaine Activité sur « Totaux par taux »

> Implémentation réalisée en tant que worker. Build solution complète + les 3 suites de tests
> explicitement demandées par la TASK sont vertes. Preuve réelle régénérée contre
> `GR_EMA_DISTRIBUTION` (`TVA1-2026-05`, 735 lignes) — invariant de non-régression vérifié par
> calcul indépendant, égal exactement. Aucun point n'est resté réellement ambigu au sens où la TASK
> l'anticipait (voir § Reste à valider pour les décisions de conception prises et documentées).

## Périmètre livré

1. **Suppression du bloc « Contrôle d'équilibre »** dans `Exporter.cs::CreerFeuilleDetailTva`
   (ex-lignes 308-323, vérifiées avant édition — inchangées depuis la rédaction de la TASK).
   `CreerFeuilleRecap` (export dépôt, feuille « Récap ») et la classe `ControleEquilibre`
   (`Model.cs`) restent intacts, comme exigé — `modele.ControleEquilibre` continue même d'être
   peuplé dans `ConstruireModeleControleAsync` (consommé ailleurs, cf. commentaire existant
   « mêmes agrégats que GetCheckupAsync » — non touché, hors périmètre TASK-184).
2. **Colonne « Domaine Activité »** ajoutée aux tableaux « Totaux par taux — Collecté »/
   « — Déductible » de la feuille « Détail TVA » uniquement (export de contrôle) — valeurs
   `"Achats"` / `"Ventes"` / `"Non résolu"` (code activité absent de la ligne ou introuvable dans
   le référentiel — jamais masqué, même principe que `RecapParActivite`, décision PO TASK-161).
3. **Résolution du domaine** par `CodeActivite` dans `ConstruireModeleControleAsync`, via un
   dictionnaire `Code → DTA_Domaine` chargé une seule fois depuis
   `GetReferentielCodesActiviteAsync()` (méthode déjà existante, TASK-161/172) — **aucune nouvelle
   requête SQL**, conformément à la contrainte de la TASK. Domaine intégré dans le `GroupBy` qui
   construit `modele.RecapsParTaux` (aux côtés de `Taux`/`Collecte` déjà existants).

## Fichiers modifiés

- `Declaration.Core/Model.cs` — `RecapParTaux.Domaine` (`string`, défaut `""`).
- `Declaration.Application/Services/DeclarationWorkflowService.cs` —
  `ConstruireModeleControleAsync` : chargement du référentiel + résolution du domaine intégrée au
  `GroupBy` des `RecapsParTaux` (tri secondaire `ThenBy(Domaine)` ajouté pour un ordre déterministe
  entre les nouveaux groupes d'un même taux) ; nouvelle méthode privée statique
  `ResoudreDomaineActiviteRecap`.
- `Declaration.Export.Excel/Exporter.cs` —
  - `CreerFeuilleDetailTva` : bloc « Contrôle d'équilibre » supprimé.
  - `EcrireBlocRecapParTaux` : nouveau paramètre `bool afficherDomaineActivite = false` (voir
    § Décision de conception ci-dessous) ; colonne « Domaine Activité » insérée en position 2
    (juste après « Taux »), colonnes montants décalées de 3 à 5 **uniquement quand le paramètre
    est actif**.
  - Les 2 appels dans `CreerFeuilleDetailTva` passent `afficherDomaineActivite: true` ; les 2
    appels dans `CreerFeuilleRecap` restent inchangés (valeur par défaut `false`) — feuille
    « Récap » strictement inchangée (vérifié par test, voir § Tests).
- `Declaration.Export.Excel.Tests/ExporterTests.cs` — fixture `GetFixtureControle()` : `Domaine`
  renseigné sur les 2 `RecapParTaux` (`"Ventes"` et `"Non résolu"`, pour couvrir l'affichage réel
  et le cas non masqué) ; assertions mises à jour pour les nouveaux index de colonne (3/4/5 au lieu
  de 2/3/4) + nouvelle assertion explicite : plus aucune occurrence de « Contrôle d'équilibre »
  dans la feuille.

## Décision de conception (point signalé comme potentiellement ambigu par la TASK)

La TASK demande de modifier `EcrireBlocRecapParTaux`, mais cette méthode est **partagée** entre
`CreerFeuilleRecap` (export dépôt, feuille « Récap ») et `CreerFeuilleDetailTva` (export contrôle,
feuille « Détail TVA ») — un ajout de colonne « en dur » aurait donc fait apparaître une colonne
« Domaine Activité » vide (jamais peuplée côté dépôt, `ConstruireModeleExportAsync` ne calculant
pas ce domaine) sur la feuille « Récap », en contradiction directe avec le critère de validation
« Feuille Récap inchangée » et l'exclusion explicite de `CreerFeuilleRecap`. **Décision retenue** :
paramètre optionnel `afficherDomaineActivite` (défaut `false`), activé uniquement par les 2 appels
de `CreerFeuilleDetailTva`. Alternative écartée : dupliquer la méthode (rejetée — règle projet
« pas de duplication de logique existante »). Position de la colonne : juste après « Taux »
(comme « Code Activité » est la 1ʳᵉ colonne du bloc « Totaux par code activité ») — aucune
préférence PO connue sur ce point précis, changement de position resterait cosmétique si le PO en
préfère une autre (ex. dernière colonne).

## Build

- Statut : **OK** — `dotnet build DeclarationTVA.slnx` → 0 erreur, 24 avertissements, tous
  préexistants (aucun nouveau introduit par ce lot — mêmes fichiers/lignes qu'avant le diff).
- **Réserve d'environnement identique à TASK-180/181/182/183** : 2 process `Declaration.API.exe`
  actifs (PID 65092 service Windows + PID 60376 console) verrouillent les DLL de sortie en place.
  Contournement identique : `-p:BaseOutputPath=D:\tmp\build_out_task184\` → build propre, 0 erreur.

## Tests

- `Declaration.Export.Excel.Tests` : **3/3** verts (dont le test `ExporterExcelControle_...`
  corrigé pour les nouveaux index de colonnes + nouvelle assertion négative sur l'absence du bloc).
- `Declaration.Core.Tests` : **52/52** verts.
- `Declaration.Orchestration.Tests` : **182/182** verts (les fakes `GetReferentielCodesActiviteAsync`
  existants — déjà présents dans 15 fichiers de test depuis TASK-172 — n'ont nécessité aucune
  modification, la méthode existait déjà dans l'interface).

## Preuve réelle (base `GR_EMA_DISTRIBUTION`, déclaration `TVA1-2026-05`)

Harness console (hors dépôt, `scratch/` gitignoré, supprimé en fin de session — même méthode que
TASK-180/181/182/183) : `ConstruireModeleControleAsync` + `GenererExcelControleAsync` appelés
directement contre la vraie base (`Server=DESKTOP-5BFKKEP`), `Id` de `TVA1-2026-05` réutilisé
(`6e6c0b95-7e65-4069-95fe-9354555021cc`, déjà résolu lors de TASK-180/182). 735 lignes
`Integree|Proposee`, identique au volume TASK-180/182.

**Invariant de non-régression (exigé par la TASK)** — recalcul indépendant, dans le harness, du
regroupement `(Taux, Collecte)` **sans** le clivage Domaine (= exactement l'ancien `GroupBy`) puis
comparaison stricte avec la somme des sous-totaux `(Achats + Ventes + Non résolu)` du nouveau
`GroupBy` par taux/sens :

```
Taux=20  Collecte=False AVANT(HT=491494,32 TVA=98298,82 TTC=589793,14)  APRES(identique) EGAL=True
Taux=0   Collecte=False AVANT(HT=124186,38 TVA=0,00     TTC=124186,38)  APRES(identique) EGAL=True
Taux=10  Collecte=False AVANT(HT=108624,39 TVA=10862,43 TTC=119486,82)  APRES(identique) EGAL=True
Taux=9   Collecte=False AVANT(HT=288,00    TVA=25,92    TTC=313,92)     APRES(identique) EGAL=True
Taux=20  Collecte=True  AVANT(HT=742456,47 TVA=148491,23 TTC=890947,70) APRES(identique) EGAL=True
Taux=0   Collecte=True  AVANT(HT=157020,18 TVA=0,00     TTC=157020,18)  APRES(identique) EGAL=True
Taux=10  Collecte=True  AVANT(HT=190373,50 TVA=19037,46 TTC=209410,96)  APRES(identique) EGAL=True
Taux=9   Collecte=True  AVANT(HT=2883,33   TVA=259,50   TTC=3142,83)    APRES(identique) EGAL=True

INVARIANT GLOBAL RESPECTE : True
```

(le taux 20/Collecte=True est le seul à se scinder en 2 groupes réels — `Ventes` HT=13 842,09 +
`Non résolu` HT=728 614,38 = 742 456,47, exactement le total d'avant.)

Relecture programmatique (ClosedXML) du `.xlsx` réellement écrit sur disque, feuille
« Détail TVA » :

```
Row  1: A="Totaux par taux — Collecté"
Row  2: A="Taux" B="Domaine Activité" C="Total HT" D="Total TVA" E="Total TTC"
Row  3: A="20"   B="Non résolu"       C=728614,38   D=145722,81   E=874337,19
Row  4: A="20"   B="Ventes"           C=13842,09    D=2768,42     E=16610,51
Row  5: A="10"   B="Non résolu"       C=190373,5    D=19037,46    E=209410,96
Row  6: A="9"    B="Non résolu"       C=2883,33     D=259,5       E=3142,83
Row  7: A="0"    B="Non résolu"       C=157020,18   D=0           E=157020,18
Row  9: A="Totaux par taux — Déductible"
Row 10: A="Taux" B="Domaine Activité" C="Total HT" D="Total TVA" E="Total TTC"
Row 11: A="20"   B="Non résolu"       C=491494,32   D=98298,82    E=589793,14
...
Row 16: A="Totaux par code activité — Collecté"  (bloc inchangé, pas de colonne Domaine ici)
```

Aucune ligne « Contrôle d'équilibre » retrouvée dans les 20 premières lignes de la feuille
(recherche programmatique, `controleEquilibreTrouve = False`).

**Constat sur les données réelles** (attendu, cohérent avec un fait déjà connu — voir mémoire
`grf-task171-cascade-activite-mapping-broken`) : sur ces 735 lignes, un seul groupe résout un
domaine réel (« Ventes », code activité `100`, correspond exactement au groupe déjà visible dans
« Totaux par code activité — Collecté »/ligne 19) — tout le reste est « Non résolu », car le
`CodeActivite` n'est pas renseigné sur la majorité des lignes de cette déclaration. Ce n'est pas un
défaut de TASK-184 : la colonne affiche fidèlement l'état réel du référentiel, sans jamais masquer
le cas non résolu — exactement le comportement demandé.

Fichier joint : `VERIFY/TASK-184-exemple-export-controle.xlsx`.

## Validation checklist

- [x] Build OK (solution complète, réserve d'environnement déjà connue, sans impact sur le résultat).
- [x] Tests passés — `Declaration.Export.Excel.Tests` 3/3, `Declaration.Core.Tests` 52/52,
      `Declaration.Orchestration.Tests` 182/182 (les 3 suites explicitement demandées par la TASK).
- [x] Bloc « Contrôle d'équilibre » absent de la feuille « Détail TVA » — vérifié sur `.xlsx` réel.
- [x] Colonne « Domaine Activité » visible et renseignée sur « Totaux par taux — Collecté »/
      « — Déductible » — vérifié sur `.xlsx` réel, valeurs jamais masquées (« Non résolu » explicite).
- [x] Invariant de non-régression : somme des sous-totaux par domaine = total par taux/sens
      d'avant TASK-184 — vérifié par calcul indépendant sur données réelles, égalité stricte sur
      les 8 couples (Taux, Collecte).
- [x] Feuille « Récap » (export dépôt) strictement inchangée — `CreerFeuilleRecap` non modifié,
      appels à `EcrireBlocRecapParTaux` sans le nouveau paramètre (défaut `false`), test dédié
      (`ExporterExcel_DevraitGenererFichierConforme`) rejoué seul et vert.
- [x] Classe `ControleEquilibre` non supprimée, toujours peuplée dans
      `ConstruireModeleControleAsync` (consommateur potentiel hors périmètre TASK-184, non exploré
      plus avant — hors sujet).
- [x] Aucune nouvelle requête SQL/jointure — `GetReferentielCodesActiviteAsync()` déjà existante,
      appelée une seule fois par exécution.
- [x] Aucun bypass sécurité, aucune couche service contournée.
- [x] Aucune dette technique silencieuse — le compromis de conception (paramètre optionnel plutôt
      que duplication) est documenté ci-dessus avec sa justification.

## Impacts détectés

- Aucun contrat public (DTO API/JSON) modifié — `RecapParTaux` reste un type interne consommé
  uniquement par l'export Excel de contrôle ; aucune route front ne l'expose directement.
- `EcrireBlocRecapParTaux` gagne un paramètre optionnel à valeur par défaut — signature
  rétrocompatible, aucun appelant existant (hors les 2 modifiés) à mettre à jour.
- Front : aucun changement, portée strictement backend comme demandé par la TASK.

## Reste à valider (honnête, non déguisé)

1. **Rendu visuel dans Microsoft Excel** : comme pour TASK-180/181/182, le `.xlsx` de preuve a été
   vérifié programmatiquement (ClosedXML) mais pas ouvert visuellement dans Excel par ce worker
   (pas d'accès interactif à Excel dans cet environnement).
2. **Position de la colonne « Domaine Activité »** (2ᵉ colonne, juste après « Taux ») : choix du
   worker en l'absence de préférence PO explicite — la TASK anticipait cette ambiguïté possible.
   Un changement de position resterait un ajustement cosmétique pur si le PO en préfère une autre.
3. **Consommateur(s) potentiel(s) de `modele.ControleEquilibre`** (ex. `GetCheckupAsync`,
   mentionné en commentaire préexistant à côté de `RecapsParTaux`) : non exploré au-delà de la
   confirmation que le champ reste peuplé — la TASK n'en demandait pas l'audit, et aucun changement
   n'y a été fait ; signalé pour information si le PO souhaite un jour explorer ce lien.
4. **Fichier `TASKS/TASK-184-suppression-bloc-controle-equilibre-export-controle.md`** : brouillon
   antérieur du même TASK-184 (suppression seule, sans colonne Domaine), resté dans `TASKS/` en
   doublon du fichier réellement traité (`TASK-184-controle-equilibre-suppression-et-domaine-totaux-taux.md`,
   celui référencé par `TODO.md`). Non touché par ce worker (hors périmètre strict de
   l'implémentation) — signalé pour nettoyage par l'architecte lors de la clôture.
5. **Process `Declaration.API.exe` (PID 60376)** : toujours actif à la fin de cette session (même
   réserve d'environnement que TASK-180/181/182/183) — sans impact sur ce VERIFY (build/tests
   validés via sortie redirigée).

## Notes worker

- Le harness de preuve (`scratch/TestTask184/`, hors dépôt car `scratch/` est gitignoré,
  `ProjectReference` vers `Declaration.Application`/`Declaration.Infrastructure`/
  `Declaration.Export.Excel`, même `SqlMapper.AddTypeHandler` Guid↔NVARCHAR(36) que
  TASK-150/180/182) a été supprimé après usage ; seul le `.xlsx` produit est conservé dans
  `VERIFY/`.
- `ThenBy(r => r.Domaine)` ajouté à l'`OrderByDescending(r => r.Taux)` existant : nécessaire pour
  un ordre déterministe entre les groupes désormais multiples par taux (auparavant un seul groupe
  par `(Taux, Collecte)`) — pur ajout de tri, aucun changement de calcul.
