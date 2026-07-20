# TASK-088 — Détail des avertissements « Ligne exclue » (écran ③ Vérifier & Intégrer)

> **Origine** : analyse architecte 14/07/2026, suite à une question PO sur l'écran ③ Vérifier & Intégrer.
> Le bloc « Avertissements (non bloquants) » n'affichait qu'un message texte tronqué par ligne
> (ex. « Ligne exclue : Échéance hors périmètre (ni facture ni solde) »), sans indiquer quelle
> ligne précisément ni quel montant.
>
> **Révision 14/07/2026 — tunnel 3 étapes (TASK-090)** : s'exécute **après** TASK-090 ; le bloc
> « Avertissements » et le type `Alerte` visés vivent désormais dans `VerifierIntegrerPanel.tsx`
> (qui remplace `IntegrationPanel.tsx`). Périmètre et critères inchangés sur le fond.

## Contexte

Les avertissements sont générés côté back (`DeclarationWorkflowService.cs`, lignes 761-771) :

```csharp
foreach (var e in exclues)
{
    model.Alertes.Add(new Alerte {
        Niveau = NiveauAlerte.Info,
        Code = "LIGNE_EXCLUE",
        Message = $"Ligne exclue : {e.MotifRejet}",
        RefLigne = e.NumeroFacture
    });
}
```

Le contrôleur enrichit encore la réponse JSON (`DeclarationsController.cs`, lignes 251-267) avec
`code`, `refLigne`, `domaine`, `filtre` (numéro de rapprochement, exploitable pour un drill-down),
en plus de `message`. Les objets `LigneCandidate` sous-jacents portent en base numéro de facture, tiers, HT/montant
affecté, mode de paiement, dates — tout est disponible via `GET /declarations/{id}/lignes`.

Côté front, le type `Alerte` (lignes 27-32) ne gardait que `type`, `message`, `ligneId?`, `domaine?` — il ignorait `code`,
`refLigne` et `filtre` pourtant renvoyés par l'API, et l'affichage se limitait à
`• {a.message}`. Résultat : ni le numéro de facture/référence, ni le montant de la ligne exclue n'étaient
visibles, alors que `refLigne` était déjà dans la réponse.

## Périmètre STRICT

- **Inclus** :
  1. Étendre le type `Alerte` côté front pour inclure `code`, `refLigne`, `filtre` (déjà renvoyés
     par l'API, simplement non déclarés en TS).
  2. Afficher, pour chaque avertissement, la référence (`refLigne`) en plus du message existant
     (ex. « Ligne exclue (FC2501717) : Échéance hors périmètre (ni facture ni solde) »).
  3. Si `filtre` (numéro de rapprochement) est présent, permettre un drill-down vers la grille
     filtrée correspondante — **réutiliser le mécanisme déjà existant** de drill-down
     anomalie→grille filtrée (TASK-016), pas une nouvelle mécanique.
- **Exclu** :
  - Afficher le montant HT/affecté de la ligne exclue (nécessite un appel réseau supplémentaire)
  - Toute modification du calcul/génération des alertes côté back.

## Objectif

```
Entrée  : réponse /checkup (alertes[].code/refLigne/filtre, déjà renvoyés par l'API)
Traitement : le front type et affiche les champs déjà présents dans la réponse au lieu de les
             ignorer
Sortie  : chaque avertissement affiche sa référence (n° facture/pièce) ; drill-down vers la ligne
           source si un filtre de rapprochement existe
```

## Implémentation

1. **Typage Front (`Alerte`)** : Modification du type `Alerte` dans `VerifierIntegrerPanel.tsx` pour ajouter les propriétés optionnelles `code`, `refLigne` et `filtre?: Record<string, any>`.
2. **Bouton Drill-down** : Ajout du support de drill-down en réutilisant le composant `DomainGrid` s'affichant en plein écran en mode lecture seule si la variable d'état `drillFiltre` est positionnée.
3. **Formatage Avertissements/Bloquants** : Les listes d'anomalies de la carte `ChecklistCard` ont été mises à jour pour injecter le numéro de référence de la facture concernée `refLigne` s'il est fourni (ex: `Ligne exclue (FC2501717) : Échéance hors...`) et pour ajouter un bouton d'action **« Voir lignes »** si `filtre` est défini, déclenchant le drill-down.

## Livrables et Preuves

- `VerifierIntegrerPanel.tsx` modifié.
- `VERIFY/TASK-088_verify.md` : capture et checklist de validation.
- `tests/task088.spec.ts` : scénario de test E2E Playwright validant l'affichage et la navigation de drill-down.

## Critères de validation

- Référence (`refLigne`) visible pour chaque avertissement qui en porte une.
- Aucune régression sur les avertissements qui n'ont pas de `refLigne`/`filtre` (affichage actuel
  conservé en repli).
- Si drill-down implémenté : navigation vers la grille filtrée déjà validée par TASK-016, pas de
  nouveau composant.
