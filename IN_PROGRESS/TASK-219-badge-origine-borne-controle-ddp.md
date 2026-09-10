# TASK-219 — Badge « déjà déclarée / 1re déclaration / reprise manuelle » sur chaque ligne (écran Contrôle DDP)

## Contexte

Demande PO (10/09/2026), complémentaire à TASK-217 (renommage/tooltips des colonnes de bornes) et
TASK-218 (commentaire généré par ligne) : afficher un badge visuel devant chaque ligne du contrôle
indiquant si la facture a déjà été déclarée dans une déclaration DDP antérieure, ou s'il s'agit de sa
première apparition.

La donnée existe déjà et est déjà exposée côté backend/DTO — aucun calcul nouveau requis :
`OrigineBorneReference` (`Declaration.API/Dtos/DeclarationDelaiPaiementDto.cs:173/208`,
`SelectionDelaiPaiementCalculator.cs:191-204`), avec 4 valeurs possibles :
- `DerniereDeclaration` : l'échéance a déjà été comptée dans au moins une déclaration antérieure
  (borne de référence = `MAX(DDP_DateFin)` sur `RT_DECLARATIONDELAISPAIEMENTLG`).
- `EcheanceLegale` : aucune déclaration antérieure — première fois que cette échéance apparaît dans
  le contrôle (borne de référence = l'échéance légale elle-même).
- `RepriseManuelle` : échéance antérieure à la date de mise en route du module, dont le retard déjà
  couvert a été saisi manuellement (TASK-128).
- `Indeterminee` : garde-fou de mise en route non levé — n'apparaît QUE sur les lignes en statut
  `RepriseManuelleRequise` (non intégrables), déjà signalées par le badge « Reprise manuelle requise »
  existant sur la colonne Statut (`ControleLignesDelaiPaiementPanel.tsx:141`) — ne pas dupliquer ce
  badge, `Indeterminee` n'a donc pas besoin d'un badge dédié dans cette TASK.

Décision PO : les 3 états utiles à distinguer visuellement sont **Déjà déclarée** / **1re
déclaration** / **Reprise manuelle** (le 4e état, `Indeterminee`, reste couvert par le badge Statut
existant).

## Objectif
```
Entrée  : l'origine de la borne de référence (déjà déclarée avant / 1re fois / reprise manuelle) n'est
          visible qu'indirectement, en déduisant depuis la présence ou non d'une date dans la colonne
          "Dernière déclaration" (TASK-217) — aucun badge visuel dédié.
Traitement : ajouter un badge coloré/typé par ligne, dérivé du champ OrigineBorneReference déjà
             calculé et déjà présent dans le DTO, sans aucun nouveau calcul backend.
Sortie  : chaque ligne du contrôle porte un badge immédiatement visible indiquant si la facture est
          déjà connue du dispositif DDP (déjà déclarée), nouvelle (1re déclaration), ou couverte par
          une reprise manuelle saisie au démarrage du module.
```

## Périmètre STRICT

- **Inclus** :
  1. `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx`, `columnDefs` : nouvelle colonne
     (ou badge intégré à une colonne existante, ex. à côté de « Dernière déclaration ») affichant un
     badge visuel dérivé de `origineBorneReference` (à vérifier/ajouter dans le mapping TypeScript de
     `api.ts` si absent — le champ existe déjà côté DTO backend, `OrigineBorneReference`).
  2. 3 badges distincts, avec libellé et couleur cohérents avec le style déjà utilisé sur la colonne
     Statut (`ControleLignesDelaiPaiementPanel.tsx:141`, badges colorés `Retard calculé` /
     `Reprise manuelle requise`) :
     - `DerniereDeclaration` → **« Déjà déclarée »**
     - `EcheanceLegale` → **« 1re déclaration »**
     - `RepriseManuelle` → **« Reprise manuelle »**
     - `Indeterminee` → aucun badge dédié (déjà couvert par le badge Statut existant, colonne
       `statut` : `Reprise manuelle requise`).
  3. Filtrable (comme les autres colonnes à `filter: CustomListFilter` déjà en place sur l'écran,
     ex. `origineDelai` ligne 146) pour permettre à l'utilisateur d'isoler rapidement les lignes
     « 1re déclaration » avant un dépôt (cas le plus sensible à vérifier).
- **Exclus / hors périmètre** :
  - Modifier la logique de calcul de `OrigineBorneReference` — donnée déjà correcte et déjà
    calculée, TASK strictement d'affichage.
  - Fusionner ce badge avec le badge Statut existant (« Retard calculé » / « Reprise manuelle
    requise ») — les deux badges répondent à des questions différentes (statut d'intégrabilité vs
    historique de déclaration) et doivent rester visuellement distincts.
  - Coordination de vocabulaire avec TASK-217/TASK-218 si menées en parallèle : réutiliser
    « Déjà déclarée » (cohérent avec le renommage « Dernière déclaration » de TASK-217) plutôt qu'un
    terme différent.

## Étapes
1. Vérifier dans `declaration-tva-web/src/api.ts` que `origineBorneReference` est bien mappé côté
   TypeScript ; sinon l'ajouter (le DTO backend l'expose déjà).
2. Définir le badge (couleur + libellé) pour les 3 valeurs utiles, en cohérence visuelle avec les
   badges Statut déjà présents sur l'écran (mêmes tokens de couleur `var(--status-*)` déjà utilisés
   ligne 141, à réutiliser plutôt qu'inventer une nouvelle palette).
3. Ajouter la colonne/le badge, avec filtre.
4. Test visuel : au moins un exemple de chaque badge visible sur des données réelles (SO_Id=1).
5. `npm run lint` + `npm run build` dans `declaration-tva-web/` → 0 erreur.

## Livrables
- `ControleLignesDelaiPaiementPanel.tsx` (et `api.ts` si mapping manquant) mis à jour.
- `VERIFY/TASK-219_verify.md` : capture d'écran montrant les 3 badges sur des lignes réelles, preuve
  du filtre fonctionnel, logs `npm run lint` / `npm run build` (0 erreur), checklist UI de
  `DOCS/UI_STANDARDS.md` cochée.

## Critères de validation
- Chaque ligne intégrable (Statut = « Retard calculé ») porte un badge « Déjà déclarée »,
  « 1re déclaration » ou « Reprise manuelle », cohérent avec la valeur réelle de
  `OrigineBorneReference`.
- Les lignes en « Reprise manuelle requise » (Statut) ne portent pas de badge d'origine dupliqué
  (pas de badge « Indéterminée » redondant avec le badge Statut existant).
- Le badge est filtrable comme les autres colonnes de l'écran.
- Cohérence visuelle avec les badges Statut existants (mêmes tokens de couleur).
- `npm run lint` + `npm run build` → 0 erreur.

## Risques / dépendances
- Coordination de vocabulaire avec TASK-217 (« Déjà déclarée » doit rester cohérent avec le
  renommage « Dernière déclaration » de la colonne de bornes) — signaler en VERIFY si TASK-217 n'est
  pas encore livrée au moment de cette TASK, pour éviter une divergence de libellé.
- Aucun risque fonctionnel autrement (TASK purement UI, read-only, donnée déjà calculée et déjà
  exposée par le backend).
