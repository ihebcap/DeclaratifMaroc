# TASK-168 — Filtre « N° Facture »/« Référence » tronqué à 500 valeurs, recherche uniquement locale

## Contexte
Diagnostiqué en session (23/07/2026) pendant l'investigation `FC2600515`/TASK-164 : le PO a signalé que
le filtre numéro de facture de l'écran Factures, avec la période réglée sur l'année 2026 complète,
n'affichait pas `FC2600515` alors que la facture existait bien dans cette période. Jamais formalisé en
task jusqu'ici — corrigé maintenant à la demande du PO.

## Constat (code confirmé, pas supposé)

### Le back-end plafonne les valeurs distinctes à 500, triées alphabétiquement
`DeclarationRepository.GetFacturesInterrogationDistinctsAsync`
([DeclarationRepository.cs:1231-1264](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs#L1231-L1264)) :
```csharp
private const int DistinctsTopBound = 500; // ligne 1017
...
var numeros = await connection.QueryAsync<string>(
    $@"SELECT DISTINCT TOP {DistinctsTopBound} E.DO_Numero AS Value {FacturesFromWhere}
       AND E.DO_Numero IS NOT NULL AND E.DO_Numero <> ''
       ORDER BY E.DO_Numero", baseParams);
```
Dès qu'une période contient plus de 500 numéros de facture distincts, seuls les 500 premiers par ordre
alphabétique sont renvoyés au front — tout numéro situé après le 500ᵉ (alphabétiquement) est invisible
dans le filtre, **quelle que soit la période choisie**, sans aucun signal à l'écran indiquant que la
liste est tronquée.

### Le front ne cherche que dans cette liste déjà tronquée, jamais côté serveur
`ExcelFilter.tsx` ([ExcelFilter.tsx:67-70](../declaration-tva-web/src/ExcelFilter.tsx#L67-L70)) :
```tsx
const filteredOptions = React.useMemo(() => {
  if (!isOpen) return [];
  return (options || []).filter(o => (o.label || '').toLowerCase().includes(searchTerm.toLowerCase()));
}, [options, searchTerm, isOpen]);
```
La recherche tapée par l'utilisateur ne filtre que le tableau `options` déjà reçu (donc déjà plafonné à
500) — aucun appel réseau n'est déclenché pour chercher au-delà. Taper le numéro exact d'une facture
absente des 500 premières valeurs alphabétiques ne la fait jamais apparaître, même si elle existe
réellement dans la période sélectionnée.

### Portée
Le même défaut existe pour la colonne « Référence » (`E.DO_Reference`, même requête plafonnée) et pour
l'équivalent Rapprochement (`RapprochementFromWhere`, lignes 989-999, `M.MV_Numero`/`M.MV_ExtraitNum`/
`B.BanqueCode`) — non vérifié si aussi impacté en pratique (volumes plus faibles a priori), à couvrir
par la même correction si le volume le justifie.

## Objectif
1. Rendre la recherche du filtre `N° Facture`/`Référence` **exhaustive**, pas limitée aux 500 premières
   valeurs alphabétiques de la période.
2. Approche recommandée : transformer `/factures/distincts` en endpoint **cherchable côté serveur** —
   ajouter un paramètre optionnel `recherche` (préfixe ou sous-chaîne) à
   `GetFacturesInterrogationDistinctsAsync`, appliqué en SQL (`AND E.DO_Numero LIKE @recherche + '%'`,
   ou `LIKE '%' + @recherche + '%'` si le PO veut une recherche par sous-chaîne) **avant** le `TOP 500` —
   le plafond reste utile comme garde-fou anti-charge sur le picklist par défaut (sans recherche), mais
   ne doit plus être la seule vue possible sur les valeurs réelles.
3. Front (`ExcelFilter.tsx`) : quand `filterType === 'list'` et que la recherche tapée ne trouve rien
   dans les options déjà chargées, déclencher un appel réseau debouncé vers l'endpoint avec le terme
   tapé (au lieu de se contenter du filtrage local) — même round-trip que celui qui a rempli `options`
   au départ, paramétré par le terme de recherche.
4. Signal explicite si la liste par défaut (sans recherche) est tronquée (ex. « 500+ valeurs, affinez
   votre recherche ») — jamais une troncature silencieuse.

## Garde-fous
- Lecture seule stricte, aucune écriture.
- Ne pas retirer le plafond `TOP 500` pur et simple (risque de charge sur une période à très fort
  volume sans terme de recherche) — le combiner avec la recherche, pas le supprimer.
- Respecter l'invariant TASK-040/067B : le `WHERE`/`FromWhere` utilisé pour les valeurs distinctes doit
  rester strictement le même que celui de la liste principale (période, société, domaine) — seul le
  filtre de recherche s'ajoute.

## Files
- [Declaration.Infrastructure/Repositories/DeclarationRepository.cs](../Declaration.Infrastructure/Repositories/DeclarationRepository.cs) (`GetFacturesInterrogationDistinctsAsync`, `DistinctsTopBound`, et l'équivalent Rapprochement si couvert).
- [Declaration.API/Controllers/FacturesController.cs](../Declaration.API/Controllers/FacturesController.cs) (`GET /factures/distincts`, nouveau paramètre `recherche`).
- [declaration-tva-web/src/ExcelFilter.tsx](../declaration-tva-web/src/ExcelFilter.tsx) (recherche debouncée côté serveur en complément du filtrage local).

## Validation
- [ ] Build back + front OK.
- [ ] Sur une période réelle avec >500 numéros distincts, taper un numéro situé après le 500ᵉ
      alphabétique le fait apparaître dans le filtre (preuve réelle, base `GR_EMA_DISTRIBUTION`).
- [ ] Aucune régression sur le comportement par défaut (liste des 500 premières valeurs, sans recherche)
      ni sur l'invariant TASK-040/067B (même `WHERE` que la liste principale).
- [ ] Tests existants `Declaration.Orchestration.Tests`/contrôleur rejoués verts.

## Risques / dépendances
- Risque faible : lecture seule, changement additif (nouveau paramètre optionnel, comportement par
  défaut inchangé si `recherche` absent).
- Dépend d'un arbitrage mineur PO : recherche par préfixe (`LIKE @x + '%'`, utilise un index) vs
  sous-chaîne (`LIKE '%' + @x + '%'`, plus permissif mais scan complet) — à trancher selon le volume
  réel de numéros de facture par société.
