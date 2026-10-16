# TASK-239 — V2 : explicabilité des calculs et détection des chiffres anormaux

Status: 🆕 à faire
Priority: HIGH
Risk: HIGH (c'est la garantie que « rien n'est caché » à l'utilisateur)
Module: Declaration.Compta.Core + Declaration.Export.Excel + Declaration.API + declaration-tva-web
Dépend de : TASK-226, TASK-227, TASK-228 (les calculs à expliquer) ; front = TASK-231 + TASK-232

> **Origine :** PO 16/10/2026 — « toute formule de calcul ou chiffre bizarre, on doit l'afficher ».

## Décisions de conception

1. **Règle générale** : tout montant affiché ou exporté doit pouvoir répondre à « d'où vient ce chiffre ? ». Aucun montant calculé sans sa formule, ses entrées et ses sources.
2. **Objet `Explication`** produit par chaque calcul (couche pure `Declaration.Compta.Core`) : formule en clair, entrées numériques, règle appliquée (ex. « prorata », « imputation proportionnelle »), arrondi appliqué, et **références sources** (`cbMarq` des écritures, journal, pièce, date d'opération).
3. **Application** : panneau « Pourquoi ce montant ? » ouvert depuis n'importe quelle ligne (XGrid) : décomposition pas à pas (par exemple : TVA facture 58,16 × règlement 400 ÷ TTC 348,96 = 66,67), avec lien vers les écritures sources.
4. **Excel** (TASK-236) : feuille `Calculs` (une ligne d'explication par ligne déclarée : formule, entrées, règle, sources) et formules Excel **vivantes** dans `Récap`. Le classeur est auto-explicatif sans l'application.
5. **Chiffres anormaux** : catalogue de règles de plausibilité, chacune avec un **code, une gravité et un seuil configurable** : TVA > HT ; taux hors liste usuelle marocaine (0, 7, 10, 14, 20 %) ; montants négatifs ou nuls ; règlement supérieur au TTC ; date d'opération hors période ou hors exercice ; écart HT × taux ; arrondi cumulé > N centimes sur une facture ; facture très ancienne réglée tardivement ; HT identique à la TVA ; part de prorata hors ]0 ; 100 %] ; total par taux très éloigné de la période précédente (écart > X %) ; même facture sur plusieurs règlements d'un montant atypique. **Liste de départ à enrichir avec les résultats de TASK-235** (analyse 11 en particulier).
6. **Jamais silencieux** : une règle franchie affiche l'alerte (bandeau de ligne, grille d'alertes, feuille `Alertes` du classeur) avec l'explication de la règle et la valeur observée. Les alertes non bloquantes sont acquittables avec motif (audit TASK-238) ; les bloquantes suivent TASK-228/234.
7. **Reproductibilité** : l'explication est calculée par le même code que le montant (pas de second calcul), et un test vérifie que « refaire le calcul à la main depuis l'explication » redonne le montant.
8. Les formules et règles sont **documentées dans la documentation fonctionnelle** (TASK-240), générée ou vérifiée à partir du même catalogue pour éviter toute dérive entre le texte et le code.

## Périmètre STRICT

- **Inclus** : objet `Explication`, intégration aux calculs de TASK-226/227/228/229, panneau front, feuille `Calculs`, catalogue de règles de plausibilité configurable (par dossier), tests.
- **Exclu** : nouvelles règles de calcul, correction automatique des anomalies.

## Livrables

- Explications + panneau + feuille Excel + catalogue de règles + tests.

## Critères de validation

- Pour 100 % des lignes d'un jeu de test (NEW_EMA, LOTREX), la formule de l'explication, rejouée à la main, redonne exactement le montant affiché.
- Une anomalie injectée par règle du catalogue est signalée avec son code et sa valeur (une preuve par règle).
- Aucun montant du classeur sans ligne correspondante dans `Calculs`.
- Un seuil modifié dans la configuration du dossier change bien le résultat, sans redéploiement.
- Build + lint 0 erreur ; **checklist UI** ; preuve par critère datée.

## Files

- `Declaration.Compta.Core/`, `Declaration.Export.Excel/`, `declaration-tva-web/src/`.
- `DOCS/CADRAGE_V2_COMPTA_SOURCE_UNIQUE.md` §3 et §4 (règles de calcul), TASK-235 (statistiques).
