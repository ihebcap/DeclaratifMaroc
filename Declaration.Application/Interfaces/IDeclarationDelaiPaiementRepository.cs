using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Core;

namespace Declaration.Application.Interfaces;

/// <summary>
/// TASK-132 : contrat de persistance du cycle de vie de la déclaration Délai de Paiement Maroc.
///
/// Tables EXISTANTES, propriété GRF, réutilisées TELLES QUELLES —
/// <c>RT_DECLARATIONDELAISPAIEMENT</c> / <c>RT_DECLARATIONDELAISPAIEMENTLG</c> (créées par les
/// migrations legacy <c>202501311347278</c> / <c>202502031742099</c>) : <b>aucune table créée, aucune
/// colonne ajoutée, aucun index/contrainte posé</b> (contrainte non négociable — tables possédées par
/// <c>apbs-gr_winform</c>). Les lectures de paramétrage (<c>P_SOCIETE</c>) et de référentiel tiers
/// (<c>F_COMPTET</c>, base Sage) sont en LECTURE SEULE STRICTE.
///
/// Écritures autorisées, et UNIQUEMENT celles-ci :
/// <list type="bullet">
/// <item><c>INSERT RT_DECLARATIONDELAISPAIEMENT</c> (création d'entête) ;</item>
/// <item><c>UPDATE RT_DECLARATIONDELAISPAIEMENT</c> restreint aux 6 colonnes mutables du legacy
/// (<c>DDP_Statut</c>, <c>UT_IdModif</c>, <c>DDP_DateModif</c>, <c>DDP_IsDepose</c>,
/// <c>DDP_Libelle</c>, <c>DDP_IsGeneretedFile</c>) ;</item>
/// <item><c>DELETE RT_DECLARATIONDELAISPAIEMENT</c> (uniquement après garde EnCours + 0 ligne) ;</item>
/// <item><c>INSERT</c>/<c>DELETE RT_DECLARATIONDELAISPAIEMENTLG</c> (lignes intégrées).</item>
/// </list>
/// </summary>
public interface IDeclarationDelaiPaiementRepository
{
    // ─── Entête ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// INSERT de l'entête (17 colonnes hors identity/rowversion) dans une transaction, retourne
    /// <c>DDP_Id</c>. Le numéro est fourni par le service (déjà résolu + contrôlé unique).
    /// </summary>
    Task<int> CreerEnteteAsync(DeclarationDelaiPaiement entete);

    Task<DeclarationDelaiPaiement?> GetEnteteAsync(int ddpId);

    Task<IReadOnlyList<DeclarationDelaiPaiementListItem>> GetAllAsync(int soId);

    /// <summary>Déclarations d'un exercice (base du contrôle d'unicité de période, legacy l.657-660).</summary>
    Task<IReadOnlyList<DeclarationDelaiPaiement>> GetAllParExerciceAsync(int soId, int exercice);

    /// <summary>Contrôle d'unicité du numéro pour la société (legacy l.610).</summary>
    Task<bool> ExisteNumeroAsync(int soId, string numero);

    /// <summary>
    /// <c>MAX(DDP_Numero)</c> des numéros de la société correspondant au motif <c>LIKE</c> de
    /// numérotation (legacy <c>SocieteRepository.Script.cs:155-160</c>). <c>null</c> si aucun.
    /// </summary>
    Task<string?> GetDernierNumeroAsync(int soId, string patternLike);

    /// <summary>
    /// Paramétrage de numérotation de la société, LECTURE SEULE sur <c>P_SOCIETE</c>
    /// (<c>SO_DecDPPrefix</c>/<c>SO_DecDPNumAnnee</c>/<c>SO_DecDPNumMois</c>/<c>SO_DecDPNumCount</c>).
    /// Échec explicite si la société est introuvable.
    /// </summary>
    Task<ConfigurationNumerotationDelaiPaiement> GetConfigurationNumerotationAsync(int soId);

    /// <summary>
    /// UPDATE restreint aux 6 colonnes mutables du legacy. Aucune autre colonne n'est atteignable par
    /// ce contrat (les bornes de période, l'exercice, le type et le numéro sont immuables).
    /// </summary>
    Task MettreAJourEtatAsync(
        int ddpId,
        StatutDeclarationDelaiPaiement statut,
        bool estDeposee,
        bool fichierGenere,
        string? libelle,
        int modificateurId,
        DateTime dateModification);

    /// <summary>DELETE de l'entête — appelé UNIQUEMENT après la garde EnCours + 0 ligne.</summary>
    Task SupprimerEnteteAsync(int ddpId);

    // ─── Lignes ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Nombre de lignes intégrées (équivalent legacy <c>HasLignes</c>).</summary>
    Task<int> CompterLignesAsync(int ddpId);

