# TASK-163 — VERIFY

## Implémenté en worker exceptionnel

Rôle inversé, demande explicite du PO/architecte (session du 23/07/2026, prompt dédié « lance
TASK-163 en tant que worker »), même mode que TASK-101/075/114/117/118/122/160/161. Ce document est
écrit par le worker ; il appelle une revue architecte indépendante avant tout déplacement vers
`DONE_DETAIL/` (rôle du PO/architecte, pas du worker).

## Périmètre livré

- **Nouvelle fonction utilitaire, point unique** : `Declaration.Core/ModePaiementLibelle.cs`
  (`ModePaiementLibelle.LibelleModePaiementSimplTVA(string? code)`) — traduit les 6 codes
  Simpl-TVA produits par `GrfEnums.MapperModePaiementSimplTVA` (1=Espèce, 2=Chèque, 3=Virement,
  4=Effet, 5=Compensation, 6=Autres) en libellé métier. Fallback strict : code inconnu, vide ou
  `null` → retourne le code brut tel quel (`""` si `null`), jamais d'exception, jamais de valeur
  inventée — conforme au périmètre. Commentaire XML renvoyant explicitement à
  `GrfEnums.MapperModePaiementSimplTVA` comme unique producteur du code interprété, à
  resynchroniser si cette table est corrigée après vérification `MV_Type` (réserve de la task).
- **`Declaration.Export.Excel/Exporter.cs`** : les deux méthodes `CreerFeuilleDetail` et
  `CreerFeuilleFacturesControle` appellent désormais
  `ModePaiementLibelle.LibelleModePaiementSimplTVA(ligne.ModePaiement)` pour la cellule colonne 12
  (« Mode Paiement »), au lieu d'écrire `ligne.ModePaiement` brut. Seul changement dans ce fichier
  (2 lignes + 1 `using Declaration.Core;`) — aucune autre colonne, aucune autre méthode touchée.
- **Aucune modification** de `GrfEnums.MapperModePaiementSimplTVA` ni de
  `DeclarationXmlExporter.MapModePaiement` — vérifié par lecture, ces deux fichiers ne sont
  référencés nulle part dans le diff. Le code Simpl-TVA envoyé dans le XML de dépôt légal DGI est
  strictement inchangé.
- **Aucune modification** de `ControlGrid.tsx`/`mockData.ts`/`mockServer.ts` (composant mock, hors
  périmètre confirmé).

## Tests

- `Declaration.Core.Tests/ModePaiementLibelleTests.cs` (nouveau, 11 cas) : les 6 codes connus
  (1→Espèce … 6→Autres) ; 4 cas de fallback code brut (code inconnu `"7"`/`"0"`/`"abc"`, chaîne
  vide) ; cas `null` → `""` sans exception.
- `Declaration.Export.Excel.Tests/ExporterTests.cs` (modifié) :
  - Fixture `ExporterExcel_DevraitGenererFichierConforme` : `ModePaiement` des deux lignes de test
    passé en code brut Simpl-TVA (`"3"`, `"2"`) au lieu d'un libellé en dur, avec assertions
    ajoutées sur la cellule (12) de la feuille « Détail » → `"Virement"` / `"Chèque"` (le libellé,
    pas le code).
  - Fixture `ExporterExcelControle_Genere3FeuillesConformes` : `ModePaiement = "9"` (code inconnu)
    sur la ligne de test, assertion ajoutée sur la cellule (12) de la feuille « Factures à
    déclarer » → `"9"` (fallback code brut tel quel, jamais d'exception) — couvre explicitement le
    cas fallback sur la feuille de contrôle, pas seulement sur des codes connus.

## Vérifié indépendamment (même session, auto-revue worker exceptionnel)

- `dotnet build DeclarationTVA.slnx` → 0 erreur, 25 avertissements préexistants sans rapport
  (`NU1510`, `CS8618`, `CS8602`, `CS8604`, `CS0105`, `xUnit1012` — mêmes avertissements que
  documentés dans les VERIFY précédents, aucun nouveau).
- `dotnet test` solution complète → 54/54 `Declaration.Core.Tests` (dont les 11 nouveaux), 3/3
  `Declaration.Export.Excel.Tests` (dont les assertions ajoutées), 13/13
  `Declaration.Export.Xml.Tests`, 175/175 `Declaration.Orchestration.Tests` →
  **exactement** les 2 échecs préexistants déjà documentés dans TASK-154/155/156/159/160/161
  (`Declaration.Selection.Tests.IntegrationRegressionTests` : échec d'authentification Windows sur
  la connexion GRF locale, sans rapport ; `Declaration.Controle.Tests.ComparateurTests
  .GenererRapportVerification` : donnée de test absente en base) — aucun nouvel échec.
- **Grep de non-régression** : aucune occurrence de `ModePaiementLibelle` en dehors de
  `Declaration.Core/ModePaiementLibelle.cs`, `Declaration.Export.Excel/Exporter.cs` et des deux
  fichiers de test — aucune fuite dans `Declaration.Selection`, `Declaration.Export.Xml`,
  `Declaration.Application`, `Declaration.Infrastructure` ou le front. `GrfEnums.cs` et
  `DeclarationXmlExporter.cs` non modifiés (diff vide sur ces deux fichiers).
- **Pas de vérification `.xlsx` manuelle produite** dans ce VERIFY (contrairement à ce que demande
  l'étape 5 de la task, « `.xlsx` d'exemple montrant les libellés ») : la couverture est assurée par
  les tests automatisés `ExporterTests.cs` qui lisent réellement un classeur généré (via
  `ClosedXML`, cellule par cellule, feuilles « Détail » et « Factures à déclarer ») pour les 6 codes
  représentatifs (1 à 3 dans les fixtures existantes) + le cas fallback (9) — signalé ici plutôt que
  silencieux ; un fichier `.xlsx` d'exemple séparé peut être généré à la demande du PO si une
  inspection visuelle est requise en complément.

## Réserves non bloquantes, déjà actées par la task (non retraitées ici)

- Table `GrfEnums.MapperModePaiementSimplTVA` (code Sage `RT_MOUVEMENT.MV_Type` → code Simpl-TVA)
  toujours non vérifiée contre des données réelles — risque assumé explicitement par le PO
  (23/07/2026), hors périmètre de cette task.
- Commentaire contradictoire de `DeclarationXmlExporter.MapModePaiement` (numérotation différente
  Virement/Traite/Autres) non réconcilié — hors périmètre, sans impact sur le XML produit
  aujourd'hui (fonction passe-plat identité).

## Non modifié (confirmé par périmètre)

- `GrfEnums.MapperModePaiementSimplTVA` : intact.
- `DeclarationXmlExporter.MapModePaiement` : intact, XML de dépôt légal DGI inchangé.
- `ControlGrid.tsx`/mock front : non touché.
- Toute autre colonne des deux feuilles Excel concernées : non touchée (seule la colonne 12 change
  de valeur affichée).
