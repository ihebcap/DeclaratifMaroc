# TASK-222 — Seuil d'exclusion DDP paramétrable par société (défaut = règle DGI), avec historique et signalement de l'écart

RISK : HIGH (périmètre déclaratif légal, écart volontaire à la règle DGI pour une société) — discipline de preuve par critère exigée dans le VERIFY.
Revue : 5 passes indépendantes le 08/10/2026 (sources, code, données, testabilité, risques) intégrées dans cette version.
Dépendance : **TASK-221 mergée avant le démarrage** (borne `>`, `MontantFactureTtc` = total TTC de la facture, règle d'éligibilité unique en C#). Partout où cette TASK dit « montant », il s'agit de `MontantFactureTtc` (décision PO « facture TTC », 08/10/2026). Livraison en production : avec TASK-221, après visa PO (décision F de TASK-221).
Point d'arrêt : dépôt de `VERIFY/TASK-222_verify.md`. **Interdit** : déplacer vers `DONE_DETAIL/`, modifier `DONE.md`/`TODO.md`/`CHANGELOG.md`, commit contenant « approuve ». La clôture revient à une review par un agent qui n'a pas implémenté.

## Décisions PO préalables (BLOQUANT : ne rien implémenter du stockage ni de l'API avant les réponses D et E)
- **D. Schéma** : acceptez-vous **2 nouvelles tables GRF** (réglage par société + historique append-only), sans toucher à `DM_PARAM_DELAIPAIEMENT_SOCIETE` ni à aucune table winform ? [ ] oui
- **E. Droits** : le réglage du seuil est réservé aux administrateurs (`UT_Admin=1`) ; la date de mise en route garde sa garde actuelle. [ ] oui
- **H. Lecture de la demande** : « jusqu'au 31/12/2025 » = **date d'émission** de la facture (`DO_Date`), non la période de déclaration ; borne **« <= 10 000 »** (identique à la DGI et à TASK-221) alors que le PO a dit « < 10 000 » (cas 10 000,00 exactement). [ ] confirmé
- **I. Écart assumé** : les échéances exclues par le réglage cessent d'être suivies, **y compris celles déjà déclarées** (ex. la facture de 800 MAD du 23/04/2025 déclarée au 31/12/2025 dans l'annuelle 2025 : plus aucun incrément de retard) ; le PO signe la liste des échéances exclues avant activation en production. [ ] oui
- **J. Bornes de validation** : `0 < seuil <= 100 000` ; `2023-07-01 <= date limite <= 2026-12-31` ; motif obligatoire (>= 10 caractères). [ ] oui
- **K. Texte du bandeau** (ci-dessous) validé tel quel. [ ] oui

## Contexte
Demande PO (08/10/2026), urgente (un client est bloqué) : pour **un client précis**, ignorer les factures dont le montant TTC est inférieur à 10 000 **jusqu'au 31/12/2025** ; **à partir du 01/01/2026 il déclare toutes les factures**. Cela s'écarte de la règle DGI pour les seules factures émises en 2025, que la DGI n'exempte plus (note circulaire n°734 §O-2 p.10 : exemption limitée aux factures émises avant le 01/01/2025 ; annonce DGI du 21/03/2025, finances.gov.ma `fiche=7218` : « l'amende pécuniaire s'applique à toutes les factures émises à compter du 1er janvier 2025, y compris celles dont le montant est inférieur ou égal à 10 000 dirhams »). Ces textes ont été présentés au PO par l'architecte le 08/10/2026 ; la décision d'écart lui appartient. Cas d'origine, selon le signalement du PO : facture de 800 MAD du 23/04/2025 (échéance légale 12/08/2025) visible à l'écran Contrôle de T3 2026 ; elle sera exclue par ce réglage.

Aujourd'hui aucun réglage n'existe : le seuil est une constante statutaire (`SeuilsLegauxDelaiPaiement`, `Declaration.Core/SelectionDelaiPaiementCalculator.cs:14-33`). La date de mise en route société (TASK-220) n'est **pas équivalente** : elle exclut toutes les factures antérieures à une date, quel que soit leur montant. Une paramétrisation par société existe pour la date de mise en route (`DM_PARAM_DELAIPAIEMENT_SOCIETE`, `DeclarationTVA.sql:543-550`, `DateMiseEnRoute DATETIME2 NOT NULL`) : **elle n'est pas réutilisée** (colonne obligatoire, PUT qui remplace la ligne, audit `UT_Id`/`DateSaisie` écrasé à chaque saisie de la date) ; le seuil vit dans ses propres tables.

