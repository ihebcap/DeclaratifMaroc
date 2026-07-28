# TASK-133 Verify — DDP : génération du fichier XML/ZIP de dépôt

> Repris un travail substantiel déjà présent sur disque à l'état non commité (fichiers neufs +
> modifications sur les fichiers TASK-127/128/131/132). Après relecture intégrale, le travail était
> déjà correct et complet au regard du périmètre STRICT de la TASK — aucune réécriture, uniquement
> vérification (build, tests, cohérence CDC, cohérence schéma réel). Aucun code n'a été modifié par
> ce passage : c'est un audit qui confirme l'état trouvé, pas une implémentation depuis zéro.

## Périmètre livré

1. **Générateur XML** (`Declaration.Export.Xml/DeclarationDelaiPaiementXmlExporter.cs`) reproduisant
   la structure exacte du legacy `DeclarationDelaisPaiementFileGenerator.cs` : mêmes tags, même ordre
   (`identifiantFiscal` → `annee` → `periode` → `activite` → [`dateJugementOuvrProc`] → `chiffreAffaire`
   → `listeFacturesHorsDelai`), bloc `<dateJugementOuvrProc>` strictement conditionnel à
   `activite=2` (EnProcedure), 3 balises optionnelles (`datePaiementHorsDelai`/`modePaiement`/
   `referencePaiement`) strictement conditionnelles à « payée dans la période ».
2. **Compression ZIP** : le XML seul dans l'archive (`archive.CreateEntryFromFile`), comme le legacy.
3. **Garde « fichier déjà existant »** : `ApplicationException` explicite si le `.xml` OU le `.zip`
   existe déjà, jamais d'écrasement silencieux (`GenererXml`, l.48-51 de l'exporter).
4. **Contrôle IF/ICE branché AVANT écriture** : `DeclarationDelaiPaiementGenerationService.GenererFichierAsync`
   appelle `IDeclarationDelaiPaiementService.VerifierGenerationFichierAutoriseeAsync` en étape 1,
   avant toute construction de modèle ou écriture disque — contrairement au legacy qui validait ligne
   par ligne pendant l'écriture (anomalie CDC §4.4 corrigée, décision déjà actée TASK-132). Re-validation
   en défense en profondeur avant de poser `DDP_IsGeneretedFile` (`MarquerFichierGenereAsync`) ; si
   cette 2ᵉ validation échoue (donnée modifiée entre les deux étapes), les fichiers tout juste écrits
   sont supprimés plutôt que de laisser un orphelin bloquer toute tentative suivante.
5. **Annulation de génération** (`AnnulerGenerationFichierAsync`, legacy `FichierAnnulerGeneration`) :
   remet `DDP_IsGeneretedFile` à faux PUIS supprime les fichiers physiques déjà écrits — amélioration
   assumée par rapport au legacy (qui laissait les fichiers orphelins bloquer toute régénération sur la
   garde « fichier déjà existant », documentée VERIFY TASK-132 §10.4). Idempotente (aucune erreur si
   les fichiers sont déjà absents).
6. **Codes `modePaiement`** conformes au CDC : `1=Espèce, 2=Chèque, 4=Virement, 5=Traite`
   (`DeclarationDelaiPaiementLigneCalculator.CodeEspece/CodeCheque/CodeVirement/CodeTraite`), mode non
   mappé (`Autre`) → balise vide, jamais bloquant.
7. **Dette assumée reproduite telle quelle** (décision PO §5.A-4, non touchée par ce livrable) :
   `<natureMarchandise>` toujours vide (`DeclarationDelaiPaiementXmlExporter.cs` l.114, chaîne littérale
   vide, aucun branchement de `SO_ColValueNatureMarchandise`) ; `<dateLivraisonMarchandise>` = date de
   la facture (`DateLivraisonMarchandise = dateEmission`, `DeclarationDelaiPaiementXmlModele.cs` l.210,
   aucun branchement de `SO_ColValueDateLivraisonMarchandise`). **Les deux mappings optionnels
   mentionnés par la TASK comme « gain rapide si le temps le permet » n'ont PAS été branchés** — je ne
   les ai pas ajoutés non plus, conformément à la consigne de ne pas anticiper/complexifier au-delà du
   périmètre trouvé déjà cohérent avec la décision PO. Documenté ici pour traçabilité, non bloquant.
8. Tests couvrant exactement les points listés par la TASK : structure XML exacte (tags + ordre,
   comparaison champ par champ), les 4 codes `modePaiement`, cas EnProcedure (tag présent, bien
   positionné), cas normal (tag absent), garde fichier XML/ZIP déjà existant, échappement XML (`&`),
   nommage trimestriel/annuel, calcul pur par ligne (solde/montant payé/3 balises optionnelles),
   durcissement assumé (règlement rapproché sans date de rapprochement → traité comme non payé, jamais
   de crash contrairement au legacy).

## Fichiers modifiés / créés (périmètre TASK-133 uniquement)

