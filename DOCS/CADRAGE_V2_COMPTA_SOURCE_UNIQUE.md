# Cadrage V2 — Déclaration TVA/DDP depuis la comptabilité Sage uniquement (16/10/2026)

> Origine : demande PO 16/10/2026. Concrétise la ROADMAP V2 de `TODO.md` (24/07 et 28/07/2026).
> Statut : cadrage + spike lecture seule effectué. TASKS 225 à 230. Aucun code écrit.

## 0. Principe produit (précision PO 16/10/2026)

La V2 **n'est pas** un générateur de déclaration définitive. Elle **aide au maximum** :

```
Compta Sage ─► analyse + contrôles ─► EXPORT EXCEL (format contractuel) ─► l'utilisateur complète
                                                                                    │
         déclaration définitive (XML) ◄─ contrôles + prévisualisation ◄─ IMPORT du fichier
```

- Les montants viennent de la compta ; l'utilisateur complète IF/ICE, désignation, code activité, **décision par ligne** (inclure / reporter / exclure) et **valide les écarts**.
- Le fichier importé est la **source de la déclaration définitive** ; tout est contrôlé à l'import (intégrité, dérive de la compta depuis l'export).
- Travail sur la branche `feature/v2-compta` : **la version actuelle (main) reste intacte**. Phase actuelle : analyse et cadrage, **aucun code** ; développement par Gemini, revue par l'architecte.
- Backlog par epic : `TODO_V2.md`.

## 1. Décisions PO (16/10/2026)

| # | Décision |
|---|---|
| 1 | Source unique = compta Sage (`F_ECRITUREC`). Rapprochement = `EC_TresoPiece` renseigné. **Date retenue = `JM_Date + EC_Jour − 1`** (jamais `EC_DateRappro`). |
| 2 | Date de déclaration = date de l'écriture de banque, **pour tous les modes de règlement**. |
| 3 | Prorata du règlement. **Pas de lettrage partiel.** |
| 4 | Cas simple : chaque compte de charge a son propre taux de TVA. |
| 5 | Le taux vient de `F_TAXE` (qui porte le compte général de TVA). |
| 6 | IF/ICE : nom des colonnes `F_COMPTET` saisi en paramétrage, stocké dans la nouvelle base. |
| 7 | Contrôle HT × taux = TVA : **on affiche l'écart, l'utilisateur valide** (sa responsabilité). Pas de tolérance. |
| 8 | Second flux : comptes de taxe **non rattachés à un tiers** (dépenses, frais bancaires) à contrôler. |
| 9 | Partie commune (`Declaration.Core`, exports XML/Excel) **inchangée** ; nouvelle source en adaptateur. |
| 10 | Pas d'Objets Métier Sage : SQL direct en lecture seule. |
| 11 | Nouvelle base **DeclaratifMaroc**, EF Core, migrations automatiques. |
| 12 | Bases de test : LOTREX, NEW_EMA DISTRIBUTION, GOCOM (voir §3). |

### Périmètre : décaissements ET encaissements (précision PO 16/10/2026)

Les deux sens sont dans le périmètre, avec **le même moteur et les mêmes règles**, en miroir :

| | Décaissement (achats) | Encaissement (ventes) |
|---|---|---|
| Journal de facture | `JO_Type=0` (ACH) | `JO_Type=1` (VTE) |
| Tiers | fournisseur `CT_Type=1` | client `CT_Type=0` |
| Ligne tiers | crédit, TTC | **débit**, TTC |
| Lignes de charge / produit | débit, HT | **crédit**, HT |
| Ligne TVA | débit (`F_TAXE.TA_Sens=0`, 6111…) | **crédit** (`F_TAXE.TA_Sens=1`, 7111…) |
| Règlement banque | tiers au débit, banque au crédit | **tiers au crédit, banque au débit** |
| Avoir | débit fournisseur | crédit client |
| Export DGI | relevé de déductions (XML documenté) | **schéma XML inconnu** (Annexe A MODULE_DECLARATION_TVA.md) → consultation/contrôle + Excel uniquement |

Le sens (`TA_Sens`, journal) est un **paramètre du moteur**, jamais du code dupliqué.

