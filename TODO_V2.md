# TODO V2 — Déclaration assistée depuis la comptabilité Sage (branche `feature/v2-compta`)

> Branche dédiée : **la version actuelle (main) n'est pas touchée** (PO 16/10/2026). Cadrage :
> `DOCS/CADRAGE_V2_COMPTA_SOURCE_UNIQUE.md`. Phase actuelle : **analyse et cadrage uniquement, aucun code** ;
> le développement est fait par Gemini (WORKER), reviewé par l'architecte.
> Principe produit : la V2 **aide au maximum** mais ne livre pas la déclaration définitive seule :
> **export Excel au format contractuel → l'utilisateur complète → ré-import → déclaration définitive**.

## EPIC V2-1 — Socle et lecture comptable
| Task | Objet | Dépend de |
|---|---|---|
| [TASK-235](TASKS/TASK-235-v2-compta-analyse-donnees-3-bases-lotrex-ema-gocom.md) | Analyse des données des 3 bases (lecture seule, **à lancer en premier avec Gemini**) | — |
| [TASK-225](TASKS/TASK-225-v2-compta-socle-base-declaratifmaroc-ef-config-dossier.md) | Base `DeclaratifMaroc` (EF, migrations auto), configuration du dossier | — |
| [TASK-226](TASKS/TASK-226-v2-compta-lecteur-factures-lettrees-ht-tva-ttc-par-taux.md) | Lecteur de factures lettrées achats/ventes → HT/TVA/TTC par taux | 225, 235 |

## EPIC V2-2 — Règlements et rapprochement
| Task | Objet | Dépend de |
|---|---|---|
| [TASK-234](TASKS/TASK-234-v2-compta-alertes-parametrage-tresorerie-sage.md) | Alertes sur le paramétrage de trésorerie Sage (banque seule, compte de trésorerie) | 225, 235 |
| [TASK-227](TASKS/TASK-227-v2-compta-reglements-rapprochement-date-operation-prorata.md) | Règlements lettrés, rapprochement banque, date `JM_Date+EC_Jour−1`, prorata, report | 226, 234 |

## EPIC V2-3 — Contrôles d'aide à la décision
| Task | Objet | Dépend de |
|---|---|---|
| [TASK-228](TASKS/TASK-228-v2-compta-controle-ht-taux-tva-ecart-valide-par-utilisateur.md) | Contrôle HT × taux = TVA, écart affiché, validation utilisateur | 225, 226 |
| [TASK-229](TASKS/TASK-229-v2-compta-flux-comptes-de-taxe-sans-tiers.md) | Flux des comptes de taxe sans tiers (dépenses, frais bancaires) | 226, 228 |

## EPIC V2-4 — Aller-retour Excel et déclaration définitive
| Task | Objet | Dépend de |
|---|---|---|
| [TASK-236](TASKS/TASK-236-v2-compta-export-excel-aide-declaration-format-contractuel.md) | Export Excel d'aide à la déclaration, format contractuel versionné | 227, 228, 229, 234 |
| [TASK-237](TASKS/TASK-237-v2-compta-import-excel-complete-controle-previsualisation.md) | Import du fichier complété, contrôles d'intégrité et de dérive, prévisualisation | 236 |
| [TASK-230](TASKS/TASK-230-v2-compta-branchement-moteur-commun-api-ecrans-export.md) | Déclaration définitive depuis le fichier importé, lignes déclarées idempotentes, XML | 237 |

## EPIC V2-5 — Transparence, documentation et diagnostic (transversal)
| Task | Objet | Dépend de |
|---|---|---|
| [TASK-238](TASKS/TASK-238-v2-journalisation-complete-et-pack-de-diagnostic.md) | Journal structuré, audit métier, pack de diagnostic en un clic | 225 |
| [TASK-239](TASKS/TASK-239-v2-explicabilite-des-calculs-et-chiffres-anormaux.md) | « Pourquoi ce montant ? », feuille `Calculs`, règles de chiffres anormaux | 226, 227, 228 |
| [TASK-240](TASKS/TASK-240-v2-documentation-complete-utilisateur-fonctionnelle-technique.md) | Documentation complète (4 documents, fiches d'alerte, aide intégrée) | toutes |

### Règles transversales de « terminé » (PO 16/10/2026) — appliquées en revue à **toute** TASK V2
1. **Documentée** : formules, alertes, colonnes et codes d'événements mis à jour dans `DOCS/V2/` (TASK-240). Doc absente ou périmée = REJECT.
2. **Explicable** : tout montant calculé expose sa formule, ses entrées et ses sources (TASK-239). Aucun calcul opaque.
3. **Rien de bizarre en silence** : toute valeur hors règle de plausibilité est affichée avec son code et sa raison, jamais corrigée ni masquée.
4. **Tracée** : tout événement significatif et toute erreur sont journalisés avec `CorrelationId` et code stable (TASK-238) ; aucune erreur « générique ».

## EPIC UX — Charte graphique xGR et XGrid
| Task | Objet | Dépend de |
|---|---|---|
| [TASK-231](TASKS/TASK-231-charte-xgr-tokens-shell-sidebar-kpicard-tags.md) | Charte xGR : tokens, sidebar, KpiCard, tags, dark mode | — |
| [TASK-232](TASKS/TASK-232-xgrid-react-portage-composant-grille.md) | XGrid porté en React (remplace ApbsGrid) | 231 |
| [TASK-233](TASKS/TASK-233-migration-ecrans-existants-charte-xgr-et-xgrid.md) | Migration des écrans V1 (5 lots), retrait d'AG Grid | 231, 232 |

L'EPIC UX est en parallèle du back des EPICS V2-1 à V2-3 ; les écrans V2 (225, 228, 229, 234, 237, 230) sont construits directement sur XGrid et la charte.

## Ordre de lancement recommandé
1. **TASK-235** (analyse Gemini, lecture seule) — prompt prêt : `DOCS/PROMPT-WORKER-TASK-235-16-10-2026.md`.
2. Arbitrage PO sur le n:m, puis TASK-225 → 226 → 234 → 227 (back).
3. En parallèle TASK-231 → 232.
4. TASK-228/229, puis TASK-236 → 237 → 230.
5. TASK-233 (migration V1) en dernier.

## Hors périmètre (à ouvrir plus tard)
- **Avoirs** (détectés et signalés, jamais intégrés — PO : « on va les voir après »).
- **DDP V2** (même source comptable ; schéma cible à préciser).
- **XML encaissement** (schéma DGI introuvable).
