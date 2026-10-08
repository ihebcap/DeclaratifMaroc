# TASK-222 — Seuil d'exclusion DDP paramétrable par société (défaut = règle DGI)

RISK : HIGH (périmètre déclaratif légal, écart volontaire à la règle DGI pour une société) — discipline de preuve par critère exigée dans le VERIFY.
Dépendance : **TASK-221** (borne `>` dans `EstEligibleSeuilLegal`) à livrer avant ou dans la même session ; les deux TASKS touchent la même méthode.

## Contexte
Demande PO (08/10/2026), urgente (un client est bloqué) : pour **un client précis**, ignorer **toutes** les factures dont le montant
est inférieur à 10 000, quelle que soit leur date. Cela s'écarte de la règle DGI, qui exempte seulement les factures émises avant le
01/01/2025 (note circulaire n°734 §O-2 ; annonce DGI du 21/03/2025, finances.gov.ma `fiche=7218` : « l'amende pécuniaire s'applique à
toutes les factures émises à compter du 1er janvier 2025, y compris celles dont le montant est inférieur ou égal à 10 000 dirhams »).
Le PO a pris connaissance de ces textes ; la décision d'écart lui appartient.

Aujourd'hui aucun réglage n'existe : le seuil est une constante statutaire (`SeuilsLegauxDelaiPaiement`,
`Declaration.Core/SelectionDelaiPaiementCalculator.cs:14-33`, TASK-131). La date de mise en route société (TASK-220) n'est **pas
équivalente** : elle exclut toutes les factures antérieures à une date, quel que soit leur montant.

Une paramétrisation existe déjà pour la date de mise en route : table `DM_PARAM_DELAIPAIEMENT_SOCIETE` (propriété GRF, créée par
`DeclarationTVA.sql:543-550`), `DelaiPaiementBootstrapRepository`, `DelaiPaiementParametrageController`
(`PUT parametrage/{soId}`), fenêtre front `MiseEnRouteDelaiPaiementModal.tsx`. Le seuil s'y greffe.

## Objectif
```
Entrée  : société sans réglage -> comportement DGI (TASK-221) ; société avec seuil 10 000 et date limite 9999-12-31.
Traitement : le seuil et sa date limite deviennent des valeurs par société ; null = valeurs statutaires.
Sortie  : pour la société réglée, toute échéance de montant <= seuil est exclue du contrôle DDP, à toute date ;
          les autres sociétés sont strictement inchangées ; l'écart est visible à l'écran.
```