## 2. Modèle comptable vérifié sur données réelles (spike lecture seule)

**Écriture d'achat** (NEW_EMA, journal `ACH`) :

| Ligne | Compte | Sens | Montant | `TA_Code` |
|---|---|---|---|---|
| Fournisseur | 4411…, `CT_Num` renseigné, `EC_Lettre=1` | 1 (crédit) | 348,96 | — |
| Charge | 7124300000 | 0 | 290,80 | **D20** |
| TVA | 6111000000 | 0 | 58,16 | — |

→ 290,80 × 20 % = 58,16 ✓. Le taux se lit via `TA_Code` de la ligne de charge → `F_TAXE.TA_Taux` ; le compte de TVA attendu est `F_TAXE.CG_Num` (ici D20 → 6111000000, confirmé).

**Pièce de règlement** (journal `BQE`, `JO_Type=2`) : 2 lignes de même (`JO_Num`, `EC_Piece`, `JM_Date`).

| Ligne | Compte | Sens | Particularité |
|---|---|---|---|
| Fournisseur | 4411…, `CT_Num` | 0 (débit) | porte le lettrage (`EC_Lettrage`) avec la facture |
| Banque | 5141000000 | 1 (crédit) | **porte `EC_TresoPiece`** (preuve de rapprochement) |

Exemple : `JM_Date`=2026-06-01, `EC_Jour`=8 → **date opération 2026-06-08**.

⚠ Dans cet exemple `EC_DateRappro`=2026-02-28, **antérieure** à la date d'opération. Cela confirme que `EC_DateRappro` est inutilisable : la décision PO #1 est la bonne.

## 3. Constats chiffrés (à ne pas ignorer)

