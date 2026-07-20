# TASK-087 — Détail de l'écart + renommage du contrôle « Équilibre comptable » (écran ④)

> **Origine** : analyse architecte 14/07/2026, suite à une question PO sur l'écran ④ Intégration.
> Le contrôle bloquant affiche un écart global (« Écart détecté : 368 517,56 MAD ») sans aucun
> détail permettant de comprendre ou corriger le problème. Le libellé « Équilibre comptable » est
> par ailleurs jugé peu clair par le PO.
>
> **Révision 14/07/2026 (demande PO « simplifier au maximum »)** : le tableau « Répartition par
> source » de `ControleDeclarationPanel.tsx` (étape ⑤, lignes 274-316) affiche **déjà** exactement
> le même détail (`recapSource` + badge équilibre/écart, lignes 280-286) que celui qu'il faudrait
> sur ④. Plutôt que de dupliquer ce rendu dans `IntegrationPanel.tsx`, on **extrait un composant
> partagé** et on le réutilise sur les deux écrans. Correction au passage : la référence à
> `SummaryPanel.tsx` dans une version antérieure de cette task était erronée — ce fichier est du
> code mort non câblé dans le tunnel (cf. TASK-089) ; le seul écran ⑤ réel est
> `ControleDeclarationPanel.tsx`.
>
> **Révision 14/07/2026 (2) — tunnel 3 étapes (TASK-090/091/092)** : cette task s'exécute
> **après** TASK-090 et TASK-091. Les ancrages fichiers changent, le fond ne change pas :
> le renommage du contrôle et le montage du détail d'écart visent le nouvel écran
> « Vérifier & Intégrer » (`VerifierIntegrerPanel.tsx`, qui remplace `IntegrationPanel.tsx`) ;
> l'extraction de `RecapSourceTable` se fait depuis le nouvel écran « Déclaration »
> (`DeclarationFinalePanel.tsx`, qui a reçu le JSX « Répartition par source » tel quel via
> TASK-091). Le typage `recapSource` de `CheckupResult` est déjà posé par TASK-090 (absorption
> TASK-086) — le point 5 ci-dessous devient une simple vérification.

## Contexte

Le contrôle est calculé dans `DeclarationsController.cs` (lignes 244-249) :

```csharp
var ecart = result.ControleEquilibre.TotalDeclareTtc
            - (result.ControleEquilibre.TotalMontantAffecte + totalTva);
var equilibre = new { isValid = Math.Abs(ecart) < 0.01m, ecart };
```

`IntegrationPanel.tsx` n'affiche que ce delta global (badge OK/BLOQUANT + montant). Le détail
ventilé **existe déjà côté API** : le même endpoint `/checkup` renvoie `recapSource` et
`recapTaux` (contrôleur, lignes 220-242), groupés par source de la ligne et par taux, avec
`ht`/`tva`/`ttc` — exactement la ventilation qui permettrait à l'utilisateur de localiser d'où
vient l'écart (quelle source, quel taux contribue le plus au delta HT+TVA vs TTC).

Il n'existe en revanche **aucune imputation ligne par ligne** de l'écart (ni en base ni en API) :
c'est une différence agrégée, pas une liste de lignes en écart. Le détail réalisable dans cette
task est donc la ventilation par source/taux, pas un drill-down facture par facture.

## Périmètre STRICT (version simplifiée)

- **Inclus** :
  1. Renommer le libellé du contrôle en **« Cohérence des totaux déclarés »** (nom retenu par le
     PO le 14/07/2026, parmi 4 options proposées), affiché dans `ChecklistCard`.
  2. **Extraire un composant partagé** `RecapSourceTable` à partir du tableau « Répartition par
     source » déjà existant dans `ControleDeclarationPanel.tsx` (lignes 274-316 : table
     Source/HT/TVA/TTC + badge équilibre/écart) — un seul rendu, deux emplacements d'usage.
     Aucune réécriture de logique, un simple déplacement du JSX existant + ses props
     (`recapSource`, `equilibre`).
  3. `IntegrationPanel.tsx` : en cas d'écart (badge BLOQUANT), monter ce composant partagé sous le
     message global — **rien d'autre** (pas de second tableau `recapTaux` en plus : la
     répartition par source suffit à comprendre l'écart, et évite de dupliquer les deux tableaux
     de ⑤ sur ④).
  4. `ControleDeclarationPanel.tsx` : remplacer son bloc actuel par un appel au composant partagé
     (aucun changement visuel sur ⑤, refactor pur).
  5. Étendre l'interface `CheckupResult` côté front pour typer `recapSource` (déjà renvoyé par
     l'API ; `recapTaux` reste hors périmètre de cette task, non nécessaire au vu du point 3).
- **Exclu** :
  - Toute imputation ligne par ligne de l'écart (n'existe pas en base/API).
  - Le tableau `recapTaux` sur ④ (déjà visible sur ⑤ une fois l'écart résolu — pas nécessaire en
    double avant intégration).
  - Le calcul de l'écart lui-même (contrôleur `DeclarationsController.cs`, inchangé).

## Objectif

```
Entrée  : réponse /checkup (equilibre.ecart + recapSource + recapTaux, déjà renvoyés par l'API)
Traitement : afficher le détail par source/taux sous le badge BLOQUANT, uniquement quand écart≠0
Sortie  : l'utilisateur voit non seulement qu'il y a un écart, mais par où il se répartit
          (source/taux), et le libellé du contrôle est sans ambiguïté
```

## Livrables

- Nouveau fichier `RecapSourceTable.tsx` (composant partagé, extrait de
  `ControleDeclarationPanel.tsx`).
- `ControleDeclarationPanel.tsx` modifié (consomme le composant partagé, aucun changement visuel).
- `IntegrationPanel.tsx` modifié (libellé renommé + composant partagé monté en cas d'écart).
- `VERIFY/TASK-087_verify.md` : capture montrant le détail par source affiché sur ④ sur un cas réel
  en écart (ex. le cas PO 68 règlements / 368 517,56 MAD), confirmation du nouveau libellé, et
  capture de ⑤ montrant l'absence de régression visuelle après refactor.

## Critères de validation

- Nouveau libellé « Cohérence des totaux déclarés » affiché sur ④.
- Quand `equilibre.isValid === false` sur ④ : tableau « Répartition par source » (composant
  partagé) visible sous le badge, mêmes données que celles qu'affichera ⑤ une fois l'écart résolu.
- Quand `equilibre.isValid === true` sur ④ : aucun changement visuel (pas de détail à afficher).
- ⑤ : rendu visuel strictement identique avant/après refactor (aucune régression).
- Un seul endroit dans le code qui rend le tableau « Répartition par source » (plus de JSX dupliqué
  entre ④ et ⑤).

## Risques / dépendances

- Aucun risque backend (lecture seule d'un contrat déjà exposé, déjà consommé ailleurs dans
  l'app). À séquencer avec TASK-086 si les deux touchent `IntegrationPanel.tsx` en même temps
  (zones distinctes du composant).
- Le refactor de `ControleDeclarationPanel.tsx` (point 4) doit rester un déplacement pur de JSX —
  toute divergence de rendu constatée en VERIFY est bloquante.
