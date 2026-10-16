# TASK-225 — V2 compta : socle (projets, base DeclaratifMaroc EF Core, migrations auto, configuration dossier)

Status: 🆕 à faire
Priority: HIGH (socle bloquant pour TASK-226 à 230)
Risk: MEDIUM
Module: nouveaux projets `Declaration.Compta.*` + Declaration.API (composition root)
Dépend de : back = aucune ; **écran de paramétrage = TASK-231** (charte xGR, arbitrage PO 16/10/2026)

> **Origine :** PO 16/10/2026 — V2 de la déclaration (TVA + DDP) alimentée par la comptabilité Sage seule.
> Voir `DOCS/CADRAGE_V2_COMPTA_SOURCE_UNIQUE.md` (décisions §1, architecture §5).

## Décisions de conception

1. **Base dédiée `DeclaratifMaroc`**, créée et migrée automatiquement par **EF Core** au démarrage de l'API (`Database.Migrate()`), jamais de script SQL manuel. Aucune table Sage ni winform dans cette base, et **aucune modification** de tables Sage/winform (règle PO absolue).
2. **Partie commune inchangée** : `Declaration.Core`, exports XML/Excel non modifiés. La V2 s'ajoute à côté (adaptateur de source).
3. **Un dossier = une base Sage compta** : configuration par dossier (société) : serveur, nom de base, identifiants lecture seule, **nom de la colonne `F_COMPTET` portant l'IF, nom de celle portant l'ICE**.
4. Identifiants Sage : chiffrés (DPAPI machine), jamais renvoyés en clair par l'API.
5. Noms de colonnes : whitelist `^[A-Za-z0-9_]+$` (patron `IdentiteFiscaleFournisseurConfig.cs`), validés aussi **par existence réelle** dans `F_COMPTET` à l'enregistrement.
6. Connexion Sage : lecture seule (`ApplicationIntent=ReadOnly` quand supporté, compte sans droit d'écriture documenté).

## Périmètre STRICT

- **Inclus** :
  1. Projets : `Declaration.Compta.Contracts` (DTO), `Declaration.Compta.Core` (logique pure), `Declaration.Compta.Reader` (SQL Sage lecture seule, vide à ce stade), `Declaration.Compta.Persistence` (EF Core, `DeclaratifMaromcDbContext`), référencés dans `DeclarationTVA.slnx`.
  2. Entités EF + migration initiale : `Dossier` (id, nom, connexion Sage chiffrée, colonne IF, colonne ICE, dates), `EcartValide` (créée vide ici, remplie en TASK-228), `LigneDeclareeCompta` (créée vide ici, **index unique** (RèglementCbMarq, FactureCbMarq, TauxCode, DossierId), remplie en TASK-230).
  3. Migrations automatiques au démarrage, idempotentes, avec journal clair en cas d'échec (message exploitable, pas d'erreur générique — cf. TASK-209).
  4. Endpoint admin `GET/PUT /api/v2/dossiers/{id}/configuration` + test de connexion Sage (`POST .../test`) : vérifie connexion, présence de `F_ECRITUREC`/`F_TAXE`/`F_COMPTET`/`F_JOURNAUX`, existence des colonnes IF/ICE.
  5. Écran de paramétrage (admin) : formulaire dossier + bouton « Tester ».
- **Exclu** : toute lecture d'écritures (TASK-226), tout calcul, tout appel à OM Sage, tout changement de `DeclarationTVA.sql` (V1 intacte).

## Livrables

- 4 projets + migration EF initiale + endpoint + écran de configuration.
- Tests : migration appliquée sur une base vide (LocalDB), idempotence (2 démarrages), rejet d'un nom de colonne invalide, mot de passe jamais en clair en `GET`.

## Critères de validation

- Démarrage sur serveur sans base `DeclaratifMaroc` → base créée et migrée, sans intervention.
- Test de connexion réussi sur **LOTREX** et **NEW_EMA DISTRIBUTION** ; échec explicite et lisible sur `GOCOM` (pas de `F_ECRITUREC`).
- Aucune écriture SQL dans les bases Sage (vérifier le compte utilisé / absence de commande d'écriture).
- `dotnet build DeclarationTVA.slnx` 0 erreur ; `npm run lint` + `npm run build` 0 erreur ; **checklist UI de `DOCS/UI_STANDARDS.md` cochée** (écran de paramétrage) ; preuve par critère datée dans le VERIFY.

## Files

- `DeclarationTVA.slnx`, `Declaration.API/Program.cs`, `Declaration.API/Controllers/` (nouveau contrôleur v2).
- [Declaration.Selection/IdentiteFiscaleFournisseurConfig.cs](../Declaration.Selection/IdentiteFiscaleFournisseurConfig.cs) — patron de validation.
- `declaration-tva-web/src/` (écran paramétrage, à lire : `DOCS/UI_STANDARDS.md`).
