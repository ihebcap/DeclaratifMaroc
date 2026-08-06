# Récap session QA — Déclaration TVA & Délai de paiement (GRF)

Date session : 2026-08-06
Objectif : test QA approfondi par un "expert-comptable" (2 agents en parallèle pilotant l'app via Playwright MCP), avec correction directe du code au fil de l'eau (exception ponctuelle au workflow "Claude ne code pas" du CLAUDE.md racine — confirmée par le PO pour cette session uniquement).

Front : http://localhost:5173/ — API : http://localhost:5000 — login Admin/Admin.

⚠️ Session arrêtée avant la fin par contrainte de quota utilisateur. Les 2 agents ont reçu une consigne de stop propre (finir la modif en cours ou l'annuler, confirmer build OK, envoyer un dernier statut). **Vérifier `git status` et `git diff` avant toute reprise pour voir l'état réel du code.**

---

## Agent 1 — Déclaration TVA (terminé, rapport complet livré)

Périmètre : les 6 déclarations 01→06/2026, écrans Sélection / Vérifier & Intégrer / Déclaration, drills Affectations & Codes activité, export Excel/XML. Conventions cherchées côté TVA : **absentes** (confirmé, le menu TVA n'a pas cet écran).

### Bilan des 6 déclarations
| Période | Règlements | Sélectionnés | Lignes | TVA totale | Contrôle |
|---|---|---|---|---|---|
| 01/2026 | 152 | 151 | 1162 | 341 224,17 | 3 anomalies bloquantes (achats) |
| 02/2026 | 127 | 126 | 750 | 274 708,84 | écart -26 122,84, 2 bloquantes (ventes) |
| 03/2026 | 104 | 102 | 558 | 236 847,34 | écart -33 077,55, 2 bloquantes (ventes) |
| 04/2026 | 172 | 169 | 1023 | 328 483,15 | écart -6 651,99, 2 bloquantes (achats) |
| 05/2026 | 150 | 144 | 735 | 276 975,36 | **OK** → intégrée, clôturée, XML généré (TVA due 58 601,02 MAD) |
| 06/2026 | 232 | 224 | 844 | 358 106,77 | à contrôler |

### 3 problèmes les plus graves
1. **Faux zéro silencieux** — migration TASK-198 (`DM_LGTVA.CodeTaxe`) jamais appliquée sur `GR_EMA_DISTRIBUTION` → API 500 masquée par "Aucune affectation / 0,00 MAD / Équilibre validé". **Corrigé** (ALTER TABLE appliqué + bandeau d'erreur persistant).
2. **XML SIMPL-TVA rejetable par la DGI** : sur 05/2026, 157/157 lignes du relevé de déductions avaient `<dpai>` hors période (date de pièce en juin pour une déclaration de mai), `<des>` vide (codes activité jamais renseignés), `<identifiantFiscal>123456</identifiantFiscal>` factice non contrôlé. Alerte visible ajoutée (correctif 16). **Décisions PO prises en fin de session (à vérifier si appliquées, cf ci-dessous) : `<dpai>` = MV_Date si mode espèces, sinon date de rapprochement.**
3. **Verdicts verts contradictoires avec bouton grisé** sur Vérifier & Intégrer (verdict calculé par onglet actif, bouton bloqué par les deux onglets sans le dire). **Corrigé.**

### Journal des correctifs (catégorie 1 — bugs corrigés), fichiers touchés
- Base `GR_EMA_DISTRIBUTION` : `ALTER TABLE dbo.DM_LGTVA ADD CodeTaxe NVARCHAR(50) NULL` (script déjà livré, migration TASK-198 appliquée)
- `declaration-tva-web/src/AffectationsDrill.tsx` : bandeau d'erreur persistant + bouton retry ; colonne "Base TVA" renommée "TVA facture (avant prorata)" + ajout colonne "Base HT" ; 3 totaux séparés (collectée/déductible/solde) au lieu d'1 seul mélangé
- `declaration-tva-web/src/CreateDeclarationModal.tsx` : aperçu du numéro de déclaration aligné sur la règle réelle du back
- `declaration-tva-web/src/DeclarationList.tsx` : "Type: 0"/"Période: 1" → libellés lisibles + unité MAD + tri chronologique + libellé "TVA totale (collectée+déductible)" avec infobulle "pas la TVA due"
- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` : contradiction verdict/bouton corrigée (onglet fautif nommé) ; "Lignes intégrées (back)" → "Lignes déjà figées en base" ; titre stepper harmonisé "Étape 2 —..." ; écart à signe contradictoire corrigé (tableau renommé "Montants des lignes à l'origine de l'écart")
- `declaration-tva-web/src/ReglementsSelection.tsx` (correctif 20, demande PO) : filtre sur toutes les colonnes (dont Date pièce, Montant, Affecté, Code tiers, Date règlement espèces — nouvelles) ; colonne "Code tiers" ajoutée ; "Intitulé tiers" distinct ; "Date rapprochement" en toutes lettres ; "Date règlement (espèces)" affichée seulement en mode espèces
- `Declaration.Core/ConstructeurDeclaration.cs` : message "DTO non fourni" → message métier actionnable
- `Declaration.Application/Services/DeclarationWorkflowService.cs` : message "anomalie de recalcul" → conséquence explicite ; **+ alerte "date de paiement hors période déclarée" (correctif 16, catégorie 3 implémentée)**
- `declaration-tva-web/src/DeclarationFinalePanel.tsx` : bug de mise en page (flexShrink) corrigé ; bandeau "TVA due / Crédit de TVA" ajouté (chiffre absent jusque-là) ; impasse de téléchargement XML (409 après 1er clic) corrigée ; libellés stepper/pastilles harmonisés
- `declaration-tva-web/src/DomainGrid.tsx` : infobulles sur actions groupées (Intégrer/Exclure/Reporter/Réinitialiser) ; **bandeau "N lignes sans code activité" (correctif 12, catégorie 3 implémentée — 100% des lignes de 02/2026 étaient concernées)**
- `Declaration.Export.Excel/Exporter.cs` + test associé : "Total Deductible" → "Total Déductible" (3/3 tests verts)

### Catégorie 3 — propositions NON implémentées (documentées pour TASK future)
1. ~~Rattrapage des règlements antérieurs non déclarés~~ — **ANNULÉ par le PO en fin de session** : ne pas implémenter (risque de re-déclarer à tort des règlements déjà couverts par un processus externe chez un client ayant un historique pré-existant). À garder uniquement comme option de config non activée par défaut si jamais redemandé.
2. Contrôle bloquant sur l'identifiant fiscal société avant génération XML (évite d'exporter un IF factice comme "123456")
3. Contrôle sur IF/ICE tiers manifestement invalides (le contrôle "ICE manquant" existe mais ignore les valeurs factices type "12345678")
4. Alerte non bloquante "date de pièce postérieure à la date de rapprochement" (cas RC26040045 — erreur de saisie Sage confirmée par le PO, non prioritaire)
5. Afficher le montant de TVA dans le drill "Codes activité" (actuellement ventilation à l'aveugle sur HT/taux/TTC)
6. Étiqueter visuellement les avoirs (montants négatifs) dans les grilles
7. Scinder le bouton "Toutes les lignes / Resynchroniser" en deux actions distinctes explicites
8. Renommer les en-têtes cryptiques "Orig. / Conf. / Incoh." dans le drill Affectations
9. Excel : taux en "20 %" plutôt que "20,000000 (C20)", échéances avec année complète

### Décisions du PO prises en fin de session — STATUT CONFIRMÉ AU STOP (agent 1 a répondu avant d'arrêter)
- **`<dpai>` (date de paiement XML)** : règle validée par le PO — si mode règlement = ESPÈCES → utiliser MV_Date (date du règlement) ; sinon → date de rapprochement. **NON IMPLÉMENTÉ** : l'agent n'avait fait que de la lecture (Model.cs, DeclarationWorkflowService.cs) en préparation du correctif, aucune modification commencée. Reste entièrement à faire par le prochain agent. Le garde-fou (correctif 16, alerte "date hors période") reste actif en attendant, donc pas de dépôt à l'aveugle possible.
- **Rattrapage** : confirmé annulé côté PO, et confirmé jamais commencé côté agent (rien à revert) — cohérent, rien à faire.

### État de compilation confirmé au moment du stop (agent 1)
- Front `tsc -b` : OK, 0 erreur
- API `dotnet build` : OK, 0 erreur/warning, binaire en exécution à jour (aucun fichier C# modifié depuis ce build)
- Tests `Declaration.Export.Excel.Tests` : 3/3 verts
- Bilan : 20 correctifs livrés et vérifiés (14 bugs, 4 refontes UX, 3 contrôles métier ajoutés), 6 déclarations TVA créées (05/2026 clôturée + XML généré)

### Point métier confirmé par le PO (non un bug)
Codes activité : listes bien distinctes achats (39 codes, série 133+) / ventes (57 codes, série 100+) — comportement normal, pas de bug malgré la présence du drill sur les deux onglets.

---

## Agent 2 — Délai de paiement (STOP confirmé, rapport final complet reçu)

Périmètre : menu DÉCLARATION > Délai de paiement, 3 onglets (Déclarations / Sélection-Contrôle / Conventions). Déclaration existante trouvée : DDP26080001, annuelle 2026, statut Clôturée, 1125 lignes.

### Correctifs confirmés jusqu'au dernier point d'avancement reçu
- **Faux zéro silencieux** (même famille que TVA) : écran Contrôle affichait "Aucune ligne hors délai" pour toutes les périodes testées à cause du mécanisme anti-double-déclaration (les échéances déjà rattachées à DDP26080001 ne remontent plus). Corrigé : back expose `nombreEcheancesDejaDeclarees` + `derniereBorneDejaDeclaree`, écran distingue 3 états (filtres actifs / déjà déclaré / réellement aucun retard).
- Totaux ajoutés (écran Contrôle + fiche déclaration), rattachés explicitement aux lignes affichées
- Filtres ajoutés sur la fiche déclaration (Fournisseur/Facture/État de règlement) — 1125 lignes s'affichaient sans aucun filtre
- Message d'erreur trompeur corrigé (panne réseau affichait à tort "Déclaration introuvable" → distingue 404/403/erreur serveur/serveur injoignable)
- Note fichier partagé : 2 champs OPTIONNELS ajoutés à `SelectionDdpDto` dans `api.ts` (purement additif, ne touche aucun symbole TVA)
- A redémarré l'API partagée une fois pour débloquer son propre build (agent 1 informé)
- A dû ouvrir un onglet Playwright dédié (index 1) suite à une collision d'onglet avec l'agent 1

### Vérification règle métier "règlement partiel → solde en déclaration ultérieure"
**Confirmé implémenté et correct** (code + tests, 26/26 verts) :
- Échéance partiellement réglée = scindée en une ligne par affectation (montant réel payé) + une ligne "solde restant" tant que non soldée
- Le solde réapparaît en déclaration ultérieure avec le montant du solde SEUL (pas la facture entière)
- Pas de double comptage des jours de retard (borne incrémentale sur la dernière période déjà déclarée pour cette échéance)
- N'a pas pu être observé à l'écran (une seule facture à règlements multiples dans les données de test, payée dans les délais)

### Trou réel trouvé (documenté catégorie 3, PAS corrigé — verrouillé par un test)
Si une période de déclaration est SAUTÉE (ex: pas de déclaration T1, direct T2), le retard des règlements partiels rapprochés PENDANT la période sautée est **définitivement perdu** (jamais déclaré nulle part). Comportement volontaire hérité du legacy (`SelectionDelaiPaiementCalculator.BorneActuelleHorsPeriode` retourne null pour une affectation rapprochée avant le début de période), verrouillé par le test `AnomalieDeux_AffectationRapprocheeAvantLaPeriode_NeProduitPasDeLigne`. **Arbitrage PO nécessaire : faut-il changer ce comportement ?** (Non tranché à ce jour.)

### Harmonisation demandée avec le vocabulaire TVA
Consigne envoyée : aligner le vocabulaire du stepper ("1. Sélection / 2. Vérifier & Intégrer / 3. Déclaration", titres "Étape N — ...") et les standards de présentation (pas de faux zéro silencieux, pas de contradiction verdict/bouton) avec les corrections déjà posées côté TVA. **Statut d'exécution non confirmé au moment du stop.**

### Journal des correctifs — CATÉGORIE 1 (bugs corrigés)
a) **Faux zéro silencieux (grave)** — écran Contrôle affichait "Aucune ligne hors délai" sur TOUTES les périodes 2023-2027 alors que 1459 échéances examinées et 1125 déjà déclarées. Corrigé : 3 états vides distincts (filtres actifs / "Aucun retard NOUVEAU — 1125 échéances déjà déclarées jusqu'au 31/12/2026" / réellement aucun retard). Fichiers : `SelectionDelaiPaiementService.cs`, `DeclarationDelaiPaiementDto.cs`, `api.ts` (2 champs additifs), `ControleLignesDelaiPaiementPanel.tsx`. Vérifié à l'écran.
b) **Message d'erreur trompeur** — un 502/API arrêtée affichait "Déclaration introuvable." → messages distincts 404/403/erreur serveur/serveur injoignable + bouton Réessayer. Fichier : `DeclarationsDelaiPaiementPanel.tsx`.

### Journal des correctifs — CATÉGORIE 2 (refontes UX)
c) Totaux absents → "Total montant affiché" (écran Contrôle) + barre de totaux sur la fiche (total montant, nb non réglées, dépassement max), libellé dynamique selon filtres. Vérifié contre SQL : 8 715 493,87 MAD / 1125 lignes / 624 j — exact.
d) Fiche à 1125 lignes sans aucun filtre → filtres Fournisseur/Facture/État de règlement + "Effacer filtres" + état vide dédié. Testé à l'écran.
e) Icône poubelle nue → libellée "Retirer".
f) **Harmonisation stepper avec TVA** : "1. Sélection des lignes / 2. Vérifier (IF/ICE) / 3. Déclaration", mêmes composants visuels que `DeclarationStepper.tsx` côté TVA, titres "Étape N — ...". **Compile (`tsc -b` 0 erreur) mais NON REVÉRIFIÉ DANS LE NAVIGATEUR — dernière action de la session, arrêtée avant vérification visuelle. À REVÉRIFIER EN PRIORITÉ par le prochain agent.**

### Journal — CATÉGORIE 3 (propositions/constats, non implémentés)
g) **Importations non exclues — règle PO NON IMPLÉMENTÉE.** Aucun filtre "importation" dans tout le back (grep vide). Le filtre devise société ne couvre pas une importation facturée en MAD. Piste : catégorie comptable N_CatCompta (tous les fournisseurs du jeu de test sont "1 = ACHAT AU MAROC", donc non testable en l'état, mais le filtre reste à ajouter dans `SelectionDelaiPaiementRepository.GetEcheancesCandidatesAsync`). **À faire par le prochain agent/TASK.**
h) **RS non masqués tant que non rapprochés — non testable/non implémentable en l'état** : table `RT_LigneDeclarationRas` vide, aucune notion RS dans le domaine DDP actuel. Nécessite une spécification préalable (comment identifier une ligne RS depuis RT_ECHEANCE ?) avant tout code.
i) **Espèces — règle vérifiée, AUCUN faux positif trouvé.** Les espèces sont auto-rapprochées (date = MV_Date), jamais de faux retard pour cause de non-rapprochement bancaire. 44 lignes espèces déclarées = vrais retards vérifiés ligne à ligne. Pas de bug ici.
j) **Avoirs déclarés comme retards — anomalie métier non signalée, non corrigée** : 26 lignes "AV..." + 50 échéances à montant négatif (dont un avoir de -10 080 MAD "3 jours de retard") figurent dans la déclaration. Le seuil légal ≥10 000 MAD n'a aucun sens sur un montant négatif. Fix proposé (exclure `EC_Montant <= 0`) non appliqué — décision de périmètre légal à trancher par le PO.
k) **Trou "période sautée"** (déjà détaillé plus haut) : retard perdu si une période est sautée, comportement legacy verrouillé par test, arbitrage PO nécessaire.

### Règlement partiel / solde — CONFIRMÉ implémenté et correct (détaillé plus haut), rien à faire.

### État de compilation confirmé au stop (agent 2)
- Front `tsc -b` : OK, 0 erreur/warning
- Back `dotnet build Declaration.API` : OK, 0 erreur/warning, API redémarrée et fonctionnelle (200 sur /api/licence/status), aucune modif depuis ce build
- Tests `Declaration.Core.Tests` (SelectionDelaiPaiementCalculatorTests) : 26/26 verts
- Captures : `D:\_vibe\GRF\.playwright-mcp\ddp-02, ddp-04, ddp-06, ddp-07, ddp-08, ddp-09`

### ⚠️ À FAIRE EN PRIORITÉ PAR LE PROCHAIN AGENT
1. **Revérifier visuellement dans le navigateur le stepper harmonisé (correctif f)** — compile mais jamais ouvert à l'écran depuis le dernier changement.
2. Arbitrer et implémenter l'exclusion des importations (g) — règle métier explicitement demandée par le PO, non traitée faute de temps.
3. Arbitrer les avoirs déclarés comme retards (j) et le trou période sautée (k).
4. RS masqués (h) : nécessite d'abord une clarification métier du PO avant tout code (la donnée n'existe pas dans le domaine actuel).

---

## Pour reprendre avec un nouvel agent

1. **Vérifier l'état réel du code** : `git status` + `git diff` dans `D:\_vibe\GRF` pour voir tout ce qui a été modifié par les 2 agents (aucun commit n'a été fait pendant cette session — tout est en modifications non commitées).
2. **Vérifier que l'API et le front compilent** (`dotnet build`, `tsc -b` côté `declaration-tva-web`) avant de continuer quoi que ce soit.
3. **Réclamer le rapport final complet de l'agent 2** (Délai de paiement) — jamais livré, seulement des points d'avancement. Un nouvel agent devra probablement reprendre/finir cet audit depuis le dernier état connu (voir section Agent 2 ci-dessus) plutôt que de tout refaire.
4. **Décisions PO en attente d'implémentation à vérifier/finir** :
   - `<dpai>` conditionnel (espèces = MV_Date, sinon date de rapprochement) — dernière consigne envoyée à l'agent 1 juste avant stop, statut d'application incertain
   - Rattrapage : confirmé annulé (ne pas implémenter, ne pas relancer ce sujet sauf demande explicite)
5. **Arbitrages PO encore ouverts (jamais tranchés)** :
   - Contrôle bloquant sur IF société / IF-ICE tiers factices avant génération XML : à décider si on l'ajoute
   - "Trou" délai de paiement sur période sautée (retard perdu si on saute une période) : à trancher
6. **Ne pas refaire ce qui est déjà fait** : les 6 déclarations TVA 01→06/2026 existent déjà en base (créées pendant ce test), tout comme DDP26080001. Un nouvel agent doit vérifier leur existence avant d'en recréer.
7. Workflow projet normal (CLAUDE.md racine) : une fois cette session de correction directe terminée, les correctifs listés ci-dessus devraient être documentés rétroactivement en TASK/DONE_DETAIL selon le processus habituel du projet — c'était l'intention initiale du PO ("ce journal me servira à documenter rétroactivement les correctifs (TASK/DONE)").
