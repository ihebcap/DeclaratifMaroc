# TASK-208 — Filtres texte/nombre ignorés côté back sur `GET .../lignes` (`DomainGrid` + drills)

Status: 🆕 à faire
Priority: MEDIUM (donnée trompeuse : l'utilisateur croit filtrer, la grille ne filtre pas réellement)
Module: Declaration.Infrastructure / declaration-tva-web

> **Origine :** point hors périmètre découvert pendant les tests de TASK-183 (24/07/2026), jamais
> transformé en TASK. Confirmé toujours présent le 09/08/2026 (grep `BuildLigneFilterWhere`,
> `Declaration.Infrastructure/Repositories/DeclarationRepository.cs:416`).

## Cause identifiée (preuve de code)

`DomainGrid.tsx` envoie un filtre JSON structuré au back pour les colonnes texte/nombre, entre autres
`factureNumero`, `tiers`, `montantHT`, `montantTVA`, `montantTTC` (voir par ex.
[declaration-tva-web/src/FacturesADeclarerPanel.tsx:132-137](../declaration-tva-web/src/FacturesADeclarerPanel.tsx#L132-L137)
pour la définition des colonnes filtrables).

Côté back, `BuildLigneFilterWhere`
([Declaration.Infrastructure/Repositories/DeclarationRepository.cs:416](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs#L416))
ne reconnaît explicitement que les clés `numeroRapprochement`/`source`/`tauxTVA`/`origine`
(TASK-067B). Toute autre clé du JSON envoyé par le front (`factureNumero`, `tiers`, `montantHT`,
`montantTVA`, `montantTTC`, etc.) est **silencieusement ignorée** — ni erreur, ni fragment SQL ajouté.
`GetLignesAsync` et `GetLignesCountAsync` utilisent toutes les deux cette même fonction
(lignes 376/394), donc la grille ET le total de pages sont affectés de façon cohérente entre eux, mais
faux par rapport à l'intention de l'utilisateur.

**Effet observable** : un utilisateur qui tape un N° de facture ou un montant dans le filtre de colonne
voit la grille se comporter comme si le filtre n'existait pas (toutes les lignes restent affichées) —
aucun message d'erreur, le filtre semble simplement ne rien faire.

## Périmètre STRICT

- **Inclus** :
  1. Étendre `BuildLigneFilterWhere` pour reconnaître au minimum les clés réellement envoyées par le
     front et actuellement ignorées : `factureNumero`, `tiers`, `montantHT`, `montantTVA`,
     `montantTTC` (vérifier dans `DomainGrid.tsx`/les panels consommateurs la liste exacte des clés
     `filterType: 'text'`/`'number'` envoyées, ne pas se fier uniquement à cette liste indicative).
  2. Texte : filtre `LIKE '%...%'` (cohérent avec le comportement Excel-like existant pour les clés
     déjà supportées). Nombre : égalité exacte ou plage, à trancher selon ce que `DomainGrid.tsx`
     envoie réellement côté payload (opérateur déjà présent dans le JSON ou valeur brute seule ?
     vérifier avant de coder).
  3. Non-régression stricte : les 4 clés déjà supportées (`numeroRapprochement`/`source`/`tauxTVA`/
     `origine`) doivent continuer à fonctionner à l'identique.
- **Exclu** :
  - Pas de changement du format JSON envoyé par le front, sauf si l'inspection du payload réel révèle
    qu'il manque une information indispensable au filtre back (à signaler si trouvé, pas à improviser).
  - Pas de refonte du système de filtre (reste un WHERE SQL additionnel simple, pas un query builder
    générique).

## Livrables

- `BuildLigneFilterWhere` filtre réellement sur `factureNumero`/`tiers`/`montantHT`/`montantTVA`/
  `montantTTC` (ou la liste exacte confirmée en inspectant le payload front).
- Test automatisé (Infrastructure ou Application selon la couche testée) couvrant au moins un filtre
  texte et un filtre nombre parmi les clés ajoutées, plus un test de non-régression sur une clé déjà
  supportée.

## Critères de validation

- Sur l'écran principal ET sur au moins un drill (les 4 « kinds » de drill partagent le même endpoint),
  taper un N° de facture existant dans le filtre de colonne ne retourne que les lignes correspondantes
  — vérifié en conditions réelles (base `GR_EMA_DISTRIBUTION`), pas seulement par test unitaire.
- `GetLignesCountAsync` (pagination) reste cohérent avec `GetLignesAsync` sous les nouveaux filtres
  (même invariant que TASK-040).
- Les filtres déjà supportés avant cette TASK ne régressent pas.

## Files

- [Declaration.Infrastructure/Repositories/DeclarationRepository.cs](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs) (fonction `BuildLigneFilterWhere`, ligne 416).
- [declaration-tva-web/src/DomainGrid.tsx](../declaration-tva-web/src/DomainGrid.tsx) — à inspecter pour confirmer le payload JSON exact envoyé pour les colonnes texte/nombre avant de coder le back.
