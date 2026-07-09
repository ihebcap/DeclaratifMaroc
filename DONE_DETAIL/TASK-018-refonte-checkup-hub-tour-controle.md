# TASK-018 — Refonte du Checkup en hub « tour de contrôle » (remplace le tunnel)

## Contexte
Le front (TASK-013 ✅, ajusté TASK-016 ✅) organise la déclaration en **tunnel linéaire** :
Décaissement → Encaissement → Dépense → Frais bancaire → **Checkup**. Le Checkup y est la **dernière
étape** (`CheckupPanel.tsx`) : équilibre, réconciliation, récaps, anomalies, clôture gardée.

**Décision PO (08/07/2026) :** l'écran actuel ne « vend » pas la valeur du produit et gaspille de
l'espace. Le Checkup doit devenir le **poste de pilotage** de la déclaration, **pas** l'étape finale d'un
tunnel. La valeur à mettre en avant est celle de tout le module : **la preuve que rien ne disparaît en
silence** (cf. règle n°1 TASK-013, [[grf-objectif-confiance-transparence]]).

> ⚠️ **C'est une refonte de la navigation du front, assumée par le PO.** Elle revient sur le principe
> « pas de refonte » de TASK-016. Les **6 règles de transparence de TASK-013 restent souveraines** et
> priment sur tout choix d'UI de cette task.

Une **maquette cliquable** validant la direction existe déjà (hub + preuve d'équilibre + barre de
ventilation cliquable + drill-down par source + clôture verrouillée). Elle sert de référence visuelle,
**pas** de spéc de chiffres (données fictives).

## Périmètre STRICT
- **Uniquement** : le front `declaration-tva-web` — remplacer le tunnel par un **hub Checkup** et
  transformer les domaines en **tuiles de drill-down**. Réutilise l'existant (`CheckupPanel`,
  `DomainGrid`, `ExcelFilter`, navigation, drill-down filtré TASK-016).
- **Exclu** : tout changement de calcul/sélection/persistance (back TASK-012/017), le contrat d'API
  public (mêmes endpoints/DTO). Si un agrégat manque au payload checkup, l'ajouter **d'abord sur le
  mock** (`mockServer.ts`) et signaler l'avenant contrat — sans réimplémenter le back.

## Objectif
Faire du **Checkup l'écran d'accueil d'une déclaration ouverte**, qui **remplace** le stepper-tunnel.

### 1. Hub « tour de contrôle » (remplace le tunnel)
- À l'ouverture d'une déclaration `EnCours`, on arrive **directement sur le hub**, pas sur le 1ᵉʳ domaine.
- Les 4 domaines ne sont plus des **étapes obligées** mais des **tuiles de drill-down** ouvrables à la
  demande (chargement par domaine conservé — contrainte volume TASK-013 intacte).
- Le hub **reste le fil conducteur** (règle n°3 TASK-013) : il montre en permanence *où on en est* et
  *le prochain pas* (domaines restant à traiter). L'intention « l'utilisateur ne se perd jamais » est
  **préservée** — seule la forme « tunnel linéaire » disparaît.

### 2. Héros = preuve de transparence (l'argument de vente)
- **Équilibre** bien visible : `Σ sources = Σ candidates` + **écart** (badge vert si 0).
- **Barre de ventilation** des lignes candidates, segmentée : *Intégrées / Exclues / Reportées /
  Écartées / Proposées*, avec le message clair **« 0 ligne silencieuse »** (règles n°1 et n°2).
- **Compteurs cliquables** (chips) → ouvrent le **drill-down filtré** correspondant (réutilise le
  mécanisme TASK-016 : ex. clic « Écartées » → grille du domaine filtrée `statutLigne=Écartée`, motif
  visible). Chaque exclusion reste **visible et justifiable** (règle n°6, traçabilité).

### 3. Tuiles source (efficacité de traitement)
- Chaque tuile : nom + **avancement** (`n traitées / n proposées`), barre de progression, badge
  « n à décider ». Clic → drill-down du domaine (grille serveur existante).

### 4. Clôture depuis le hub
- Le bouton **Clôturer** vit **dans le hub** et reste **verrouillé** tant qu'il reste des lignes non
  décidées / une anomalie bloquante (gating TASK-013 conservé). Libellé explicite du **reste à faire**
  (ex. « Clôturer · N à traiter »).

### 5. Densité / espace
- Supprimer l'espace mort actuel : ventilation en **barre visuelle** plutôt que deux gros nombres qui
  flottent ; récaps par source / par taux **masqués tant que vides** (état clair au lieu de « Aucune
  donnée » qui occupe la hauteur).

