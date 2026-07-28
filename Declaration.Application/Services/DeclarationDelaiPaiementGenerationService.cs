using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Core;
using Declaration.Core.Model;

namespace Declaration.Application.Services;

/// <summary>
/// TASK-133 — DDP : génération du fichier XML/ZIP de dépôt Délai de Paiement Maroc. Orchestration
/// PURE lecture puis écriture (aucune logique métier ici — le calcul par ligne est délégué à
/// <see cref="DeclarationDelaiPaiementLigneCalculator"/>, la sérialisation à
/// <see cref="Declaration.Export.Xml.DeclarationDelaiPaiementXmlExporter"/>) : ce service ne fait que
/// construire le modèle depuis les lectures et enchaîner les 2 points d'entrée déjà exposés par
/// TASK-132 (<see cref="IDeclarationDelaiPaiementService.VerifierGenerationFichierAutoriseeAsync"/> /
/// <see cref="IDeclarationDelaiPaiementService.MarquerFichierGenereAsync"/>).
///
/// Périmètre STRICT (cf. TASK-133) : génération XML/ZIP, garde « fichier déjà existant » (déléguée à
/// l'exporter), garde « contrôle IF/ICE validé » (déléguée à TASK-132, appelée EN PREMIER, avant le
/// premier octet écrit), annulation de génération. AUCUN endpoint/UI ici (TASK-134).
/// </summary>
public interface IDeclarationDelaiPaiementGenerationService
{
    /// <summary>
    /// Génère XML + ZIP puis pose <c>DDP_IsGeneretedFile</c>. Lève avant tout écriture si le contrôle
    /// IF/ICE ou la garde d'état échoue (message utilisateur déjà prêt, cf. TASK-132). Retourne le
    /// chemin du ZIP produit.
    /// </summary>
    Task<string> GenererFichierAsync(int ddpId, int utilisateurId);

    /// <summary>
    /// Annule la génération (legacy <c>FichierAnnulerGeneration</c>) : remet <c>DDP_IsGeneretedFile</c>
    /// à faux PUIS supprime les fichiers physiques déjà écrits — amélioration assumée par rapport au
    /// legacy (cf. VERIFY TASK-132 §10.4 : le legacy laissait les fichiers orphelins sur disque, ce qui
    /// bloquait toute régénération ultérieure sur la garde « fichier déjà existant »). Idempotent :
    /// aucune erreur si les fichiers sont déjà absents.
    /// </summary>
    Task AnnulerGenerationFichierAsync(int ddpId, int utilisateurId);

    /// <summary>
    /// Chemins déterministes (générés ou non — l'appelant teste leur existence), pour TASK-134.
    /// Lecture seule, aucun effet de bord.
    /// </summary>
    Task<(string XmlPath, string ZipPath)> ObtenirCheminsFichiersAsync(int ddpId);
}

/// <inheritdoc cref="IDeclarationDelaiPaiementGenerationService"/>
public sealed class DeclarationDelaiPaiementGenerationService : IDeclarationDelaiPaiementGenerationService
{
    private readonly IDeclarationDelaiPaiementService _declarationService;
    private readonly IDeclarationDelaiPaiementRepository _repository;
    private readonly string _dossierSortie;