## Objectif
```
Entrée  : société sans réglage -> règle DGI (TASK-221) ; société avec seuil 10 000 et date limite 2025-12-31.
Traitement : seuil et date limite deviennent des valeurs par société (une ligne dédiée) ; absence de ligne = valeurs statutaires.
Sortie  : pour la société réglée, toute échéance de facture émise au plus tard à la date limite et de total TTC <= seuil est exclue du
          contrôle DDP ; dès le 01/01/2026 tous les montants sont retenus ; les autres sociétés sont strictement inchangées ;
          l'écart est visible à l'écran, dans l'export et dans un historique non modifiable.
```

## Périmètre STRICT
- **Inclus** :
  1. **Stockage (décision D)** : dans `DeclarationTVA.sql`, après la table existante (~l.553), conventions du fichier (`IF OBJECT_ID('dbo.X','U') IS NULL CREATE TABLE ...` suivi de `GO`) :
     - `dbo.DM_PARAM_DDP_SEUIL_SOCIETE` : `SO_Id INT NOT NULL PRIMARY KEY`, `SeuilMontant decimal(18,2) NULL`, `SeuilDateLimite date NULL`, `UT_Id INT NULL`, `DateSaisie datetime2 NOT NULL`, `Motif nvarchar(400) NOT NULL`. Ligne absente = règle DGI ; `SeuilMontant NULL` = 10 000 statutaire ; `SeuilDateLimite NULL` = 2024-12-31 statutaire.
     - `dbo.DM_PARAM_DDP_SEUIL_SOCIETE_HISTO` : `HI_Id INT IDENTITY PRIMARY KEY`, `SO_Id`, `Action` (`SET`/`RESET`), `SeuilMontant_Ancien/Nouveau`, `SeuilDateLimite_Ancien/Nouveau`, `UT_Id`, `DateModif datetime2`, `Motif` ; append-only (`GRANT SELECT, INSERT` seulement) ; une entrée à chaque `SET`/`RESET`, écrite dans la même transaction que la modification.
     - `GRANT` de la première table comme le motif existant (`DeclarationTVA.sql:843`) ; en-tête du fichier (l.12) mis à jour (TASK-222). Aucune autre table touchée, aucun `ALTER` sur une table existante.
  2. **Règle** : `SeuilsLegauxDelaiPaiement.EstEligibleSeuilLegal(DateTime dateDocument, decimal montantFactureTtc, decimal seuilMontant, DateTime dateLimiteSeuil)` ; la surcharge à 2 arguments (TASK-221) transmet les constantes statutaires et reste utilisée par les tests statutaires. Même borne et même arrondi que TASK-221 (`Math.Round(x,2,AwayFromZero) > seuil` requis pour être retenue avant la date limite incluse). `DateDebutDeclarationLoi` (2023-07-01) s'applique dans tous les cas. Le SQL n'est plus concerné (TASK-221 : règle unique en C#).
  3. **Résolution à un seul endroit** : `SelectionDelaiPaiementService.SelectionnerAsync` lit le réglage de la société **avant** le calcul des candidats et passe les valeurs résolues (`null` -> constante statutaire) à `ParametresSelectionDelaiPaiement` (`SeuilMontant`, `SeuilDateLimite`) et à `EstEligibleSeuilLegal`. `ResultatSelectionDelaiPaiement` expose `SeuilApplique { SeuilMontant, DateLimite, EstPersonnalise }`, y compris sur le chemin de retour anticipé (aucune échéance). Si les nouvelles tables sont absentes (script non rejoué), le service applique la règle DGI et journalise un avertissement (jamais d'erreur 500, jamais d'exclusion non voulue).
  4. **API** (`DelaiPaiementParametrageController`, DTO dans `DeclarationDelaiPaiementDto.cs`) : le `PUT parametrage/{soId}` de la date de mise en route est **inchangé** (aucun effet sur le seuil) ; `GET parametrage/{soId}` renvoie en plus `seuil { seuilMontant, seuilDateLimite, motif, utId, dateSaisie, ecartRegleDgi } | null` (ajout rétro-compatible) ; nouveaux `PUT parametrage/{soId}/seuil` (corps `{ seuilMontant?, seuilDateLimite?, motif }`) et `DELETE parametrage/{soId}/seuil` (corps/paramètre `motif`) réservés aux administrateurs : **nouvelle garde** `User.HasClaim("UT_Admin","1")` ET société autorisée (`EstSocieteAutorisee` seule laisserait passer tout utilisateur de la société), sinon 403 ; `soId` uniquement en route. Validation (décision J) : au moins un des deux champs renseigné ; `0 < seuilMontant <= 100 000` avec au plus 2 décimales ; `2023-07-01 <= seuilDateLimite <= 2026-12-31` (borne haute = constante statutaire) ; dates normalisées `.Date` ; motif >= 10 caractères ; sinon 400. Journal Information structuré (`SoId`, `UT_Id`, anciennes/nouvelles valeurs). `EcartRegleDgi = (SeuilMontant != null && SeuilMontant != 10000) || (SeuilDateLimite != null && SeuilDateLimite.Date != 2024-12-31)`, défini une seule fois (service) et réutilisé par l'API, le DTO de sélection, le bandeau et l'export.
  5. **Front** : `api.ts` (helpers `getParametrageDdp` étendu + `setSeuilDdp`/`resetSeuilDdp`, aujourd'hui limités à la date, l.504-511) ; nouvelle fenêtre « Seuil d'exclusion DDP » ouverte depuis le même point d'entrée que la mise en route (`MiseEnRouteDelaiPaiementModal.tsx` est partagé par 4 emplacements : ne pas y mêler les champs de seuil) ; bouton visible et champs éditables **uniquement pour un administrateur** (vérifier comment `Auth.tsx` expose le claim) ; saisie du seuil (format fr-FR 2 décimales), de la date limite, du motif ; avertissement explicite à l'enregistrement quand `EcartRegleDgi` ; bouton « Revenir à la règle DGI » (DELETE + motif). Respect de `DOCS/UI_STANDARDS.md` (lire intégralement : variables CSS, `lucide-react` `AlertTriangle` 13-16, `btn`/`btn-primary`, `form-input`, aucune couleur en dur ; justifier tout `<input type="date">`).
  6. **Bandeau d'écart** sur les 3 emplacements qui montrent la sélection DDP : écran Contrôle (`ControleLignesDelaiPaiementPanel.tsx`), écran des déclarations DDP (`DeclarationsDelaiPaiementPanel.tsx`) et fenêtre de sélection d'intégration ; affiché si et seulement si `ecartRegleDgi` est vrai, avec ce texte (décision K) : « **ATTENTION — Réglage société dérogatoire à la règle DGI : les factures de total TTC <= {seuil} MAD émises jusqu'au {dateLimite} sont exclues de ce contrôle. La DGI (note circulaire 734 ; annonce du 21/03/2025) n'exempte plus que les factures émises avant le 01/01/2025. Réglé par {utilisateur} le {date}. Risque : amende de 5 000 MAD par facture manquante ou inexacte lorsque la déclaration est insuffisante ou incomplète.** »
  7. **En-tête d'export Excel** : le même texte en première ligne. `grid/gridExport.ts` n'écrit aujourd'hui aucune ligne d'en-tête (`json_to_sheet(rows)`) : étendre `exportGridToExcel` et `ApbsGrid` par une prop `exportHeaderLines?: string[]` (`XLSX.utils.sheet_add_aoa` puis `json_to_sheet(rows, { origin })`), **sans changer le comportement des autres grilles**, avec un test de non-régression sur une autre grille. Le fichier XML de dépôt DGI n'est pas modifié par cette TASK.
  8. **Tests** : voir « Plan de tests ». 9. **Déploiement** : ordre 1) script SQL (rétro-compatible, tables nouvelles, rien ne casse l'ancienne API), 2) API, 3) front ; rollback sans `DROP` (l'ancienne API ignore les tables) ; ajouter la ligne du script à rejouer dans `DOCS/DEPLOIEMENT.md` ; vérifier que `Declaration.Setup/deploy` embarque le script à jour (à citer dans le VERIFY) ; `DeclarationTVA.sql` est aussi modifié par TASK-126 (ouverte) : rebase et tolérer son remaniement.
- **Exclus** : valeurs par défaut/DGI ; TASK-220 ; tout autre calcul du montant que `MontantFactureTtc` fourni par TASK-221 ; modification de `DM_PARAM_DELAIPAIEMENT_SOCIETE` ; écran de TASK-211 ; tout changement de schéma sur une table winform ; purge/régénération de déclarations déjà générées ou déposées ; fichier XML de dépôt.

## Étapes
1. Lire `DOCS/UI_STANDARDS.md`, le contrôleur et le repository de paramétrage, `MiseEnRouteDelaiPaiementModal.tsx`, `gridExport.ts`, `ApbsGrid.tsx` ; `git log -5` sur les fichiers touchés et rebase sur `main` (TASK-221 mergée).
2. Script SQL (2 tables), entités et repositories (lecture ; `SET`/`RESET` transactionnels avec historique).
3. Règle paramétrée (C#) et résolution dans le service ; `SeuilApplique` dans le résultat.
4. API (garde admin, validation, journal) ; front (fenêtre, avertissement, bandeau x3, en-tête d'export).
5. **Mesure sur la base du client** (lecture seule, depuis une machine qui y accède ; sinon STOP) : liste exportée des échéances exclues par le réglage, par statut (jamais déclarée / déjà déclarée avec sa dernière borne), nombre et montants ; jointe au VERIFY.
6. `dotnet build DeclarationTVA.slnx` ; `dotnet test` ; `npm run lint` + `npm run build` ; `npx playwright test` (`task134`, `task136`, nouveau `task222`) ; script SQL **exécuté deux fois** sur une base de test (0 erreur, 0 ligne modifiée) ; checklist de revue UI de `DOCS/UI_STANDARDS.md` cochée par critère avec méthode et date.
7. Déposer `VERIFY/TASK-222_verify.md` et **s'arrêter**.

## Plan de tests
- **Core** : `Seuil_Personnalise_800_2025_04_23_Exclu`, `…_2026_01_01_Inclus`, `…_LimiteNull_UtiliseStatutaire`, `…_SeuilNull_UtiliseStatutaire`, `…_Borne_10000_Exclu`, `…_10000_01_Inclus`, `…_AvantLoi_Exclu`, `…_MiseEnRouteEtSeuilCumules` (l'exclusion la plus large l'emporte), `EcartRegleDgi_*` (réglage égal au statutaire = pas d'écart).
- **Service/Orchestration** : valeurs résolues passées une seule fois ; chemin « aucune échéance » porte `SeuilApplique` ; tables absentes = règle DGI + avertissement ; société A réglée n'affecte pas la société B.
- **API** (nouveau `ParametrageDelaiPaiementApiTests`) : non admin 403 ; admin 204 ; société hors claim 403 ; seuil 0, négatif, > 100 000, 3 décimales -> 400 ; date limite < 2023-07-01 ou > 2026-12-31 -> 400 ; motif < 10 caractères -> 400 ; les deux champs absents -> 400 ; `DELETE` remet la règle DGI ; `PUT parametrage/{soId}` (date de mise en route) sans effet sur le seuil ; chaque `SET`/`RESET` écrit une entrée d'historique ; `GET` renvoie `seuil` et `ecartRegleDgi`.
- **Front/Playwright** (`task222.spec.ts`) : fenêtre et champs visibles pour un admin, absents pour un non-admin ; avertissement à l'enregistrement ; bandeau affiché sur les 3 emplacements si écart actif, absent sinon ; en-tête d'export présent/absent ; bouton « Revenir à la règle DGI ».
- **Export** : test de non-régression d'une autre grille (aucune ligne d'en-tête ajoutée).

## Scénarios de test (règle après TASK-221 ; `DateMiseEnRoute` non configurée ; « montant » = `MontantFactureTtc`)
| Société | DoDate | Montant | Attendu |
|---|---|---|---|
| sans réglage | 2025-04-23 | 800 | oui (règle DGI) |
| sans réglage | 2024-12-31 | 9 999,99 | non |
| seuil 10 000 / limite 2025-12-31 | 2025-04-23 | 800 | non (cas d'origine du client) |
| seuil 10 000 / limite 2025-12-31 | 2025-12-31 | 800 | non |
| seuil 10 000 / limite 2025-12-31 | 2025-12-31 | 10 000,00 | non (borne exclue) |
| seuil 10 000 / limite 2025-12-31 | 2025-12-31 | 10 000,01 | oui |
| seuil 10 000 / limite 2025-12-31 | 2025-12-31 à 14:00 | 800 | non (heure ignorée) |
| seuil 10 000 / limite 2025-12-31 | 2026-01-01 | 800 | oui (dès 2026 tous les montants) |
| seuil 10 000 / limite 2025-12-31 | 2026-02-01 | 0,01 | oui |
| seuil 10 000 / limite 2025-12-31 | 2023-06-30 | 1 000 000 | non (avant la loi) |
| seuil 10 000 / limite null | 2025-04-23 | 800 | oui (limite statutaire 2024-12-31) |
| seuil 10 000 / limite null | 2024-12-31 | 800 | non |
| seuil null / limite 2025-12-31 | 2025-04-23 | 800 | non (seuil statutaire 10 000 jusqu'à la limite société) |
| seuil null / limite 2025-12-31 | 2025-04-23 | 10 000,01 | oui |
| seuil 5 000 / limite 2025-12-31 | 2025-04-23 | 5 000,00 | non |
| seuil 5 000 / limite 2025-12-31 | 2025-04-23 | 5 000,01 | oui |
| seuil 20 000 / limite 2024-12-31 | 2024-12-31 | 15 000 | non (seuil relevé) |
| société A réglée | société B, 2025-04-23 | 800 | B : oui (A n'affecte pas B) |
| seuil + `DateMiseEnRoute` = 2025-06-01 | 2025-04-23 | 50 000 | non (mise en route plus large) |
Propriétés : bandeau, en-tête d'export et DTO de sélection partagent la même définition `EcartRegleDgi` ; un réglage égal au statutaire (10 000 / 2024-12-31) n'affiche aucun bandeau ; seuil `NULL` et limite `NULL` simultanés refusés (400) ; deux `PUT` concurrents : dernier écrit gagne, chaque écriture historisée.

## Livrables
Script SQL, code, tests, front, `VERIFY/TASK-222_verify.md` : réponses du PO aux décisions D, E, H, I, J, K ; checklist avec preuve datée par critère (méthode + date) ; captures du bandeau (3 emplacements), de la fenêtre de réglage et de l'export avec en-tête ; liste exportée des échéances exclues pour la société concernée (étape 5) à signer par le PO ; preuve de la double exécution du script.

## Critères de validation
- Les scénarios et le plan de tests passent ; aucun littéral de seuil dupliqué hors constantes statutaires (`grep`).
- `dotnet build` + `dotnet test` + lint/build front + Playwright : 0 erreur ; checklist UI cochée par critère avec méthode et date.
- Script SQL rejouable sans effet, ne crée que les 2 nouvelles tables ; aucune table existante modifiée.
- Bandeau et en-tête d'export affichés si et seulement si `ecartRegleDgi` ; liste des échéances exclues fournie au PO.

## Risques / dépendances
- **Exposition légale** : ignorer les factures émises en 2025 de montant TTC <= 10 000 contredit la note DGI 734 et l'annonce du 21/03/2025 ; amende de 5 000 MAD par facture manquante ou inexacte lorsque la déclaration est insuffisante ou incomplète (NC 734 p.7). Dès 2026 le client déclare tout : l'écart est borné aux factures de 2025. Par défaut la règle reste DGI ; l'écart est un choix explicite, tracé (utilisateur, date, motif, historique), visible à l'écran et dans l'export.
- **Cohérence avec l'historique** : une échéance déjà déclarée (ex. facture de 800 MAD du 23/04/2025, si elle figure dans l'annuelle 2025 déposée avant le 01/04/2026) cessera d'être alimentée dès que le réglage s'applique : plus aucun incrément de retard, ce qui peut rendre les déclarations suivantes incomplètes pour cette facture (décision I). Les déclarations déjà déposées ne sont pas modifiées.
- **Déploiement** : tant que le client n'a pas la nouvelle version et le script SQL, aucun contournement n'existe dans l'application ; sans les tables, la règle DGI s'applique (avertissement journalisé).
- **Conflits de merge** : `ControleLignesDelaiPaiementPanel.tsx`, `DeclarationsDelaiPaiementPanel.tsx` (TASK-221, TASK-201, TASK-178), `MiseEnRouteDelaiPaiementModal.tsx` (TASK-220), `DeclarationTVA.sql` (TASK-126), composant partagé `ApbsGrid`/`gridExport.ts` ; `IN_PROGRESS/` était vide le 08/10/2026 : revérifier au démarrage.
- Le seuil est exprimé en devise société (le filtre `DE_Id` de la sélection l'impose déjà) : à écrire dans l'aide de la fenêtre.