## Contraintes techniques
- **6 règles de transparence TASK-013 souveraines** — la refonte ne doit **rien** masquer : toute ligne
  écartée reste atteignable avec motif ; la réconciliation boucle toujours.
- **Volume** : chargement par domaine **à la demande** conservé ; le hub lit des **agrégats** (checkup),
  jamais l'ensemble des lignes (contrainte n°5 TASK-013).
- **Réutiliser** `CheckupPanel`, `DomainGrid`, `ExcelFilter` (mode serveur), le drill-down + bulk-sur-
  filtre TASK-016, les toasts et le design gocom-web. **Pas** de nouveau composant lourd ni framework UI.
- Développable **sur mock** (`mockServer.ts`). Si la ventilation (5 statuts) n'est pas dans le payload
  `GET /checkup`, l'ajouter au mock et **répercuter l'avenant** au contrat TASK-012.
- React 19 + Vite + TS ; `oxlint` ; e2e `playwright`. `API_BASE` runtime + JWT Bearer inchangés.
- **Simple avant beau** : densité et lisibilité, pas de sur-UI.

## Étapes
1. Remplacer l'atterrissage tunnel par le **hub Checkup** (route/état : déclaration ouverte → hub).
2. Héros : **barre de ventilation** (5 segments) + équilibre + « 0 ligne silencieuse » ; brancher sur
   les agrégats checkup (compléter le mock si besoin).
3. **Chips ventilation cliquables** → drill-down filtré (réutilise TASK-016).
4. **Tuiles source** (avancement + « à décider ») → drill-down domaine ; retour au hub recalculé.
5. **Clôture dans le hub** avec libellé du reste-à-faire + gating conservé.
6. Densité : récaps masqués si vides ; retrait de l'espace mort.
7. e2e Playwright : ouvrir déclaration → hub → clic chip/tuile → drill-down filtré → décision →
   retour hub recalculé → clôture verrouillée tant que reste-à-faire.

## Livrables
- Front `declaration-tva-web` refondu : hub Checkup en écran d'accueil, domaines en drill-down.
- Mock `checkup` complété si besoin (ventilation 5 statuts) + **avenant contrat** signalé à TASK-012.
- Tests Playwright du parcours hub.
- `VERIFY/TASK-018_verify.md` : captures (hub, barre de ventilation, chip → grille filtrée, tuile →
  drill-down, clôture verrouillée avec reste-à-faire) + preuve que la réconciliation boucle et
  qu'aucune ligne écartée n'est masquée.

## Critères de validation
- Ouvrir une déclaration `EnCours` arrive **sur le hub**, pas sur un domaine.
- Équilibre `Σ sources = Σ candidates` + écart affichés ; **barre de ventilation** couvrant 100 % des
  candidates (Intégrées+Exclues+Reportées+Écartées+Proposées = Total) → **réconciliation bouclée**.
- Clic sur un compteur/tuile ouvre le **drill-down filtré** attendu ; motifs des écartées visibles.
- **Clôture verrouillée** tant qu'il reste des lignes à décider ou une anomalie bloquante ; libellé du
  reste-à-faire correct.
- Aucune régression des 6 règles de transparence (rien de masqué, tout expliqué).
- Récaps masqués tant que vides ; densité améliorée (plus d'espace mort du Checkup actuel).
- `oxlint` + build tsc/vite OK ; parcours e2e Playwright vert.

## Risques / dépendances
- **⚠️ GATÉE — validation données réelles** : décision PO de valider la direction **sur données réelles**
  avant lancement (cf. [[grf-checkup-hub-maquette]]). Ne pas démarrer tant que le PO n'a pas confirmé le
  rendu réel (base `GR_EMA_DISTRIBUTION`, TASK-001/017).
- **Refonte de navigation** : revient sur « pas de refonte » (TASK-016) — décision PO explicite, tracée.
  Risque de régression sur le « fil conducteur » (règle n°3) → le hub doit toujours montrer *état +
  prochain pas*.
- **Payload checkup** : si la ventilation 5 statuts n'y est pas, avenant contrat TASK-012 à répercuter
  (comme l'avenant `:bulk` de TASK-016).
- **À trancher au démarrage** (PO) : (a) faut-il un **indicateur de progression global** (ex. anneau
  « % traité ») en tête de hub ? (b) le stepper linéaire est-il **retiré** ou **conservé en secours**
  pour qui préfère le parcours guidé ?
