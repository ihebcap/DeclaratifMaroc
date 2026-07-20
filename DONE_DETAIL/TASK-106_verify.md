# TASK-106 Verify — Token de paiement espèce non valorisée (`MV_Point` bornée à `Espece`)

> Preuves produites sur **SQL Server `.\sql2022` / `GR_EMA_DISTRIBUTION`** — même moteur et
> même base que la production, lecture seule (aucune ligne créée/modifiée/supprimée).

## Cause racine (rappel)

`VentilationSageCacheRepository.GetCurrentPaiementToken` (`Declaration.Orchestration/VentilationSageCacheRepository.cs:48`)
exigeait `MV_Point = 1` pour **tout** `EC_Id`, y compris l'espèce — alors que
`SelectionExpliqueeEvaluator.cs:85-90` exempte déjà l'espèce de `MV_Point` à l'éligibilité.
Conséquence : un règlement espèce sélectionné franchissait l'éligibilité mais recevait un
token `NULL` à la valorisation → facture traitée comme « non/dépayée », disparition
silencieuse de la ligne (aucune TVA déclarée sur règlement espèce).

## Correctif appliqué

`GetCurrentPaiementToken` reconnaît désormais un règlement comme payé si :
- `MV_Point = 1` (comportement inchangé, tous domaines), **OU**
- `MV_Domaine = 1` (Fournisseur) **ET** `MV_Type = 0` (Espèce) — bornage **strict** à la
  source Espèce, miroir exact de `SelectionExpliqueeEvaluator`.

Aucune modification de la sélection/éligibilité (hors périmètre, déjà correcte).

## Preuve sur données réelles — les 8 règlements cités dans TASK-106

Résolution EC_Id réelle (lecture seule, `RT_MOUVEMENT` ⋈ `RT_AFFECTATION`) :

| Règlement | MV_Domaine | MV_Type | MV_Point | EC_Id | Token AVANT correctif | Token APRÈS correctif |
|---|---|---|---|---|---|---|
| RF26010010 | 1 (Fournisseur) | 0 (Espèce) | 0 | 18233 | `NULL` | ✅ non nul |
| RF26010011 | 1 | 0 | 0 | 18268 | `NULL` | ✅ non nul |
| RF26010012 | 1 | 0 | 0 | 18267 | `NULL` | ✅ non nul |
| RF26030013 | 1 | 0 | 0 | 18227 | `NULL` | ✅ non nul |
| RF26030014 | 1 | 0 | 0 | 18211 | `NULL` | ✅ non nul |
| RF26030015 | 1 | 0 | 0 | 18266 | `NULL` | ✅ non nul |
| RF26030016 | 1 | 0 | 0 | 18319 | `NULL` | ✅ non nul |
| RF26030017 | 1 | 0 | 0 | 19866 | `NULL` | ✅ non nul |

**Résultat : 8/8 règlements espèce produisent désormais un token de paiement non nul**
(contre 0/8 avant correctif) — confirmé par le test automatisé
`Espece_ReglementsReels_TASK106_TokenNonNul_8sur8`.

## Non-régression stricte — bornage à `Espece`

| Règlement | MV_Domaine | MV_Type | MV_Point | EC_Id | Comportement attendu | Résultat |
|---|---|---|---|---|---|---|
| RF26010005 | 1 | 3 (Virement) | 0 (non rapproché) | 18269 | reste `NULL` (non déclarable) | ✅ `NULL` confirmé |
| RF26010001 | 1 | 3 (Virement) | 1 (rapproché) | 18219 | token non nul (inchangé) | ✅ non nul confirmé |

Un non-espèce non rapproché **reste rejeté** — aucune extension de l'exemption `MV_Point`
au-delà de la source `Espece` (pas de régression fiscale sur TASK-099).

## Tests automatisés

Fichier : `Declaration.Orchestration.Tests/Task024CacheVentilationSageTests.cs`
(classe `Task106TokenEspeceTests`, exécution directe contre `VentilationSageCacheRepository`
réel, sans stub, en lecture seule sur données réelles) :

```
Espece_ReglementsReels_TASK106_TokenNonNul_8sur8              ✅ PASS
NonEspece_NonRapprochee_TokenResteNull_NonRegressionTask099   ✅ PASS
NonEspece_Rapprochee_TokenNonNul_ComportementInchange         ✅ PASS
```

Suite complète `Declaration.Orchestration.Tests` (non-régression globale) :

```
dotnet test Declaration.Orchestration.Tests
Réussi ! - échec : 0, réussite : 130, ignorée(s) : 0, total : 130
```

Build solution complète : `dotnet build` → **0 erreur**.

## Limite de cette preuve (à confirmer par le PO via l'écran Factures/Sélection)

Cette vérification prouve le **point de rupture exact** de TASK-106 : la lecture/validation
du cache reconnaît désormais l'espèce et ne renvoie plus un token `NULL`. La matérialisation
complète d'une ligne TVA valorisée (montants Sage/FGR réels) dépend ensuite du pipeline OM/FGR
existant (`OrchestrateurDeclaration`, hors périmètre de ce correctif et déjà couvert par les
tests TASK-024). Il est recommandé que le PO relance une sélection incluant ces 8 règlements
espèce sur l'écran ① pour confirmer visuellement l'apparition des lignes valorisées et
l'absence de « token MV NULL » dans `valorisation.log`.

## Critères de validation

- [x] Un règlement espèce fournisseur éligible ne reçoit plus un token `NULL` (8/8 confirmés).
- [x] La levée d'exigence `MV_Point` est bornée à `Espece` (non-espèce non rapproché toujours rejeté).
- [x] Aucune modification de `SelectionExpliqueeEvaluator` (hors périmètre respecté).
- [x] Build 0 erreur, suite de tests `Declaration.Orchestration.Tests` 130/130 verte.
- [ ] Confirmation visuelle PO sur l'écran ① (ligne valorisée réelle + `valorisation.log` propre) — à faire en aval.