    /// <summary>Clés (<c>EC_Id</c>, <c>AF_Id</c>) déjà intégrées dans CETTE déclaration (garde anti-double-intégration).</summary>
    Task<IReadOnlyList<CleLigneDelaiPaiement>> GetClesLignesAsync(int ddpId);

    /// <summary>
    /// INSERT en LOT des lignes dans une TRANSACTION unique (ARCHITECTURE §5) — corrige le legacy
    /// <c>LigneControleDelaisPaiementController.IntergerLigne</c> qui bouclait sans transaction
    /// englobante et pouvait donc laisser une intégration PARTIELLE derrière lui. Retourne le nombre
    /// de lignes insérées.
    /// </summary>
    Task<int> AjouterLignesAsync(int ddpId, IReadOnlyCollection<LigneAIntegrerDelaiPaiement> lignes);

    /// <summary>Lignes intégrées relues avec échéance/règlement (jointures identiques au legacy).</summary>
    Task<IReadOnlyList<LigneDeclarationDelaiPaiement>> GetLignesAsync(int ddpId);

    /// <summary>DELETE d'UNE ligne intégrée (legacy <c>DeclarationDelaisPaiementDeleteLigne</c>), scopé sur la déclaration.</summary>
    Task<int> SupprimerLigneAsync(int ddpId, int ddplId);

    // ─── Référentiel tiers ERP (LECTURE SEULE, base Sage) ─────────────────────────────────────────

    /// <summary>
    /// Identités fiscales (IF/ICE) des codes tiers fournis, lues sur <c>F_COMPTET</c> via la connexion
    /// SAGE résolue par <c>SO_Id</c> (TASK-118) et les noms de colonnes configurés par société
    /// (TASK-048/154). Indexé par code tiers ; un code ABSENT du dictionnaire = fournisseur
    /// introuvable dans le référentiel (motif bloquant distinct d'un IF/ICE vide).
    /// </summary>
    Task<IReadOnlyDictionary<string, IdentiteFiscaleTiersErp>> GetIdentitesFiscalesTiersAsync(int soId, IReadOnlyCollection<string> tiersCodes);

    /// <summary>
    /// TASK-191 : valeurs réelles « nature marchandise »/« date livraison marchandise » des documents
    /// facture fournisseur dont le numéro figure dans <paramref name="numerosFacture"/>, lues sur
    /// <c>F_DOCENTETE</c> (Sage, lecture seule) via les 2 colonnes configurables par société
    /// (<c>P_SOCIETE.SO_ColValueNatureMarchandise</c>/<c>SO_ColValueDateLivraisonMarchandise</c>),
    /// même contrat de tolérance que <see cref="GetIdentitesFiscalesTiersAsync"/> : AUCUNE des deux
    /// colonnes configurée ⇒ dictionnaire vide, AUCUN aller-retour Sage (jamais une erreur). Indexé
    /// par numéro de facture (<c>DO_Numero</c>/<c>DO_Piece</c>) ; un numéro ABSENT du dictionnaire
    /// signifie que le document Sage correspondant est introuvable (l'appelant applique alors le
    /// même repli que si la valeur était vide).
    /// </summary>
    Task<IReadOnlyDictionary<string, ValeursMarchandiseErp>> GetValeursMarchandiseAsync(int soId, IReadOnlyCollection<string> numerosFacture);

    // ─── Société (LECTURE SEULE, base GRF) — TASK-133 ─────────────────────────────────────────────

    /// <summary>
    /// Champs société nécessaires à l'en-tête du fichier XML de dépôt (<see cref="SocieteDelaiPaiementInfo"/>),
    /// LECTURE SEULE sur <c>P_SOCIETE</c>. Échec explicite si la société est introuvable — jamais un
    /// en-tête incomplet silencieux.
    /// </summary>
    Task<SocieteDelaiPaiementInfo> GetSocieteInfoAsync(int soId);

    // ─── Référentiel mode de règlement (LECTURE SEULE, base GRF) — TASK-133 ───────────────────────

    /// <summary>
    /// <c>P_MODEREGLEMENT.MR_TypeNo</c> (table EXISTANTE, propriété GRF, réutilisée telle quelle —
    /// aucune modification de schéma) indexé par <c>MR_Id</c>, pour convertir le mode de règlement
    /// d'une ligne (<c>LigneDeclarationDelaiPaiement.ReglementModeId</c>) en code <c>modePaiement</c>
    /// DDP (<see cref="Declaration.Core.Model.DeclarationDelaiPaiementLigneCalculator"/>). Un
    /// <c>MR_Id</c> ABSENT du dictionnaire = mode introuvable (legacy : <c>modeLigne == null</c>,
    /// <c>modePaiement</c> reste vide, jamais bloquant).
    /// </summary>
    Task<IReadOnlyDictionary<int, Declaration.Core.Model.TypeModeReglementDelaiPaiement>> GetTypesModeReglementAsync(
        IReadOnlyCollection<int> modeIds);
}