Nouveaux :
- `Declaration.Application/Services/DeclarationDelaiPaiementGenerationService.cs` — orchestration
  (garde état+IF/ICE → construction modèle → écriture → marquage, annulation, chemins déterministes).
- `Declaration.Core/DeclarationDelaiPaiementXmlModele.cs` — modèle XML pur + `DeclarationDelaiPaiementLigneCalculator`
  (calcul par ligne, sans dépendance base) + `TypeModeReglementDelaiPaiement`.
- `Declaration.Export.Xml/DeclarationDelaiPaiementXmlExporter.cs` — sérialisation XML + ZIP.
- `Declaration.Export.Xml.Tests/DeclarationDelaiPaiementXmlExporterTests.cs` — 13 tests structure/codes/gardes.
- `Declaration.Core.Tests/DeclarationDelaiPaiementLigneCalculatorTests.cs` — 15 tests calcul pur + résolution période XML.
- `Declaration.Orchestration.Tests/Task133GenerationFichierDelaiPaiementTests.cs` — 9 tests d'enchaînement
  (IF/ICE avant écriture, garde fichier existant, échec de marquage après écriture réussie, annulation,
  chemins déterministes) sur la chaîne réelle `DeclarationDelaiPaiementService` + `DeclarationDelaiPaiementGenerationService`.

Modifiés (déjà commités par TASK-127/128/131/132, retouchés pour exposer ce dont TASK-133 a besoin) :
- `Declaration.Application/Entities/DeclarationDelaiPaiementEntities.cs` — `NumRc`/`Adresse` ajoutés à
  `IdentiteFiscaleTiersErp` (non soumis au contrôle bloquant IF/ICE, uniquement pour le XML) + nouvelle
  classe `SocieteDelaiPaiementInfo`.
- `Declaration.Application/Interfaces/IDeclarationDelaiPaiementRepository.cs` — 2 méthodes lecture seule
  ajoutées : `GetSocieteInfoAsync`, `GetTypesModeReglementAsync`.
- `Declaration.Core/DeclarationDelaiPaiementCycleDeVie.cs` — `ResoudrePeriodeXml`/`ResoudrePeriodeFichier`
  (valeur `<periode>` XML jamais égale au `DDP_Periode` brut persisté pour une annuelle, cf. décision
  déjà actée TASK-132 §Décisions n°5).
- `Declaration.Infrastructure/Repositories/DeclarationDelaiPaiementRepository.cs` — implémentation des 2
  méthodes ci-dessus (`SELECT` additifs uniquement, aucune requête existante modifiée) + `SELECT` additifs
  `NumRc`/`Adresse` dans `GetIdentitesFiscalesTiersAsync` (nom de colonne validé par whitelist regex,
  tolérant l'absence de configuration — contrairement à l'IF/ICE qui sont obligatoires).
- `Declaration.Orchestration.Tests/Task132CycleDeVieDeclarationDelaiPaiementTests.cs` — `FakeRepository`
  complété (stubs `GetSocieteInfoAsync`/`GetTypesModeReglementAsync`) pour rester conforme à l'interface
  élargie ; aucune assertion TASK-133 ajoutée ici (couvertes par le fichier de tests dédié).

Fichiers hors périmètre (CHANGELOG.md, TODO.md, DONE.md, LANCEMENT_DEV.md,
declaration-tva-web/.env.development, declaration-tva-web/vite.config.ts,
DOCS/GUIDE_PROCESS_DECLARATION_TVA.html, Declaration.API/Program.cs, VERIFY/TASK-006/007/024,
renommages TASK-186→190) : **non touchés, non commités** — appartiennent au correctif TVA en cours
sur un autre fil de travail.

## BUILD

- Status : **OK**
- `dotnet build DeclarationTVA.slnx` (solution complète) → **0 erreur**, 2 avertissements préexistants
  sans rapport (`NU1510` sur `Declaration.Setup.csproj`, package `System.Text.Encoding.CodePages`).

## Tests exécutés (réellement lancés dans cet environnement, résultats bruts ci-dessous)

- `dotnet test Declaration.Core.Tests` → **215/215 verts** (inclut les 15 tests
  `DeclarationDelaiPaiementLigneCalculatorTests` + `DeclarationDelaiPaiementPeriodeXmlTests` neufs).
- `dotnet test Declaration.Export.Xml.Tests` → **26/26 verts** (inclut les 13 tests
  `DeclarationDelaiPaiementXmlExporterTests` neufs).
- `dotnet test Declaration.Orchestration.Tests` → **225/225 verts** (inclut les 9 tests
  `Task133GenerationFichierDelaiPaiementTests` neufs).

## Vérification schéma réel (contrainte NON NÉGOCIABLE — aucune modification de schéma winform)