    /// <param name="dossierSortieOverride">
    /// RÉSERVÉ AUX TESTS (jamais renseigné par la DI de production) : par défaut
    /// <c>null</c> ⇒ utilise <see cref="DeclarationWorkflowService.ObtenirDossierExports"/>, LE MÊME
    /// dossier déterministe que le module TVA (TASK-155) — convention explicitement réutilisée plutôt
    /// que réinventée. Un override permet aux tests d'orchestration d'isoler leurs fichiers dans un
    /// répertoire temporaire jetable, sans jamais écrire sous <c>AppContext.BaseDirectory</c>.
    /// </param>
    public DeclarationDelaiPaiementGenerationService(
        IDeclarationDelaiPaiementService declarationService,
        IDeclarationDelaiPaiementRepository repository,
        string? dossierSortieOverride = null)
    {
        _declarationService = declarationService ?? throw new ArgumentNullException(nameof(declarationService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _dossierSortie = dossierSortieOverride ?? DeclarationWorkflowService.ObtenirDossierExports();
    }

    public async Task<string> GenererFichierAsync(int ddpId, int utilisateurId)
    {
        // 1) Garde d'état + contrôle IF/ICE COMPLET, AVANT le premier octet écrit (mandat explicite
        //    TASK-132 §10.1). Lève InvalidOperationException avec le message utilisateur déjà prêt.
        await _declarationService.VerifierGenerationFichierAutoriseeAsync(ddpId);

        // 2) Construction du modèle — LECTURES UNIQUEMENT.
        var modele = await ConstruireModeleAsync(ddpId);

        // 3) Écriture (l'exporter porte sa propre garde « fichier déjà existant »).
        var dossierSortie = _dossierSortie;
        Directory.CreateDirectory(dossierSortie);

        var exporter = new Declaration.Export.Xml.DeclarationDelaiPaiementXmlExporter();
        var zipPath = exporter.GenererXml(modele, dossierSortie);

        // 4) Marquage — re-valide intégralement (défense en profondeur, TASK-132). Si cette
        //    re-validation échoue (donnée modifiée entre les étapes 1 et 4), les fichiers viennent
        //    d'être écrits avec succès mais le flag ne peut pas être posé : on les supprime plutôt que
        //    de laisser un orphelin bloquer toute tentative suivante (même anomalie que celle
        //    documentée en annulation, cf. AnnulerGenerationFichierAsync).
        try
        {
            await _declarationService.MarquerFichierGenereAsync(ddpId, utilisateurId);
        }
        catch
        {
            SupprimerFichiersSiPresents(modele.EnTete, dossierSortie);
            throw;
        }

        return zipPath;
    }

    public async Task AnnulerGenerationFichierAsync(int ddpId, int utilisateurId)
    {
        var entete = await _declarationService.GetAsync(ddpId)
            ?? throw new InvalidOperationException("Impossible de charger la déclaration.");

        // Garde legacy (fichier généré → déposée → clôturée → au moins une ligne), TASK-132.
        await _declarationService.AnnulerGenerationFichierAsync(ddpId, utilisateurId);

        // Amélioration assumée (cf. VERIFY TASK-132 §10.4) : supprime les fichiers physiques pour ne
        // jamais bloquer une régénération ultérieure sur la garde « fichier déjà existant ».
        var enTeteXml = ConstruireEnTeteXmlPourNommage(entete);
        SupprimerFichiersSiPresents(enTeteXml, _dossierSortie);
    }

    public async Task<(string XmlPath, string ZipPath)> ObtenirCheminsFichiersAsync(int ddpId)
    {
        var entete = await _declarationService.GetAsync(ddpId)
            ?? throw new InvalidOperationException("Impossible de charger la déclaration.");

        var enTeteXml = ConstruireEnTeteXmlPourNommage(entete);
        var (_, xmlPath, zipPath) = Declaration.Export.Xml.DeclarationDelaiPaiementXmlExporter.CalculerChemins(
            enTeteXml, _dossierSortie);

        return (xmlPath, zipPath);
    }

    // ─── Construction du modèle ───────────────────────────────────────────────────────────────────

    private async Task<DeclarationDelaiPaiementXmlModele> ConstruireModeleAsync(int ddpId)
    {
        var entete = await _declarationService.GetAsync(ddpId)
            ?? throw new InvalidOperationException("Impossible de charger la déclaration.");

        var societe = await _repository.GetSocieteInfoAsync(entete.SocieteId);

        // Identifiant fiscal de la SOCIÉTÉ (racine du fichier) : jamais un placeholder silencieux
        // (même principe que TASK-151/TASK-155 pour l'export TVA).
        if (string.IsNullOrWhiteSpace(societe.IdentifiantFiscal))
            throw new InvalidOperationException(
                $"Identifiant fiscal de la société (SO_Id={entete.SocieteId}) absent ou vide dans " +
                "P_SOCIETE.SO_Identifiant — génération du fichier Délai de Paiement impossible.");

        var lignes = await _declarationService.GetLignesAsync(ddpId);

        var codesTiers = lignes
            .Select(l => (l.TiersCode ?? string.Empty).Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var identites = await _repository.GetIdentitesFiscalesTiersAsync(entete.SocieteId, codesTiers);

        var modeIds = lignes
            .Where(l => l.ReglementModeId.HasValue)
            .Select(l => l.ReglementModeId!.Value)
            .Distinct()
            .ToList();
        var typesMode = await _repository.GetTypesModeReglementAsync(modeIds);

        // TASK-191 : nature marchandise / date livraison marchandise réelles (F_DOCENTETE), tolérant à
        // l'absence de configuration société (dictionnaire vide, aucun aller-retour Sage superflu).
        var numerosFacture = lignes
            .Select(l => (l.DoNumero ?? string.Empty).Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var valeursMarchandise = await _repository.GetValeursMarchandiseAsync(entete.SocieteId, numerosFacture);

        var enTeteXml = ConstruireEnTeteXml(entete, societe);

        var factures = lignes.Select(ligne => ConstruireFactureXml(entete, ligne, identites, typesMode, valeursMarchandise)).ToList();

        return new DeclarationDelaiPaiementXmlModele
        {
            EnTete = enTeteXml,
            Factures = factures
        };
    }

    private static EnTeteDeclarationDelaiPaiementXml ConstruireEnTeteXml(
        DeclarationDelaiPaiement entete, SocieteDelaiPaiementInfo societe) => new()
    {
        Numero = entete.Numero,
        Exercice = entete.Exercice,
        PeriodeXml = DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodeXml(entete.Type, entete.Trimestre),
        PeriodeFichier = DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodeFichier(entete.Type, entete.Trimestre),
        IdentifiantFiscalSociete = societe.IdentifiantFiscal ?? string.Empty,
        ActiviteMarrocCode = societe.ActiviteMarrocCode,
        DateJugement = societe.DateJugement,
        ChiffreAffaire = societe.ChiffreAffaire
    };

    /// <summary>
    /// Sous-ensemble de <see cref="ConstruireEnTeteXml"/> suffisant pour calculer le NOM DE FICHIER
    /// (<c>Numero</c>/<c>Exercice</c>/<c>PeriodeFichier</c>) sans lire <c>P_SOCIETE</c> — utilisé par
    /// l'annulation et la lecture de chemins, qui n'ont besoin d'aucun autre champ société.
    /// </summary>
    private static EnTeteDeclarationDelaiPaiementXml ConstruireEnTeteXmlPourNommage(DeclarationDelaiPaiement entete) => new()
    {
        Numero = entete.Numero,
        Exercice = entete.Exercice,
        PeriodeFichier = DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodeFichier(entete.Type, entete.Trimestre)
    };

    private static FactureHorsDelaiXml ConstruireFactureXml(
        DeclarationDelaiPaiement entete,
        LigneDeclarationDelaiPaiement ligne,
        IReadOnlyDictionary<string, IdentiteFiscaleTiersErp> identites,
        IReadOnlyDictionary<int, TypeModeReglementDelaiPaiement> typesMode,
        IReadOnlyDictionary<string, ValeursMarchandiseErp> valeursMarchandise)
    {
        var code = (ligne.TiersCode ?? string.Empty).Trim();

        // Défense en profondeur : le contrôle IF/ICE (étape 1 de GenererFichierAsync) a déjà validé
        // 100 % des fournisseurs — un tiers introuvable ICI indiquerait une donnée modifiée entre les
        // deux lectures. Jamais un enregistrement silencieux avec des champs vides.
        if (code.Length == 0 || !identites.TryGetValue(code, out var identite))
            throw new InvalidOperationException(
                $"Fournisseur [{ligne.TiersCode}] introuvable dans le référentiel tiers ERP — génération " +
                "annulée (donnée modifiée depuis le contrôle IF/ICE).");

        TypeModeReglementDelaiPaiement? typeMode = ligne.ReglementModeId.HasValue
            && typesMode.TryGetValue(ligne.ReglementModeId.Value, out var t)
                ? t
                : null;

        // TASK-191 : absent du dictionnaire (config société absente OU document Sage introuvable) ⇒
        // ValeursMarchandiseErp? reste null, le calculateur applique alors le même repli qu'avant.
        var numFacture = ligne.DoNumero?.Trim();
        ValeursMarchandiseErp? valeurs = !string.IsNullOrEmpty(numFacture)
            && valeursMarchandise.TryGetValue(numFacture, out var v)
                ? v
                : null;

        return DeclarationDelaiPaiementLigneCalculator.Calculer(
            dateFinPeriode: entete.DateFin,
            identifiantFiscalFournisseur: identite.IdentifiantFiscal,
            numRc: identite.NumRc,
            adresseSiegeSocial: identite.Adresse,
            numFacture: numFacture,
            dateEmission: ligne.DoDate,
            dateConvenuePaiementFacture: ligne.EcheanceLegale,
            montantFactureTtc: ligne.MontantEcheance,
            soldeEcheance: ligne.SoldeEcheance,
            montantAffecte: ligne.MontantAffecte,
            reglementRapproche: ligne.ReglementRapproche,
            dateRapprochement: ligne.ReglementDateRapprochement,
            referencePaiement: ligne.ReglementPiece?.Trim(),
            typeModeReglement: typeMode,
            natureMarchandiseReelle: valeurs?.NatureMarchandise,
            dateLivraisonMarchandiseReelle: valeurs?.DateLivraisonMarchandise);
    }

    private static void SupprimerFichiersSiPresents(EnTeteDeclarationDelaiPaiementXml enTeteXml, string dossierSortie)
    {
        var (_, xmlPath, zipPath) = Declaration.Export.Xml.DeclarationDelaiPaiementXmlExporter.CalculerChemins(enTeteXml, dossierSortie);
        if (File.Exists(xmlPath)) File.Delete(xmlPath);
        if (File.Exists(zipPath)) File.Delete(zipPath);
    }
}