| Indicateur | NEW_EMA | LOTREX | GOCOM |
|---|---|---|---|
| Base Sage compta (`F_ECRITUREC`) | oui | oui | **NON** (4 tables `F_*`, pas d'écritures) |
| Écritures de banque / dont `EC_TresoPiece` | 1 890 / 673 | 30 425 / 3 364 | — |
| Règlements fournisseur lettrés en banque : rapprochés / non rapprochés | 335 / **182** | à re-mesurer (tiers ≠ `F%`) | — |
| Règlements lettrés en caisse (`CAI`, `JO_Rappro=0`) | 51, aucun rapprochement attendu | `CAIS`/`CAP`/`EAP` ont `JO_Rappro=2` | — |
| Pièces d'achat multi-taux | 335 | 8 | — |
| Lettrages partiels fournisseur | 0 | 0 | — |
| Lignes de charge avec `TA_Code` absent de `F_TAXE` | 0 | 1 | — |
| Plan comptable TVA déductible | 6111… | 3455… | — |

**Côté ventes (encaissement)** :

| Indicateur | NEW_EMA | LOTREX |
|---|---|---|
| Écritures client lettrées | **0** (1 585 lignes débit VTE + 685 RAN, aucune lettrée) | oui |
| Règlements clients lettrés en banque : rapprochés / total | 0 / 0 | BQ4 136/586, BQ5 185/431, BQ2 0/1 |
| Règlements clients lettrés en caisse (`CA`) | 0 | 46, aucun rapprochement |
| Pièces de vente multi-taux | 1 296 | 0 |

⚠ **L'encaissement ne peut pas être testé sur EMA** : la règle « facture lettrée » n'y produit aucune ligne, faute de lettrage client. Test encaissement = LOTREX. Question PO ouverte §7.7.

Conséquences :
1. **Ne pas coder en dur** : préfixe tiers (`F%` faux sur LOTREX), compte collectif (4411), comptes de TVA, journaux. Tout vient de `F_COMPTET`/`F_TAXE`/`F_JOURNAUX` ou de la configuration.
2. **Le besoin de rapprochement dépend du journal** (`F_JOURNAUX.JO_Rappro=2`), pas du type « banque » : LOTREX rapproche aussi certaines caisses. Règle par défaut proposée : rapprochement exigé **si et seulement si** le journal du règlement a `JO_Rappro=2`. **À confirmer par le PO** (le PO a dit « autre mode de règlement, pas de rapprochement »).
3. **`F_TAXE` n'est pas injective sur le compte** : en EMA, `D07` (7 %) et `D18` (18 %) partagent 6111400000 ; `D18` a `TA_Type=1`. Donc le taux se déduit du `TA_Code` de la ligne de charge, jamais du compte de TVA seul. Seul `TA_Type=0` (pourcentage) est supporté ; tout autre type est une alerte bloquante.
4. **Multi-taux** : 335 pièces d'achat multi-taux en EMA. Le « cas simple » du PO (un compte de charge = un taux) se traduit par : HT par taux = Σ des lignes de charge de même `TA_Code`, TVA constatée = Σ des lignes sur `F_TAXE.CG_Num`.
5. **Règlements partagés** : un lettrage peut regrouper plusieurs factures et/ou plusieurs règlements (et des avoirs : 65 lettrages achat/achat en EMA). Les lettrages sont toujours équilibrés (0 partiel) → le code doit **détecter** un déséquilibre et le bloquer, pas le corriger.
6. **182 règlements bancaires lettrés non rapprochés** en EMA = flux de contrôle indispensable (« lettrée avec banque mais non rapprochée » = non déclarable tant que non rapproché).

## 4. Règles de calcul proposées (à valider PO avant TASK-227)

- **Une facture lettrée avec un seul règlement** : TVA déclarée en totalité à la date du règlement.
- **Une facture lettrée avec plusieurs règlements** : prorata `HT_taux × règlement / TTC_facture` à la date de chaque règlement (une seule opération, arrondi `AwayFromZero`, pas de ratio intermédiaire arrondi — leçon du bug GRFN §2).
- **Un règlement lettré avec plusieurs factures** (cas N:M) : répartition proportionnelle de chaque règlement sur les factures (`part = règlement × TTC_facture / Σ TTC factures du lettrage`). Somme conservée, déterministe.
- **Avoir** (facture ACH au débit du fournisseur) : TVA signée négative à la date du règlement ou de la compensation. À confirmer PO.

## 5. Architecture

```
Sage compta (SQL lecture seule)  ──►  Declaration.Compta.Reader  ──►  contrats (DTO facture/règlement)
                                           │
                       Declaration.Compta.Core (pur : allocation, contrôle HT×taux)
                                           │
                    adaptateur source  ──►  Declaration.Core (INCHANGÉ) ──► exports XML / Excel
                                           │
              DeclaratifMaroc (EF Core, migrations auto) : config dossier, validations d'écarts, lignes déclarées
```

- **Idempotence dès la conception** (leçon TASK-224) : index unique sur (écriture de règlement `cbMarq`, écriture de facture `cbMarq`, taux) dans la table des lignes déclarées.
- Identité de ligne = `cbMarq` (jamais `EC_Piece` seul). Regroupement d'une pièce = (`JO_Num`, `JM_Date`, `EC_Piece`) à valider contre collisions en TASK-226.
- La base DeclaratifMaroc ne contient **aucune table Sage** et ne modifie **aucune** table winform/Sage (règle absolue du PO).
- Identifiants Sage de lecture : chiffrés (DPAPI machine), jamais renvoyés en clair par l'API.

## 6. Plan de TASKS

| Ordre | TASK | Objet | Dépend de |
|---|---|---|---|
| 1 | TASK-225 | Socle : projets, base DeclaratifMaroc, EF Core + migrations auto, configuration dossier (connexion Sage, colonnes IF/ICE) | — |
| 2 | TASK-226 | Lecteur compta : factures d'achat lettrées → HT/TVA/TTC par taux | 225 |
| 3 | TASK-227 | Règlements lettrés : rapprochement, date opération, allocation/prorata | 226 |
| 4 | TASK-228 | Contrôle HT × taux = TVA, écart affiché, validation utilisateur persistée | 226, 225 |
| 5 | TASK-229 | Flux comptes de taxe sans tiers (dépenses, frais bancaires) | 226, 228 |
| 6 | TASK-230 | Branchement moteur commun, API et écrans de contrôle | 227, 228, 229 |
| ∥ | TASK-231 | **Charte graphique xGR** : tokens, sidebar/topbar noir-vert, KpiCard, tags, dark mode | — (parallèle au back V2) |
| ∥ | TASK-232 | **XGrid React** : portage de `XGrid.vue` (remplace ApbsGrid) | 231 |
| 7 | TASK-233 | Migration des écrans V1 (5 lots) vers charte + XGrid, retrait AG Grid | 231, 232 |

Arbitrages PO du 16/10/2026 : (a) **XGrid porté en React** (le front reste React, pas de réécriture en Vue) ; (b) **charte + XGrid avant le front V2**, donc les écrans V2 des TASK-225/228/229/230 sont construits directement dessus. Le back V2 (226, 227, et la logique de 228/229) avance en parallèle de 231/232.

## 8. Arbitrages PO du 16/10/2026 (priment sur §3.2, §4 et §7)

| Sujet | Décision |
|---|---|
| GOCOM | Base **commerciale** (`F_COMPTET`, `F_DOCENTETE`, `F_DOCLIGNE`), sans écriture comptable. Sa base compta reste à identifier (TASK-235, analyse 13). |
| Rapprochement | **Banque seulement**, sur le **compte de trésorerie** du journal. Tout paramétrage différent → **alerte système** (TASK-234). Autres règlements : pas de rapprochement. |
| Avoirs | Sens opposé, **traités plus tard** ; détectés et signalés en attendant. |
| Report | Un règlement non déclaré **apparaît toujours jusqu'à sa déclaration**. |
| Clients | Toujours sur le **lettrage**. |
| Validation d'écart | Liée à la **facture** (montants figés, invalidée si l'écriture change). |
| Imputation n:m | **Proportionnelle** (PO 16/10/2026). Règle détaillée dans TASK-227. |

