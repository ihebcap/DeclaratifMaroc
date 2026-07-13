# TASK-035 — Shell de navigation « menu groupé à valeur ajoutée » + écran dense « 0 espace perdu »

> Nom de fichier conservé (`...deux-entrees...`) pour l'historique/les liens TODO. Le périmètre a évolué : **menu groupé multi-entrées** (décision PO 09/07/2026), voir ci-dessous.

## Contexte
Retour PO (09/07/2026) sur le front du module Déclaration TVA (`declaration-tva-web`) :
- Le PO veut **plusieurs entrées de menu pour donner à voir la valeur ajoutée** du produit — pas seulement 2. La décompilation de GénéraFi (produit de référence) a confirmé que rapprochement bancaire, factures, relevé de déductions, RAS, télédéclaration et tableau de bord sont des modules à part entière.
- **Mais** on ne construit que le **cœur** d'abord (Rapprochement + Déclaration). Les autres entrées sont **visibles** (elles montrent la portée) et **grisées / « à venir »** tant que la roadmap R1/R2/R3 n'est pas lancée. ⚠️ Ne pas dériver : le menu affiche la valeur, il ne l'implémente pas toute.
- La sidebar actuelle (`App.tsx:91-101`) affiche « Mes Déclarations » + « Déclaration en cours » (contextuel) — à remplacer.
- Directive de style : **écran simple, densité maximale, « 0 espace perdu »** — GRC/GOCOM cité **uniquement comme exemple de style**. ⚠️ **Deux modules différents** : aucune fusion, aucun couplage avec `gocom-web`/repo GRC_WEB.

## Périmètre STRICT
- **Inclus** : refonte de l'**architecture de navigation** (menu latéral **groupé** à sections) + **densification** de la mise en page. Front-only (`declaration-tva-web`).
- **Exclu** : toute intégration `gocom-web`/GRC_WEB. Aucun changement d'API, de calcul, de workflow. Ne pas implémenter les écrans « à venir » (RAS, Télédéclaration, Tableau de bord) ni les interrogations secondaires (Factures, Relevé) au-delà d'un **placeholder honnête**. Dépend de l'affichage corrigé par **TASK-034**.

## Décision PO — TRANCHÉE (09/07/2026)
- **Menu groupé complet** (option recommandée retenue) : on affiche toute la portée, on ne build que le cœur.
- **Ordre d'exécution** : **034 → 035 → 036 → 037 → 038** (le shell menu remonte en #2 pour rendre la valeur visible tôt).

