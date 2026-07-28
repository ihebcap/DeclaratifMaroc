# TASK-133 — DDP : génération du fichier XML/ZIP de dépôt

## Contexte
Décision PO §5.A-6 (confirmée 19/07/2026) : **l'algorithme actuel est correct et sert de base à la
réimplémentation** — reprendre la structure XML à l'identique, pas de refonte du format.

## Référence legacy (structure exacte retrouvée en code, à reproduire à l'identique)
`DeclarationDelaisPaiementFileGenerator.cs` (`Tresorerie.UIDeclarationTva/Infrastructures/`) :

```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<DeclarationDelaiPaiement>
  <identifiantFiscal>{société.Identifiant}</identifiantFiscal>
  <annee>{déclaration.Exercice}</annee>
  <periode>{1-4 si Trimestrielle, 5 si Annuelle}</periode>
  <activite>{société.ActiviteDelaiPaiementMarroc : 1=Normal, 2=EnProcedure}</activite>
  <!-- si EnProcedure uniquement : -->
  <dateJugementOuvrProc>{société.DateJugement:yyyy-MM-dd}</dateJugementOuvrProc>
  <chiffreAffaire>{société.ChiffreAffaire, arrondi n décimales devise société}</chiffreAffaire>
  <listeFacturesHorsDelai>
    <FactureHorsDelai>
      <identifiantFiscal>{IF du FOURNISSEUR — pas de la société, même nom de tag}</identifiantFiscal>
      <numRC>{n° registre de commerce fournisseur}</numRC>
      <adresseSiegeSocial>{adresse fournisseur}</adresseSiegeSocial>
      <numFacture>{n° document}</numFacture>
      <dateEmission>{date facture:yyyy-MM-dd}</dateEmission>
      <natureMarchandise></natureMarchandise>              <!-- toujours vide, cf. dette ci-dessous -->
      <dateLivraisonMarchandise>{date facture:yyyy-MM-dd}</dateLivraisonMarchandise>  <!-- cf. dette -->
      <dateConvenuePaiementFacture>{échéance légale:yyyy-MM-dd}</dateConvenuePaiementFacture>
      <montantFactureTtc>{TTC facture}</montantFactureTtc>
      <montantNonEncorePaye>{solde}</montantNonEncorePaye>
      <montantPayeHorsDelai>{montant affecté si payé dans la période, sinon 0}</montantPayeHorsDelai>
      <!-- uniquement si payée/pointée dans la période : -->
      <datePaiementHorsDelai>{date de pointage:yyyy-MM-dd}</datePaiementHorsDelai>
      <modePaiement>{1=Espèce, 2=Chèque, 4=Virement, 5=Traite}</modePaiement>
      <referencePaiement>{n° pièce règlement}</referencePaiement>
    </FactureHorsDelai>
    <!-- ... une entrée par ligne de déclaration ... -->
  </listeFacturesHorsDelai>
</DeclarationDelaiPaiement>
```
Nom de fichier : `{Numero}-{Exercice}-{T1..T4|A}.xml` + `.zip` (contenant uniquement le XML). Générer dans
un répertoire cible, échec explicite si le fichier existe déjà (ne jamais écraser silencieusement).

**Point non documenté au CDC** : le XML **ne porte aucun tag ICE fournisseur** — seuls `identifiantFiscal`
et `numRC` sortent au niveau ligne. Le contrôle bloquant IF/ICE (TASK-132) doit néanmoins valider les deux
(l'ICE ne sort pas dans le fichier mais reste un pré-requis réglementaire de conformité du tiers).

## Dette assumée, non bloquante (décision PO §5.A-4, confirmée)
- `<natureMarchandise>` toujours vide dans le legacy alors qu'un mapping existe (`SO_ColValueNatureMarchandise`,
  §7.1) — à corriger progressivement, **non bloquant pour ce premier livrable**.
- `<dateLivraisonMarchandise>` = date de la facture (pas une vraie date de livraison) alors qu'un mapping
  existe (`SO_ColValueDateLivraisonMarchandise`, §7.1) — même statut, non bloquant.
- Si le temps le permet dans cette tâche, brancher ces deux mappings (déjà lus par `erpDesignationDocument`
  dans le legacy, jamais exploités dans la boucle de génération, `GetAllDesignationFacture` déjà appelé
  l.53-54 du generator) serait un gain rapide — à évaluer avec l'architecte sans bloquer la livraison si
  ça complexifie le premier jet.

## Objectif
```
Entrée  : déclaration clôturée + contrôle IF/ICE validé (TASK-132)
Traitement : générer XML (structure ci-dessus) + ZIP, nommage {Numero}-{Exercice}-{periode}
Sortie  : fichier .xml + .zip, DDP_IsGeneretedFile = true
```

## Périmètre STRICT
- **Inclus** : génération XML/ZIP à l'identique de la structure ci-dessus, garde "fichier déjà existant",
  garde "contrôle IF/ICE validé" (dépend de TASK-132), annulation de génération tant que non déposé.
- **Exclu** : contrôle IF/ICE lui-même (TASK-132), dépôt (flag géré en TASK-132), UI (TASK-134).

## Étapes
1. Générateur XML reproduisant la structure exacte ci-dessus (tags, ordre, codes `modePaiement`).
2. Compression ZIP (le XML seul dans l'archive, comme le legacy).
3. Garde "fichier déjà existant" (ne jamais écraser).
4. Brancher le contrôle IF/ICE (TASK-132) **avant** toute écriture de fichier (valider 100 % des lignes
   d'abord, générer ensuite — contrairement au legacy qui échoue en cours de boucle).
5. Annulation de génération (`FichierAnnulerGeneration`) tant que non déposé.
6. Tests : structure XML exacte (snapshot/comparaison champ par champ), codes `modePaiement`, cas
   "EnProcedure" (tag `dateJugementOuvrProc` présent), cas normal (tag absent), garde fichier existant.

## Livrables
- Générateur XML/ZIP.
- Tests (structure exacte, codes de paiement, cas EnProcedure).

## Critères de validation
- Fichier XML structurellement identique au format legacy (comparaison champ par champ sur un jeu de
  données réel si disponible).
- Aucune génération possible sans contrôle IF/ICE 100 % validé au préalable.
- Build 0 erreur, tests verts.

## Risques / dépendances
- Dépend de TASK-132 (contrôle IF/ICE, cycle de vie).
- **Bloquant pour** TASK-134 (le front déclenche la génération).