**Imputation n:m — de quoi s'agit-il ?** Un seul lettrage peut regrouper plusieurs factures et plusieurs règlements. Sage ne dit pas quel règlement paie quelle facture. Exemple : factures A = 300 TTC et B = 700 TTC ; règlements P1 = 600 (juin) et P2 = 400 (juillet). La TVA de chaque facture doit être répartie entre les règlements, donc entre les mois.
- **Proportionnelle** : chaque règlement paie chaque facture au prorata de son poids. P1 → A 180 et B 420 ; P2 → A 120 et B 280. Résultat indépendant de l'ordre, mais aucune facture n'est « soldée » à une date précise.
- **Chronologique (FIFO)** : on solde les factures les plus anciennes avec les premiers règlements. P1 (600) → A 300 puis B 300 ; P2 (400) → B 400. Plus intuitif comptablement, mais dépend de l'ordre des dates.
Recommandation : à décider après les statistiques de TASK-235 (analyse 6) : si ces cas sont rares, la règle importe peu ; ils sont listés en attendant.

## 7. Points encore ouverts (historique, voir §8)

1. **GOCOM** : aucune écriture comptable dans `GOCOM`. Quelle base contient sa compta Sage ?
2. **Règle de rapprochement par journal** (§3.2) : exiger `EC_TresoPiece` uniquement si `JO_Rappro=2` — OK ?
3. **Règles §4** (prorata N:M, avoirs) — OK ?
4. **Période** : la déclaration d'un mois = règlements dont la date opération tombe dans le mois ; les règlements non encore déclarés d'une période antérieure remontent-ils (report), comme en V1 ?
5. **Tiers** : fournisseurs identifiés par `F_COMPTET.CT_Type=1` (pas par préfixe) — OK ?
6. **Écart validé** : la validation est-elle liée à la facture (permanente) ou à la déclaration ? Proposition : à la facture, avec montants figés ; toute modification de l'écriture invalide la validation.
7. **Clients non lettrés (EMA)** : sur cette base, aucune écriture client n'est lettrée. Pour l'encaissement, la facture « réglée » se détermine-t-elle toujours par le lettrage (EMA ne produira alors aucune ligne de TVA collectée) ? Ou faut-il un repli (par exemple règlement identifié par référence/pièce) ? Proposition : lettrage uniquement, et un contrôle signale les règlements clients non lettrés.
8. **Encaissement : export** : le schéma XML DGI « chiffre d'affaires / TVA facturée » reste introuvable. Livraison V2 encaissement = grilles de contrôle + Excel, sans XML, tant que le PO n'a pas fourni la spécification.
