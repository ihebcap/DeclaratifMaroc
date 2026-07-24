# TASK-163 — Export Excel : remplacer le code Mode Paiement (2/3/4...) par son libellé métier

## Contexte
Signalement PO (23/07/2026) : la colonne « Mode Paiement » des exports Excel affiche un code brut
(« 2 », « 3 », « 4 »...) au lieu d'un intitulé compréhensible.

**Constat code (cartographie architecte)** : le champ `ModePaiement` (`LigneDeclarationEnrichie.
ModePaiement`, `Declaration.Core/Model.cs:61`) n'est affiché nulle part dans les grilles front réelles
(le seul composant qui l'expose, `ControlGrid.tsx`, tourne sur des données mock — `mockData.ts`/
`mockServer.ts` — jamais branché à l'API réelle, `LigneCandidateDto` ne l'expose pas). L'unique endroit
où ce code brut atteint réellement l'utilisateur est la colonne « Mode Paiement » des deux feuilles
Excel de `Declaration.Export.Excel/Exporter.cs` (`CreerFeuilleDetail` et `CreerFeuilleFacturesControle`,
même trou dupliqué que TASK-162).

Ce code n'est pas un code Sage brut : c'est déjà le **code Simpl-TVA** (nomenclature DGI), calculé par
`GrfEnums.MapperModePaiementSimplTVA()` (`Declaration.Selection/GrfEnums.cs:52-67`) à partir du code de
règlement Sage réel (`RT_MOUVEMENT.MV_Type`, aliasé `ModePaiementId`) — **le même** code que celui
sérialisé tel quel dans le `<mp><id>` du XML de dépôt légal DGI
(`Declaration.Export.Xml/DeclarationXmlExporter.cs:60/76`).

## ⚠️ Risque documenté et assumé par le PO (décision actée 23/07/2026)
`GrfEnums.MapperModePaiementSimplTVA` porte, depuis son écriture, le commentaire `// TODO: Ajuster avec
les vrais MP_Id de GRF` — la correspondance `RT_MOUVEMENT.MV_Type` (Sage) → code Simpl-TVA **n'a jamais
été vérifiée** contre des données réelles. De plus, `DeclarationXmlExporter.MapModePaiement`
(ligne 107-118) porte un **second commentaire** donnant une numérotation différente pour le même jeu de
codes (Virement=4/Traite=5/Autre=7 au lieu de Virement=3/Effet=4/Autres=6 dans `GrfEnums`) — sans
conséquence sur le XML produit aujourd'hui (la fonction est un passe-plat identité pour toute entrée déjà
numérique), mais preuve qu'aucune table de référence unique n'a jamais été validée.

**Décision PO explicite (23/07/2026)** : afficher le libellé dès maintenant en réutilisant la table
`GrfEnums.MapperModePaiementSimplTVA` telle quelle (1=Espèce, 2=Chèque, 3=Virement, 4=Effet,
5=Compensation, 6=Autres), **sans attendre la vérification** de cette table contre les vraies valeurs
`MV_Type` — risque assumé : si la table s'avère fausse une fois vérifiée, le libellé affiché sera
erroné jusqu'à correction. Non traité dans cette task : la vérification `MV_Type` réelle (cf. Réserve
ci-dessous, à cadrer séparément si le PO le souhaite).

## Périmètre STRICT
- **Inclus** :
  1. Nouvelle fonction utilitaire dans `Declaration.Core` (partagé par `Declaration.Selection` **et**
     `Declaration.Export.Excel`, qui référencent déjà tous les deux `Declaration.Core` — évite toute
     nouvelle dépendance croisée Export.Excel → Selection) : `LibelleModePaiementSimplTVA(string code)`
     → « Espèce »/« Chèque »/« Virement »/« Effet »/« Compensation »/« Autres », avec commentaire
     renvoyant explicitement à `GrfEnums.MapperModePaiementSimplTVA` comme **unique producteur** du
     code interprété (les deux tables doivent rester synchronisées si l'une est corrigée après
     vérification `MV_Type`, cf. réserve). Fallback : code inconnu/vide → retourne le code brut tel
     quel (jamais d'exception, jamais de valeur inventée).
  2. `Declaration.Export.Excel/Exporter.cs` : `CreerFeuilleDetail` et `CreerFeuilleFacturesControle`
     appellent cette fonction pour la cellule « Mode Paiement » au lieu d'écrire `ligne.ModePaiement`
     brut.
  3. Tests : `Declaration.Core.Tests` (nouvelle fonction), `Declaration.Export.Excel.Tests` (libellé
     attendu en cellule, pas le code brut).
- **Exclu** :
  - Toute modification de `GrfEnums.MapperModePaiementSimplTVA` ou de `DeclarationXmlExporter.
    MapModePaiement` — le code Simpl-TVA envoyé à la DGI dans le XML **reste inchangé**, seul
    l'affichage Excel change.
  - Toute vérification des vraies valeurs `MV_Type` (cf. Réserve) — risque explicitement assumé par le
    PO pour cette task.
  - Le front (`ControlGrid.tsx` reste un composant mock non branché, hors périmètre).

## Étapes
1. `Declaration.Core` : ajouter `LibelleModePaiementSimplTVA(string code)`.
2. `Exporter.cs::CreerFeuilleDetail` : cellule « Mode Paiement » = libellé.
3. `Exporter.cs::CreerFeuilleFacturesControle` : idem.
4. Tests mis à jour/ajoutés (assertions sur le libellé, pas le code).
5. `VERIFY/TASK-163_verify.md` : `.xlsx` d'exemple montrant les libellés sur les deux feuilles pour
   plusieurs codes (1 à 6 si données de test disponibles) + build/tests rejoués.

## Livrables
- `Declaration.Core` : nouvelle fonction de libellé (partagée, un seul point de vérité pour l'affichage).
- `Exporter.cs` modifié (2 méthodes).
- Tests mis à jour.
- `VERIFY/TASK-163_verify.md`.

## Critères de validation
- Colonne « Mode Paiement » affiche un intitulé métier (jamais un chiffre nu) sur les deux feuilles
  Excel concernées, code inconnu/vide affiché tel quel (jamais d'exception).
- Aucun changement du code Simpl-TVA envoyé dans le XML de dépôt légal (`DeclarationXmlExporter`
  intact, non-régression `Declaration.Export.Xml.Tests`).
- Build + tests rejoués (`Declaration.Core.Tests`, `Declaration.Export.Excel.Tests`).

## ⚠️ Réserve non bloquante — table `MV_Type` → Simpl-TVA jamais vérifiée (à cadrer séparément)
Rappel du risque assumé ci-dessus : `GrfEnums.MapperModePaiementSimplTVA` reste un mapping provisoire
(TODO d'origine, jamais levé) entre le vrai code Sage `RT_MOUVEMENT.MV_Type` et le code Simpl-TVA — ce
même code alimente aussi le XML de dépôt légal DGI (risque fiscal, pas seulement d'affichage). Si le PO
souhaite lever ce risque, une task dédiée serait nécessaire : confronter quelques règlements réels
connus (Espèce/Chèque/Virement/Effet) à leur `MV_Type` en base, vérifier/corriger
`GrfEnums.MapperModePaiementSimplTVA`, et réconcilier au passage le commentaire contradictoire de
`DeclarationXmlExporter.MapModePaiement` (numérotation différente pour Virement/Traite/Autres — sans
impact sur le XML actuel car passe-plat identité, mais dette documentaire trompeuse pour tout futur
mainteneur). Non incluse ici sur décision PO explicite (23/07/2026).