## Périmètre STRICT
- **Inclus** :
  1. Stockage : 2 colonnes **nullables** sur `DM_PARAM_DELAIPAIEMENT_SOCIETE` (table propriété GRF, pas une table winform) :
     `SeuilMontant` `decimal(18,2) NULL` et `SeuilDateLimite` `date NULL` ; ajout idempotent
     (`IF COL_LENGTH('dbo.DM_PARAM_DELAIPAIEMENT_SOCIETE','SeuilMontant') IS NULL ALTER TABLE ... ADD ...`) dans `DeclarationTVA.sql`
     (script exécuté à l'installation/mise à jour). Aucune autre table touchée.
  2. Règle : `null` ⇒ valeurs statutaires (10 000 ; `DateLimiteSeuilMontant` 2024-12-31). Sinon l'échéance est exclue si
     `DoDate <= SeuilDateLimite` ET `montant <= SeuilMontant` (même borne que TASK-221 : `montant > seuil` requis pour être retenue).
     `DateDebutDeclarationLoi` (2023-07-01) reste appliquée dans tous les cas. Pour le client concerné : `SeuilDateLimite = 9999-12-31`.
  3. Branchement : le service de sélection résout les valeurs de la société et les passe **aux deux endroits** (requête SQL
     `GetEcheancesCandidatesAsync`, déjà paramétrée, et `ParametresSelectionDelaiPaiement` pour la réapplication défensive du
     calculateur) : une seule source de vérité, aucun littéral dupliqué.
  4. API : étendre `GET/PUT parametrage/{soId}` ; modification réservée aux administrateurs (claim `UT_Admin`, comme la logique
     d'autorisation existante) ; validation : `SeuilMontant > 0`, `SeuilDateLimite >= 2023-07-01` ; `UT_Id`/`DateSaisie` existants
     renseignés ; journalisation (ancienne/nouvelle valeur, utilisateur) au niveau Information.
  5. Front : champs dans `MiseEnRouteDelaiPaiementModal.tsx` avec bouton « Revenir à la règle DGI » (remet null) et avertissement
     explicite quand le réglage s'écarte de la règle DGI ; **bandeau visible sur l'écran Contrôle DDP et en tête de l'Export Excel**
     tant qu'un seuil personnalisé est actif (« Seuil d'exclusion personnalisé : montants <= X exclus jusqu'au Y — différent de la règle
     DGI »). Conformité `DOCS/UI_STANDARDS.md` (lire et appliquer).
- **Exclus** : valeurs par défaut/DGI ; TASK-220 ; seuil par facture (somme des échéances) ; écran de TASK-211 ; tout changement de
  schéma sur une table winform ; purge/correction de déclarations déjà déposées.

## Étapes
1. Lire `DOCS/UI_STANDARDS.md`, le modal existant, le contrôleur et le repository de paramétrage.
2. Script SQL idempotent + entité/repository (lecture/écriture des 2 colonnes).
3. Règle d'éligibilité paramétrée (C#) + passage des valeurs au SQL ; tests unitaires.
4. API (autorisation admin, validation, journal) ; front (champs, avertissement, bandeau, export).
5. `dotnet build DeclarationTVA.slnx` ; tests ; `npm run lint` + `npm run build` ; checklist UI de `DOCS/UI_STANDARDS.md`.

## Scénarios de test (après TASK-221 ; DateMiseEnRoute non configurée)
| Société | DoDate | Montant | Attendu |
|---|---|---|---|
| sans réglage | 2025-04-23 | 800 | oui (règle DGI : factures de 2025 déclarées) |
| sans réglage | 2024-12-31 | 9 999,99 | non |
| seuil 10 000 / limite 9999-12-31 | 2025-04-23 | 800 | non |
| seuil 10 000 / limite 9999-12-31 | 2026-02-01 | 800 | non |
| seuil 10 000 / limite 9999-12-31 | 2026-02-01 | 10 000,00 | non (borne exclue) |
| seuil 10 000 / limite 9999-12-31 | 2026-02-01 | 10 000,01 | oui |
| seuil 10 000 / limite 9999-12-31 | 2023-06-30 | 1 000 000 | non (avant la loi) |
| seuil renseigné, limite null | 2025-04-23 | 800 | oui (limite = statutaire 2024-12-31) |
+ SQL et C# donnent le même résultat sur ces cas ; une société A réglée n'affecte pas la société B ; un utilisateur non admin reçoit 403 ;
valeurs invalides rejetées ; bandeau et en-tête d'export affichés si et seulement si un écart est actif.

## Livrables
Script SQL, code, tests, front, `VERIFY/TASK-222_verify.md` : checklist avec preuve datée par critère, capture du bandeau et de la
fenêtre de réglage, mesure sur la base du client (nombre de lignes avant/après).

## Critères de validation
- Les scénarios passent ; aucun littéral de seuil dupliqué hors constantes statutaires.
- Build .NET + tests + lint/build front : 0 erreur ; checklist UI cochée.
- Le script SQL est rejouable sans effet (idempotent) et ne touche que `DM_PARAM_DELAIPAIEMENT_SOCIETE`.

## Risques / dépendances
- **Exposition légale** : ignorer les factures de 2025 et après de montant <= 10 000 contredit la note DGI 734 et l'annonce du
  21/03/2025 ; amende de 5 000 MAD par facture manquante ou inexacte (NC 734 p.7). Par défaut la règle reste DGI ; l'écart est un
  choix explicite, tracé (utilisateur, date), visible à l'écran et dans l'export.
- **Cohérence avec l'historique** : une échéance déjà déclarée (ex. facture de 800 MAD du 23/04/2025, déclarée au 31/12/2025 dans
  l'annuelle 2025) cessera d'être alimentée dès que le réglage s'applique : plus aucun incrément de retard pour elle.
- **Déploiement** : le client doit recevoir la nouvelle version et le script SQL (mise à jour d'installation) avant tout réglage ;
  tant que ce n'est pas fait, aucun contournement n'existe dans l'application.
- **Point à confirmer par le PO** : l'ajout de 2 colonnes à une table propriété GRF (`DM_PARAM_DELAIPAIEMENT_SOCIETE`) est accepté ;
  les tables winform restent intouchées.
