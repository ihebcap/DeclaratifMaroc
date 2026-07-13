# TASK-064 — Trigger : immuabilité TOTALE d'une affectation déclarée

> **Origine :** demande PO 13/07/2026. Le verrou TASK-028 bloque le DELETE et l'UPDATE des seules
> colonnes financières d'une affectation tamponnée (`DT_Id NOT NULL`). Le PO veut qu'**aucune**
> modification d'une affectation déclarée ne soit possible tant que la déclaration n'est pas rouverte,
> pas seulement les colonnes financières.

## Constat (preuve code, aucune supposition)
1. **DELETE déjà bloqué** — `Declaration.Infrastructure/SQL/003_Verrou_DT_Id.sql:59-73` (bloc 1a) :
   interdit la suppression d'une affectation `DT_Id IS NOT NULL`.
2. **UPDATE partiellement bloqué** — `003_Verrou_DT_Id.sql:83-117` (bloc 1b) : n'interdit l'UPDATE que
   si l'une des colonnes **financières/structurantes** change (`AF_Montant`, `MV_Id`, `EC_Id`, `AF_Date`).
   → Toute autre colonne de `RT_AFFECTATION` reste **modifiable** sur une affectation déclarée.
3. **Transition de tampon à préserver** — le trigger autorise volontairement `DT_Id : valeur → NULL`
   (dé-tamponnage = réouverture, `003_Verrou_DT_Id.sql:87-88`). Cette porte doit rester ouverte, sinon
   `ReouvriDeclarationAsync` (`DeclarationWorkflowService.cs:453-456`) ne peut plus détamponner.

## Objectif
Étendre le bloc 1b pour interdire **tout UPDATE** d'une affectation déclarée (`DT_Id NOT NULL` avant ET
après), quelle que soit la colonne touchée — **sauf** la transition de dé-tamponnage `DT_Id : valeur → NULL`
(réouverture) qui reste autorisée.

## Périmètre
### Fichier unique : `Declaration.Infrastructure/SQL/003_Verrou_DT_Id.sql`
Remplacer la condition « colonnes financières » du bloc 1b par une condition d'immuabilité totale :
- **Interdire** si `deleted.DT_Id IS NOT NULL` (était déclarée) **ET** `inserted.DT_Id IS NOT NULL`
  (reste déclarée = pas un dé-tamponnage). Dans ce cas, **tout** UPDATE est refusé, sans énumérer les
  colonnes.
- **Autoriser** la transition `deleted.DT_Id NOT NULL → inserted.DT_Id NULL` (réouverture) : ne pas
  déclencher le THROW dans ce cas.
- Conserver le bloc 1a (DELETE) inchangé.
- Conserver le trigger `RT_MOUVEMENT` (bloc 2) inchangé.

### Message d'erreur
Adapter le message du THROW 50028 : « affectation incluse dans la déclaration TVA n° X — toute
modification est refusée. Rouvrez la déclaration avant correction. » (retirer la mention « colonnes
financières » devenue trop restrictive).

## Points tranchés / à vérifier
1. **Dé-tamponnage combiné à un autre changement — TRANCHÉ : REFUSÉ** (décision architecte 13/07/2026).
   N'autoriser que le dé-tamponnage PUR : dans le même UPDATE, seul `DT_Id` change et passe valeur → NULL.
   Tout UPDATE qui met `DT_Id → NULL` **et** touche une autre colonne est refusé (une réouverture ne doit
   pas servir de cheval de Troie pour modifier). Traduction trigger : la clause d'autorisation ne s'applique
   que si `inserted.DT_Id IS NULL` **et** toutes les autres colonnes protégées sont inchangées.
2. **Colonnes système** (rowversion/horodatage technique éventuel mis à jour par un autre trigger) : si
   la base porte une colonne technique auto-mise-à-jour, l'immuabilité totale la bloquerait. Vérifier sur
   `GR_EMA_DISTRIBUTION` qu'aucune colonne de `RT_AFFECTATION` n'est écrite automatiquement par un flux
   legitime hors GRFN. **Aucune colonne de ce type identifiée** dans le script actuel — à re-confirmer sur
   la base réelle avant déploiement prod.

## Garde-fous
1. **Ne pas casser la réouverture** : la transition `DT_Id valeur → NULL` doit rester autorisée
   (test explicite requis, voir VERIFY).
2. **Portée globale GRFN conservée** : le trigger s'applique à tous les writers (GRFN/Sage legacy inclus),
   comme TASK-028. Ne pas ajouter de filtre d'application.
3. **Index non filtré conservé** : ne pas transformer `IX_RT_AFFECTATION_MV_Id_DT_Id` en index filtré
   (contrainte `QUOTED_IDENTIFIER` sur les writers legacy — voir §0 du script).
4. **Idempotence** : le script doit rester ré-exécutable (DROP/CREATE trigger déjà en place).

## Livrables de preuve (VERIFY)
1. `003_Verrou_DT_Id.sql` diffé : bloc 1b généralisé, message adapté, blocs 1a et 2 inchangés.
2. Test réel sur base : sur une affectation `DT_Id NOT NULL`,
   - UPDATE d'une colonne **non financière** (ex. libellé/commentaire) → **refusé** (THROW 50028).
   - UPDATE d'une colonne financière → toujours refusé.
   - `UPDATE ... SET DT_Id = NULL` (dé-tamponnage pur) → **autorisé**.
3. Non-régression `ReouvriDeclarationAsync` : réouverture d'une déclaration clôturée fonctionne toujours
   (détamponnage effectif, affectation redevenue modifiable).

## Dépendances / risques
- **Dépend de TASK-028** (livré) : réutilise le même trigger et le même tampon `DT_Id`.
- **Risque flux legacy** : une immuabilité totale peut bloquer un UPDATE legitime GRFN/Sage sur une
  affectation déjà déclarée (ex. re-pointage). C'est l'intention métier (verrou), mais à valider avec le
  PO que plus AUCUN flux automatique ne doit toucher une affectation déclarée hors réouverture.
