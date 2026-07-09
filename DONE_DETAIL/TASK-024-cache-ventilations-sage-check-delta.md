# TASK-024 — Cache des ventilations Sage (gel par le paiement, validé à la lecture)

## Contexte
Cible **durable** issue de la discussion perf (retour worker TASK-009) : matérialiser les ventilations TVA
des factures **Sage (`EC_Type = 0`)** dans une **table GRF** (une copie SQL du résultat OM), pour relire une
déclaration **100 % SQL** (les FGR le sont déjà via TASK-022), sans rappeler l'OM (~0,5-1 s/facture).

Bénéfice double : perf (0 OM en relecture — contrôle/recalcul TASK-009) **et transparence** (une table cache
est **auditable** : on voit quoi a été lu, quand, depuis quelle source).

## Modèle de fraîcheur — **le paiement gèle la facture, validé à la lecture** (`cbModification` abandonné)
Décision PO : **on ignore complètement tout marqueur de modification Sage** (`cbModification` & co). La seule
preuve de fraîcheur = l'**immuabilité de la facture, garantie par le paiement**, pas par la déclaration :

- **C'est le règlement/paiement qui bloque la facture.** Une facture « passée au règlement » est **clôturée
  dans Sage** → sa ventilation (HT/TVA par taux) ne peut plus changer. **≠ la déclaration** : le `DT_Id`
  (TASK-028) sert l'intégrité de la déclaration (anti-re-déclaration / anti-dérapprochement), pas la
  cacheabilité de la facture.
- **Le module est en TVA sur encaissement** (piloté par le règlement — [[grf-modele-comptable-4-interrogations]]) :
  une facture **n'entre dans le périmètre que parce qu'elle est payée** → **toute facture ventilée est déjà
  gelée par le paiement, par construction**.
- **Pas de détection d'événement — validation à la lecture.** On **ne peut pas** modifier le code de GRFN
  (boîte noire) pour être « prévenu » d'un dé-paiement, et on ne veut pas dépendre d'un tel événement. À la
  place, le lecteur **revérifie l'état de paiement local** avant de servir une ligne de cache :
  - facture **toujours payée** (affectation présente + `MV_Point = 1`, mêmes critères que la sélection) →
    cache servi (0 OM) ;
  - paiement **défait/absent** → cache **ignoré**, relecture OM (puis réécriture).
  On ne vérifie **jamais Sage** : uniquement le **lien de paiement local** (`RT_AFFECTATION`/`RT_MOUVEMENT`),
  requête triviale, qui est de toute façon ce qui rend la facture éligible.
- Complémentarité TASK-028 : une facture **déclarée** ne peut plus être dé-payée (trigger) → cache de fait
  permanent ; une facture **payée non déclarée** est couverte par la validation à la lecture.

## Périmètre STRICT
- **Table cache GRF** : la ventilation Sage d'une facture (clé = facture/pièce `EC_Id`, buckets par taux) +
  **token de fraîcheur** = état de paiement au moment de l'écriture (réf. affectation / `MV_Id`, `MV_Point`) +
  métadonnées d'audit (date de lecture, source).
- **Écriture** : à la première lecture OM d'une facture payée (réutilise le batch **TASK-023**).
- **Validation à la lecture** : avant de servir le cache, requête SQL locale comparant le token stocké à
  l'état de paiement courant ; si divergence → ne pas servir, relire l'OM.
- **Lecteur unifié** : `EC_Type=0` → cache **si présent ET validé**, sinon OM (puis matérialisation).
- **Exclu** : FGR (déjà SQL via TASK-022), Type 4 (TASK-025), front, tout marqueur `cbModification`, toute
  modification du code GRFN et tout trigger de purge (inutile : validation à la lecture).

## Positionnement / architecture
- Table `RT_*` dédiée (isolation respectée) OU table applicative GRC_WEB — à trancher à l'implémentation
  selon la contrainte d'isolation `RT_*`.
- Le lecteur unifié devient : `EC_Type=111` → `RT_HISTOCOMPTA` ; `EC_Type=0` → **cache validé sinon OM** ;
  `EC_Type=4` → alerte (TASK-025).
- La prorata par règlement reste calculée à la lecture depuis les montants d'affectation ; le cache porte la
  **ventilation de la facture** (buckets par taux), pas le prorata.

## Objectif
```
Facture payée    : ventilation matérialisée à la 1re lecture OM → relectures ultérieures 100 % SQL (0 OM)
Fraîcheur        : garantie par le paiement (facture clôturée Sage), PAS par la déclaration
Validation lecture : token de paiement du cache == état local courant → servir ; sinon relire l'OM
Sans toucher GRF : aucun hook code, aucun trigger de purge ; la validation locale suffit
```

## Contraintes techniques
- `decimal`, isolation `RT_*`, `net10.0` côté lecteur ; la lecture OM s'appuie sur `SageTaxReader` (net8-windows).
- **Aucune ventilation servie du cache sans validation du paiement local** (token == état courant).
- La requête de validation doit être **locale et triviale** (mêmes critères que la sélection d'éligibilité) —
  jamais d'appel Sage.
- Aucun code GRFN modifié, aucun trigger de purge ajouté pour le cache.

## Étapes
1. Créer la table cache (clé facture/`EC_Id`, buckets par taux, **token de paiement**, date lecture, source).
2. Brancher l'écriture cache à la première lecture OM d'une facture payée (batch TASK-023) + y stocker le token.
3. Implémenter la **validation à la lecture** : requête SQL locale (affectation présente + `MV_Point=1`) ;
   token identique → cache fiable, sinon relecture OM.
4. Lecteur unifié : Type 0 → cache si présent **et** validé, sinon OM ; brancher l'orchestrateur.
5. Tests : 1re lecture → cache écrit + token ; 2ᵉ lecture même période = 0 OM ; simulation d'un dé-paiement
   (affectation retirée / `MV_Point→0`) → cache **non servi**, retour OM ; égalité cache↔OM sur échantillon.

## Livrables
- Table cache + écriture au 1er accès + validation à la lecture + lecteur unifié branché.
- `VERIFY/TASK-024_verify.md` : preuve qu'une 2ᵉ lecture ne rappelle plus l'OM, qu'un paiement défait invalide
  la lecture du cache (retour OM), égalité cache↔OM sur échantillon.

## Critères de validation
- 2ᵉ exécution d'une même période : **0 appel OM** pour les Type 0 déjà en cache et validés.
- Paiement défait (facture non déclarée) : cache **non servi**, la facture repasse par l'OM.
- Ventilations cache = ventilations OM sur échantillon (aucune divergence).
- Aucune ligne servie sans facture gelée par le paiement (token validé). **Aucun code GRF modifié.** Build + tests verts.

## Risques / dépendances
- **Fraîcheur = paiement** (pas la déclaration). TASK-028 **renforce** la garantie pour les factures déclarées
  (dé-paiement interdit) mais TASK-024 **ne dépend pas** de TASK-028 pour la cacheabilité.
- Dépend de **TASK-022** (routing/lecteur unifié) et **TASK-023** (batch pour la lecture OM).
- **Risque central** : servir du cache une facture dont le paiement a été défait = ventilation périmée
  (anti-transparence). La **validation à la lecture** l'élimine sans dépendre de GRF.
- Portée perf : gain sur la **relecture** (contrôle/TASK-009) et sur les périodes déjà lues ; la 1re lecture
  d'une facture reste un appel OM. Assumé.