## Cible de navigation (IA) — menu groupé
Sidebar compacte, style dense, **3 sections**. Légende : 🟢 livré maintenant · ⚪ interrogation à faible coût (placeholder honnête pour l'instant) · 🔒 grisé/« à venir » (roadmap, non cliquable).

| Section | Entrée | Nature | Statut | Source |
|---|---|---|---|---|
| **INTERROGATION** | Rapprochement bancaire | Interrogation globale, lecture seule, pivot règlement | 🟢 | écran **TASK-037** / endpoint **TASK-036** |
| | Factures | Interrogation lecture seule (vue factures) | ⚪ placeholder | à cadrer ultérieurement |
| **DÉCLARATION** | Déclaration TVA | Liste + création + poste de travail « Factures à déclarer » (Je déclare / Je ne déclare pas) + bandeau réconciliation + clôture | 🟢 | flux existant + traçabilité **TASK-038** |
| | Relevé de déductions | Sortie/annexe officielle | ⚪ placeholder | à cadrer ultérieurement |
| **À VENIR** | Retenue à la source (RAS) | Roadmap **R1** | 🔒 grisé | — |
| | Télédéclaration (SIMPL) | Roadmap **R2** | 🔒 grisé | — |
| | Tableau de bord | Roadmap **R3** | 🔒 grisé | — |

## Objectif
```
Entrée  : l'utilisateur ouvre le module déclaration
Sortie  : sidebar compacte à sections (INTERROGATION / DÉCLARATION / À VENIR) ;
          entrées cœur actives (Rapprochement 🟢, Déclaration 🟢), autres en placeholder ⚪
          ou grisées 🔒 « à venir » ; contenu principal en pleine hauteur/largeur, marges réduites.
Style   : dense, sobre, lisible pour un comptable — « 0 espace perdu ».
Effet   : la valeur ajoutée (portée du produit) est visible sans être toute construite.
```

## Étapes
1. `App.tsx` : remplacer les items de sidebar actuels (`:91-101`) par un **menu groupé** (état de navigation dédié, ex. `section: 'rapprochement' | 'factures' | 'declaration' | 'releve' | ...`). Conserver header société + footer utilisateur/déconnexion, mais **compacts**.
2. Rendre les groupes/entrées : les 🔒 « à venir » sont **désactivées** (non cliquables, indice visuel « bientôt »), sans route morte. Les ⚪ ouvrent un **placeholder honnête** (« Écran en cours »), **jamais** de données factices.
3. Router le contenu principal :
   - `Déclaration TVA` → flux existant (`DeclarationList` → `DeclarationStepper`/poste de travail « Factures à déclarer »).
   - `Rapprochement bancaire` → **écran de TASK-037** (placeholder honnête tant que 037 non livré).
   - Autres → placeholder honnête.
4. **Densification** : réduire paddings (`main-content`, panneaux), grille en pleine hauteur (`flex:1`, scroll interne géré par `DomainGrid`), sidebar compacte par défaut, supprimer les blocs décoratifs. GOCOM = référence visuelle **uniquement**.
5. Pas de scroll horizontal parasite hors grille. Ne pas casser bandeau réconciliation, clôture verrouillée, toasts.

## Livrables
- Menu latéral groupé (3 sections) + mise en page densifiée dans `declaration-tva-web`.
- `VERIFY/TASK-035_verify.md` : captures **avant/après** montrant (1) le menu groupé avec entrées actives vs grisées, (2) le poste de travail « Factures à déclarer » et le placeholder « Rapprochement bancaire », (3) la densité (espace utile maximisé). Build `tsc + vite` + `oxlint` OK. Naviguer les sections actives sans erreur console.

## Critères de validation
- Menu latéral **groupé** : INTERROGATION (Rapprochement 🟢, Factures ⚪) / DÉCLARATION (Déclaration TVA 🟢, Relevé ⚪) / À VENIR (RAS, Télédéclaration, Tableau de bord — 🔒 grisés, non cliquables).
- « Déclaration TVA » ouvre le flux liste → poste de travail (Factures à déclarer + clôture) inchangé fonctionnellement.
- « Rapprochement bancaire » ouvre l'écran TASK-037 (ou placeholder honnête si non livré) — jamais de données factices.
- Entrées « à venir » visibles mais désactivées ; aucune route morte, aucune donnée factice.
- Mise en page dense : contenu principal pleine hauteur/largeur, pas d'espace mort notable ; pas de scroll horizontal hors grille.
- Aucune dépendance ni appel vers `gocom-web`/GRC_WEB. Aucun changement d'API/calcul (le shell ne fait que router).

## Risques / dépendances
- **Dépend de TASK-034** (affichage) : ordonner **034 → 035**.
- L'écran « Rapprochement bancaire » est fourni par **TASK-036 (back) + TASK-037 (front)** : 035 livre le shell + le routage + les placeholders ; l'écran réel arrive avec 037. 035 ne bloque pas sur 036/037.
- **Risque de dérive** : ne PAS commencer à implémenter Factures / Relevé / RAS / Télédéclaration / Tableau de bord ici. Le menu les **montre**, ne les **fait pas**. Rester *simple avant beau*.
- Ne pas régresser la clôture verrouillée ni le bandeau réconciliation.