TASK-133 ne crée aucune nouvelle table (génération de fichier pure, lecture seule sur des tables déjà
existantes). Point néanmoins vérifié activement dans cet audit (les colonnes `P_SOCIETE`/`P_MODEREGLEMENT`
lues par le code neuf n'étaient référencées nulle part ailleurs dans le repo hors CDC/TASK-133, donc à
risque d'être des noms supposés plutôt que réels) — requêtes `sqlcmd` rejouées contre
`GR_EMA_DISTRIBUTION`/`DESKTOP-5BFKKEP` (accès réel disponible) :

- `P_SOCIETE` : `SO_Identifiant` (nvarchar), `SO_ColValueNumRegistreCommerceFournisseur` (nvarchar),
  `SO_ChiffreAffaire` (decimal), `SO_DateJugement` (datetime), `SO_ActiviteMarroc` (int) — **les 5
  colonnes existent réellement**, aucune n'a été ajoutée par ce travail (confirmées aussi documentées
  dans le CDC `apbs-gr_winform/analayse/CDC-DELAI-PAIEMENT-MAROC.md` §7.1 comme colonnes historiques).
- `P_MODEREGLEMENT` : `MR_Id`/`MR_TypeNo` existent. Mapping réel rejoué : `MR_Id=1→MR_TypeNo=0
  (Espece), 2→1 (Cheque), 9→2 (Traite), 4→3 (Virement), 7→1, 10→4` — **conforme exactement au
  commentaire du code** (`TypeModeReglementDelaiPaiement`) qui annonçait cette correspondance.
- `F_COMPTET` (base Sage `NEW_EMA DISTRIBUTION`) : `CT_Adresse` (varchar) existe — utilisée pour
  `AdresseSiegeSocial`.
- **Aucune modification de schéma effectuée ni nécessaire** — uniquement des `SELECT` additifs sur des
  tables/colonnes déjà en place. Aucune table `DM_*` neuve créée par TASK-133 (les tables d'en-tête/lignes
  `RT_DECLARATIONDELAISPAIEMENT`/`RT_DECLARATIONDELAISPAIEMENTLG` existent déjà, créées par TASK-127/128,
  hors périmètre de ce livrable).

## VALIDATION CHECKLIST (critères de la TASK)

- [x] Générateur XML reproduisant la structure exacte (tags, ordre, codes `modePaiement`).
- [x] Compression ZIP (XML seul dans l'archive).
- [x] Garde « fichier déjà existant » (XML et ZIP testés séparément).
- [x] Contrôle IF/ICE (TASK-132) branché AVANT toute écriture de fichier — validé par test dédié
      (`GenererFichierAsync_IfIceInvalide_LeveAvantEcriture_AucunFichier_FlagResteFaux`).
- [x] Annulation de génération (`FichierAnnulerGeneration`) tant que non déposé — présente, testée,
      idempotente.
- [x] Tests : structure XML exacte, codes `modePaiement`, cas EnProcedure, cas normal, garde fichier
      existant — tous présents.
- [x] Build 0 erreur.
- [x] Tests verts (Core 215/215, Export.Xml 26/26, Orchestration 225/225).
- [x] Aucun bypass sécurité.
- [x] Aucune dette technique silencieuse introduite : la dette assumée `<natureMarchandise>`/
      `<dateLivraisonMarchandise>` est documentée dans le code (commentaires renvoyant à TASK-133) et
      ci-dessus, pas seulement dans la TASK d'origine.

## Reste à valider (non couvert par ce VERIFY)

1. **Comparaison sur un jeu de données réel** : le critère de validation de la TASK mentionne « si
   disponible » une comparaison champ par champ sur des données réelles issues d'une déclaration DDP
   effectivement clôturée. **Aucune déclaration DDP réelle clôturée n'est disponible dans la base de
   dev à ce stade** (le périmètre DDP est neuf, TASK-127 à 132 viennent d'être livrées) — seule une
   comparaison contre des données de test en mémoire a été possible (tests d'orchestration TASK-133).
   Non bloquant pour ce livrable (le critère de la TASK le conditionnait explicitement à la
   disponibilité des données), mais à refaire dès qu'une déclaration réelle existera.
2. **Cycle complet dépôt réel** : ce périmètre exclut explicitement le dépôt (flag géré par TASK-132) et
   l'UI (TASK-134) — aucun test de bout en bout via l'API HTTP n'a été fait ici (TASK-133 n'expose
   aucun endpoint, cf. périmètre strict de la TASK). À vérifier une fois TASK-134 câblé.
3. Le commentaire de code de `TypeModeReglementDelaiPaiement` annonçait déjà la correspondance
   `MR_Id→MR_TypeNo` vérifiée au point précédent ; je l'ai revérifiée indépendamment plutôt que de la
   supposer exacte sur la seule foi du commentaire — confirmée conforme.

## Notes

- Le travail trouvé sur disque (non commité) était déjà substantiellement complet et conforme à la
  fois au CDC legacy et aux décisions PO actées dans la TASK. Mon rôle ici a été un audit de
  conformité + vérification schéma réel + build/tests, pas une réécriture. Aucune ligne de code
  fonctionnelle n'a été modifiée par ce passage.
- Périmètre respecté strictement : aucun endpoint API, aucun changement front, aucun anticipation de
  TASK-134/136.
