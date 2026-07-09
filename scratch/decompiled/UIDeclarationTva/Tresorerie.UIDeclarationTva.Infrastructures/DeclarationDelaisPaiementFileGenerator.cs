using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Erp.ICore;
using Tresorerie.UICommun.Helper;
using Tresorerie.UICommun.Helper.Views;

namespace Tresorerie.UIDeclarationTva.Infrastructures;

public class DeclarationDelaisPaiementFileGenerator
{
	private readonly IGroupeService _groupe;

	private readonly ModeViewHelper _modeHelper;

	private readonly FournisseurErpHelper _fournisseurHelper;

	private readonly IErpCommService _erpCommService;

	public DeclarationDelaisPaiementFileGenerator(IGroupeService groupe, ModeViewHelper modeHelper, FournisseurErpHelper fournisseurHelper, IErpCommService erpCommService)
	{
		_groupe = groupe ?? throw new ArgumentNullException("groupe");
		_modeHelper = modeHelper ?? throw new ArgumentNullException("modeHelper");
		_fournisseurHelper = fournisseurHelper ?? throw new ArgumentNullException("fournisseurHelper");
		_erpCommService = erpCommService ?? throw new ArgumentNullException("erpCommService");
	}

	public void Generate(int declarationNo, string path, int nbDecimal, IProgress<int> progress, CancellationToken cancellationToken)
	{
		SocieteManager societeManager = _groupe.SocieteManager;
		Societe societe = societeManager.Societe;
		DeclarationDelaisPaiementTiers declarationDelaisPaiementTiers = societeManager.DeclarationDelaisPaiementGet(declarationNo);
		if (declarationDelaisPaiementTiers == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationDelaisPaiementTiers.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration [" + declarationDelaisPaiementTiers.Numero + "] n'est pas clôturée.");
		}
		if (declarationDelaisPaiementTiers.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier de la déclaration [" + declarationDelaisPaiementTiers.Numero + "] est déjà généré.");
		}
		IEnumerable<LigneDeclarationDelaisPaiement> enumerable = societeManager.DeclarationDelaisPaiementLigneGetAll(declarationNo);
		if (!enumerable.Any())
		{
			throw new ApplicationException("La déclaration [" + declarationDelaisPaiementTiers.Numero + "] ne contient aucune ligne.");
		}
		IEnumerable<ModeView> all = _modeHelper.GetAll();
		IEnumerable<IErpFournisseur> all2 = _fournisseurHelper.GetAll(reload: true);
		IEnumerable<IErpTiersIce> allInfoFournisseur = _fournisseurHelper.GetAllInfoFournisseur(reload: true);
		_erpCommService.GetAllDesignationFacture(societe.DeclarationTvaEncaissementErpColumnNameDesignationDocument, societe.ErpColumnValueNatureMarchandise, societe.ErpColumnValueDateLivraisonMarchandise).ToList();
		NumberFormatInfo numberFormatInfo = new NumberFormatInfo();
		numberFormatInfo.NumberDecimalSeparator = ".";
		string text = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";
		text += $"\r\n<DeclarationDelaiPaiement>\r\n<identifiantFiscal>{societe.Identifiant}</identifiantFiscal>\r\n<annee>{declarationDelaisPaiementTiers.Exercice}</annee>\r\n<periode>{(int)((declarationDelaisPaiementTiers.TypeDeclaration == TypeDeclarationDelaisPaiement.Trimestrielle) ? declarationDelaisPaiementTiers.TrimestrePeriode : ((DeclarationTvaEncaissementTrimestrePeriode)5))}</periode>\r\n<activite>{(int)societe.ActiviteDelaiPaiementMarroc}</activite>";
		if (societe.ActiviteDelaiPaiementMarroc == ActiviteDelaiPaiementMarroc.EntrepriseEncourProcedure)
		{
			text += $"\r\n<dateJugementOuvrProc>{societe.DateJugement:yyyy-MM-dd}</dateJugementOuvrProc> \r\n";
		}
		text = text + "\r\n<chiffreAffaire>" + Math.Round(societe.ChiffreAffaire, nbDecimal).ToString(numberFormatInfo) + "</chiffreAffaire>\r\n<listeFacturesHorsDelai>";
		decimal num = default(decimal);
		foreach (LigneDeclarationDelaisPaiement ligne in enumerable)
		{
			cancellationToken.ThrowIfCancellationRequested();
			progress.Report((int)(num++ / (decimal)enumerable.Count() * 100m));
			ModeView modeView = all.SingleOrDefault((ModeView x) => x.No == ligne.ReglementModeNo);
			IErpFournisseur erpFournisseur = all2.SingleOrDefault((IErpFournisseur x) => x.No == ligne.TiersNo);
			if (erpFournisseur == null)
			{
				throw new ApplicationException("Impossible de charger le fournisseur [" + ligne.TiersCode + "]");
			}
			IErpTiersIce erpTiersIce = allInfoFournisseur.FirstOrDefault((IErpTiersIce x) => x.TiersCode == ligne.TiersCode);
			if (erpTiersIce == null)
			{
				throw new ApplicationException("Impossible de charger les informations fournisseur [" + ligne.TiersCode + "]");
			}
			string text2 = "";
			if (ligne.ReglementIsPointe && modeView != null)
			{
				switch (modeView.Type)
				{
				case ReglementType.Espece:
					text2 = "1";
					break;
				case ReglementType.Cheque:
					text2 = "2";
					break;
				case ReglementType.Traite:
					text2 = "5";
					break;
				case ReglementType.Virement:
					text2 = "4";
					break;
				}
			}
			decimal d = (ligne.ReglementIsPointe ? ligne.DocumentSolde : (ligne.DocumentSolde + (ligne.AffectationMontant.HasValue ? ligne.AffectationMontant.Value : 0m)));
			decimal d2 = ((!ligne.ReglementIsPointe) ? 0m : (ligne.AffectationMontant.HasValue ? ligne.AffectationMontant.Value : 0m));
			string text3 = ((!ligne.ReglementIsPointe) ? "" : (ligne.ReglementDatePoint.HasValue ? ligne.ReglementDatePoint.Value.ToString("yyyy-MM-dd") : ""));
			text += $"\r\n            <FactureHorsDelai>\r\n            <identifiantFiscal>{erpTiersIce.TiersIdentifiant}</identifiantFiscal>\r\n            <numRC>{erpTiersIce.TiersNumRegistreCommerceMarroc}</numRC>\r\n            <adresseSiegeSocial>{erpFournisseur.Adresse}</adresseSiegeSocial>\r\n            <numFacture>{ligne.DocumentNumero}</numFacture>\r\n            <dateEmission>{ligne.DocumentDate:yyyy-MM-dd}</dateEmission>\r\n            <natureMarchandise></natureMarchandise>\r\n            <dateLivraisonMarchandise>{ligne.DocumentDate:yyyy-MM-dd}</dateLivraisonMarchandise>\r\n            <dateConvenuePaiementFacture>{ligne.DocumentEcheanceLegale:yyyy-MM-dd}</dateConvenuePaiementFacture>\r\n            <montantFactureTtc>{Math.Round(ligne.DocumentMontant, nbDecimal).ToString(numberFormatInfo)}</montantFactureTtc>\r\n            <montantNonEncorePaye>{Math.Round(d, nbDecimal).ToString(numberFormatInfo)}</montantNonEncorePaye>\r\n            <montantPayeHorsDelai>{Math.Round(d2, nbDecimal).ToString(numberFormatInfo)}</montantPayeHorsDelai>\r\n            ";
			if (ligne.ReglementIsPointe)
			{
				text = text + "\r\n            <datePaiementHorsDelai>" + text3 + "</datePaiementHorsDelai>\r\n            <modePaiement>" + text2 + "</modePaiement>\r\n            <referencePaiement>" + (ligne.ReglementIsPointe ? ligne.ReglementPieceNumero : "") + "</referencePaiement>\r\n            ";
			}
			text += "\r\n            </FactureHorsDelai>\r\n            ";
		}
		text += "\r\n        </listeFacturesHorsDelai>\r\n        </DeclarationDelaiPaiement>\r\n        ";
		string arg = ((declarationDelaisPaiementTiers.TypeDeclaration == TypeDeclarationDelaisPaiement.Trimestrielle) ? $"T{(int)declarationDelaisPaiementTiers.TrimestrePeriode}" : "A");
		string text4 = $"{declarationDelaisPaiementTiers.Numero}-{declarationDelaisPaiementTiers.Exercice}-{arg}";
		string path2 = path + "\\" + text4 + ".xml";
		string path3 = path + "\\" + text4 + ".zip";
		if (File.Exists(path2))
		{
			throw new ApplicationException("Le fichier XML [" + text4 + "] existe déja.");
		}
		if (File.Exists(path3))
		{
			throw new ApplicationException("Le fichier ZIP [" + text4 + "] existe déja.");
		}
		File.WriteAllText(path2, text);
		using FileStream stream = new FileStream(path3, FileMode.Create);
		using ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Create);
		using Stream destination = zipArchive.CreateEntry(Path.GetFileName(path2)).Open();
		using FileStream fileStream = File.OpenRead(path2);
		fileStream.CopyTo(destination);
	}
}
