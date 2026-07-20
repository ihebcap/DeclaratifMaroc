# TASK-112 — Drill « Cohérence des totaux déclarés » : filtre tautologique, l'écart n'est jamais isolé

## Origine

Signalement PO 17/07/2026 (capture écran, `TVA1-2026-01`, onglet Décaissement, écran ② « Vérifier
& Intégrer ») : « lorsque je clique Cohérence des totaux déclarés il me ramène vers un écran pour
voir les anomalies mais il m'affiche toutes les données sans filtre » (228 résultats, 3 pages).

Ce point était le suivi laissé ouvert par **TASK-110** (§ Risques : « signalement PO sur le filtre
Source reste non confirmé côté code — à clarifier avant tout correctif »). Il est désormais
**confirmé et expliqué** : le filtre est bien appliqué, mais il ne retire rien.

## Contexte

- `handleDrillSource` (`VerifierIntegrerPanel.tsx:377-384`) construit, depuis l'onglet Décaissement :
  `domaine: 'Decaissement'` + `filtre: { source: ['Decaissement'] }`.
- Or `DM_LGTVA.Source` vaut déjà `Decaissement` pour **toutes** les lignes du domaine Décaissement
  (enum `SourceAffectation`, valeurs `Decaissement`/`Encaissement`/`Espece`/`Depense`/
  `Frais bancaire`). Le récap est groupé sur cette même colonne
  (`DeclarationsController.cs:241-252`), et `sourceBelongsToDomain`
  (`VerifierIntegrerPanel.tsx:370-372`) restreint déjà l'affichage au domaine de l'onglet.
- Conséquence : filtrer le domaine Décaissement par « source = Décaissement » est une tautologie →
  `AND Source IN ('Decaissement')` (`DeclarationRepository.cs:417-425`) laisse passer les 228 lignes.
  La chaîne technique tracée par TASK-110 est correcte de bout en bout ; c'est le **postulat** de
  TASK-107 qui est faux : `Source` n'est pas un axe discriminant, c'est un doublon du domaine.
- Défaut de fond au-delà du filtre : le contrôle affiche « Écart détecté : X MAD » et le drill est
  censé donner à voir **cet écart**. Il restitue l'ensemble déclaré (Integree + Proposee,
  `DeclarationsController.cs:239`). Aucune ligne du drill ne répond à la question « d'où vient
  l'écart ». Même filtre corrigé, un drill sur un axe non lié à l'écart ne répond pas au besoin.
- L'axe réellement discriminant visible à l'écran est **Origine** (`Sage` / `FGR`), pas `Source`.

## Périmètre STRICT

- **Inclus** :
  1. Décision d'axe (arbitrage PO requis, cf. Bloquant) : sur quoi le récap sous le badge ÉCART
     doit grouper, et ce que le drill doit filtrer pour **isoler l'écart** et non l'ensemble déclaré.
  2. `VerifierIntegrerPanel.tsx:377-384` (`handleDrillSource`) + `RecapSourceTable.tsx:85` :
     alignement sur l'axe retenu.
  3. `DeclarationsController.cs:241-252` (`recapSource`) : groupement sur l'axe retenu si celui-ci
     n'est plus `Source`.
- **Exclu** :
  - Toute correction de mise en page / alignement de colonnes de la grille → **TASK-113** (défaut
    distinct, ne pas mélanger).
  - Le calcul de l'écart lui-même (`DeclarationWorkflowService`, artefact d'ensembles TASK-108) —
    non remis en cause ici, seule sa **restitution** est en cause.

## Objectif

```
Entrée  : contrôle "Cohérence des totaux déclarés" en état ÉCART sur ② Vérifier & Intégrer
Traitement : le récap groupe sur un axe réellement discriminant (arbitrage PO) ; le clic filtre les
             lignes qui COMPOSENT l'écart, pas l'ensemble déclaré
Sortie  : drill affichant un sous-ensemble strict et explicable des lignes, dont le total se
          rattache à l'écart annoncé ; aucun cas où le drill renvoie 100 % des lignes du domaine
```

## Livrables

- `VerifierIntegrerPanel.tsx` / `RecapSourceTable.tsx` / `DeclarationsController.cs` alignés sur
  l'axe arbitré.
- Preuve réelle `GR_EMA_DISTRIBUTION` (`TVA1-2026-01`, Décaissement) : nombre de lignes du drill
  < nombre de lignes du domaine, et rattachement explicite à l'écart affiché.
- `VERIFY/TASK-112_verify.md` : build OK, capture avant/après, preuve du rattachement à l'écart.

## Critères de validation

- Le drill ne renvoie **jamais** l'intégralité des lignes du domaine (cas tautologique éliminé).
- Le lien entre les lignes affichées et l'écart annoncé est explicite et vérifiable par le PO.
- Aucune régression sur le drill anomalie (TASK-088) ni sur le drill affectations (TASK-092/111).
- Principe « aucune ligne silencieuse » préservé : si l'écart n'est pas explicable par l'axe retenu,
  le dire à l'écran plutôt que d'afficher un sous-ensemble trompeur.

## Risques / dépendances

- **BLOQUANT — arbitrage PO requis avant implémentation** : axe de groupement du récap (Origine
  Sage/FGR ? taux TVA ? statut de ligne ?). TASK-107 avait été arbitrée « option 3 » sur `Source` ;
  cet arbitrage est invalidé par la tautologie constatée. Ne pas coder avant réponse.
- Si l'écart provient d'un **manque** (lignes attendues absentes) et non d'un excès, aucun filtre sur
  les lignes présentes ne peut le montrer — le drill devrait alors afficher l'artefact d'ensembles
  de TASK-108 plutôt qu'une grille de lignes. À trancher dans le même arbitrage.
- `SOURCE_LABELS` (`RecapSourceTable.tsx:6-15`) contient des clés mortes/incohérentes (`ACHAT`,
  `VENTE`, `Décaissement` accentué vs `Decaissement` renvoyé par le back) — dette à nettoyer si
  l'axe `Source` est abandonné.

## NOTES

Analyse de code uniquement (rôle architecte, aucun accès UI). Lignes vérifiées directement :
`VerifierIntegrerPanel.tsx:370-384/461-506/973-981/1049-1059`, `RecapSourceTable.tsx:6-19/66-99`,
`DeclarationsController.cs:220-252/298-312`, `DeclarationRepository.cs:417-425/531-554`.
Clôt le suivi ouvert par TASK-110 § Risques (« filtre Source non confirmé ») : confirmé, cause
identifiée, requalifié en défaut de conception de TASK-107 et non en bug d'implémentation.
