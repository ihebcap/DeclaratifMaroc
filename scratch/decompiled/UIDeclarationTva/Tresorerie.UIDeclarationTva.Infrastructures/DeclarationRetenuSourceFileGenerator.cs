using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using SageBO.Core.Models;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Erp.ICore;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.UICommun.Helper;
using Tresorerie.UICommun.Helper.Views;

namespace Tresorerie.UIDeclarationTva.Infrastructures;

public class DeclarationRetenuSourceFileGenerator
{
	private readonly IGroupeService _groupe;

	private readonly FournisseurErpHelper _fournisseurErpHelper;

	private readonly DeviseViewHelper _deviseHelper;

	private readonly IErpCommService _erpCommService;

	public DeclarationRetenuSourceFileGenerator(IGroupeService groupe, FournisseurErpHelper fournisseurErpHelper, DeviseViewHelper deviseHelper, IErpCommService erpCommService)
	{
		_groupe = groupe ?? throw new ArgumentNullException("groupe");
		_fournisseurErpHelper = fournisseurErpHelper;
		_deviseHelper = deviseHelper;
		_erpCommService = erpCommService;
	}

	public void Generate(int declarationNo, string path, int nbDecimal, IProgress<int> progress, CancellationToken cancellationToken)
	{
		if (_groupe.SocieteManager.Societe.LegislationType == Legislation.Maroc)
		{
			GenerateMarroc(declarationNo, path, nbDecimal, progress, cancellationToken);
		}
		else
		{
			GenerateTunisie(declarationNo, path, nbDecimal, progress, cancellationToken);
		}
	}

	private void GenerateTunisie(int declarationNo, string path, int nbDecimal, IProgress<int> progress, CancellationToken cancellationToken)
	{
		SocieteManager societeManager = _groupe.SocieteManager;
		Societe societe = societeManager.Societe;
		DeclarationRetenuSource declarationRetenuSource = societeManager.DeclarationRetenuSourceGet(declarationNo);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration [" + declarationRetenuSource.Numero + "] n'est pas clôturée.");
		}
		if (declarationRetenuSource.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier de la déclaration [" + declarationRetenuSource.Numero + "] est déjà généré.");
		}
		if (!societeManager.DeclarationRetenuSourceHasLignes(declarationNo))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		List<RetenuALaSource> list = societeManager.RetenueSourceTejGetAllByDeclaration(declarationNo).ToList();
		if (!list.Any())
		{
			throw new ApplicationException("Aucune ligne pour la déclaration.");
		}
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		List<OperationRetenuSource> all = _groupe.OperationRetenuSourceManager.GetAll();
		if (string.IsNullOrEmpty(societe.Identifiant))
		{
			throw new ApplicationException("Veuillez vérifier l'identifiant de la société.");
		}
		try
		{
			string text = ((societe.Identifiant.Length > 8) ? societe.Identifiant.Substring(0, 8) : societe.Identifiant);
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
			stringBuilder.AppendLine("<DeclarationsRS VersionSchema=\"1.0\">");
			stringBuilder.AppendLine("<Declarant>");
			stringBuilder.AppendLine("<TypeIdentifiant>1</TypeIdentifiant>");
			stringBuilder.AppendLine("<Identifiant>" + text + "</Identifiant>");
			stringBuilder.AppendLine("<CategorieContribuable>PM</CategorieContribuable>");
			stringBuilder.AppendLine("</Declarant>");
			stringBuilder.AppendLine("<ReferenceDeclaration>");
			stringBuilder.AppendLine("<ActeDepot>" + ((declarationRetenuSource.Nature == NatureDeclaration.Initiale) ? "0" : "1") + "</ActeDepot>");
			stringBuilder.AppendLine($"<AnneeDepot>{declarationRetenuSource.Exercice}</AnneeDepot>");
			stringBuilder.AppendLine("<MoisDepot>" + new DateTime(1900, (int)declarationRetenuSource.MoisPeriode, 1).ToString("MM") + "</MoisDepot>");
			stringBuilder.AppendLine("</ReferenceDeclaration>");
			stringBuilder.AppendLine("<AjouterCertificats>");
			decimal num = default(decimal);
			foreach (RetenuALaSource ligne in list)
			{
				cancellationToken.ThrowIfCancellationRequested();
				progress.Report((int)(num++ / (decimal)list.Count * 100m));
				if (!ligne.OperationRetenueNo.HasValue)
				{
					throw new ApplicationException("Code opération invalide. RS: [" + ligne.Numero + "]");
				}
				IErpFournisseur erpFournisseur = _fournisseurErpHelper.Get(ligne.FournisseurNo);
				if (erpFournisseur == null)
				{
					throw new ApplicationException("Impossible de charger le fournisseur [" + ligne.FournisseurCode + "]");
				}
				IErpTiersIce infoFournisseur = _fournisseurErpHelper.GetInfoFournisseur(ligne.FournisseurNo);
				if (infoFournisseur == null)
				{
					throw new ApplicationException("Impossible de charger les informations complementaire du fournisseur [" + ligne.FournisseurCode + "]");
				}
				OperationRetenuSource operationRetenuSource = all.FirstOrDefault((OperationRetenuSource x) => x.No == ligne.OperationRetenueNo);
				if (operationRetenuSource == null)
				{
					throw new ApplicationException("Impossible de déterminer l'opération de retenue [" + ligne.Numero + "].");
				}
				if (string.IsNullOrEmpty(infoFournisseur.TiersIdentifiant))
				{
					throw new ApplicationException("L'identifiant du fournisseur [" + infoFournisseur.TiersCode + "] est obligatoire.");
				}
				if (string.IsNullOrEmpty(infoFournisseur.TiersActivite))
				{
					throw new ApplicationException("L'activité du fournisseur [" + infoFournisseur.TiersCode + "] est obligatoire.");
				}
				if (string.IsNullOrEmpty(infoFournisseur.TiersEmail))
				{
					throw new ApplicationException("L'email du fournisseur [" + infoFournisseur.TiersCode + "] est obligatoire.");
				}
				if (string.IsNullOrEmpty(erpFournisseur.Telephone))
				{
					throw new ApplicationException("Le téléphone du fournisseur [" + infoFournisseur.TiersCode + "] est obligatoire.");
				}
				if (string.IsNullOrEmpty(erpFournisseur.Adresse))
				{
					throw new ApplicationException("L'adresse du fournisseur [" + infoFournisseur.TiersCode + "] est obligatoire.");
				}
				string text2 = ((infoFournisseur.TiersIdentifiant.Length > 8) ? infoFournisseur.TiersIdentifiant.Substring(0, 8) : infoFournisseur.TiersIdentifiant);
				NatureFournisseur natureFournisseur = _fournisseurErpHelper.GetNatureFournisseur(ligne.FournisseurCode);
				DeviseView deviseView = _deviseHelper.Get(ligne.DeviseNo);
				decimal num2 = ((defaultDeviseSociete.No == ligne.DeviseNo) ? 1m : Math.Round(1m / ligne.Cours, 5));
				if (natureFournisseur == NatureFournisseur.PersonnePhysique && infoFournisseur.TiersDateNaissance.IsNotLogique())
				{
					throw new ApplicationException("La date de naissance du fournisseur [" + infoFournisseur.TiersCode + "] est invalide.");
				}
				stringBuilder.AppendLine("<Certificat>");
				stringBuilder.AppendLine("<Beneficiaire>");
				stringBuilder.AppendLine("<IdTaxpayer>");
				switch (natureFournisseur)
				{
				case NatureFournisseur.PersonneMorale:
					stringBuilder.AppendLine("<MatriculeFiscal>");
					stringBuilder.AppendLine("<TypeIdentifiant>1</TypeIdentifiant>");
					stringBuilder.AppendLine("<Identifiant>" + text2 + "</Identifiant>");
					stringBuilder.AppendLine("<CategorieContribuable>PM</CategorieContribuable>");
					stringBuilder.AppendLine("</MatriculeFiscal>");
					break;
				case NatureFournisseur.PersonnePhysique:
					stringBuilder.AppendLine("<CIN>");
					stringBuilder.AppendLine("<TypeIdentifiant>2</TypeIdentifiant>");
					stringBuilder.AppendLine("<Identifiant>" + text2 + "</Identifiant>");
					stringBuilder.AppendLine("<DateNaissance>" + $"{infoFournisseur.TiersDateNaissance:MM/dd/yyyy}" + "</DateNaissance>");
					stringBuilder.AppendLine("<CategorieContribuable>PP</CategorieContribuable>");
					stringBuilder.AppendLine("</CIN>");
					break;
				}
				stringBuilder.AppendLine("</IdTaxpayer>");
				stringBuilder.AppendLine("<Resident>1</Resident>");
				stringBuilder.AppendLine("<NometprenonOuRaisonsociale>" + erpFournisseur.Intitule + "</NometprenonOuRaisonsociale>");
				stringBuilder.AppendLine("<Adresse>" + erpFournisseur.Adresse + "</Adresse>");
				stringBuilder.AppendLine("<Activite>" + infoFournisseur.TiersActivite + "</Activite>");
				stringBuilder.AppendLine("<InfosContact>");
				stringBuilder.AppendLine("<AdresseMail>" + infoFournisseur.TiersEmail + "</AdresseMail>");
				stringBuilder.AppendLine("<NumTel>" + erpFournisseur.Telephone + "</NumTel>");
				stringBuilder.AppendLine("</InfosContact>");
				stringBuilder.AppendLine("</Beneficiaire>");
				stringBuilder.AppendLine("<DatePayement>" + ligne.Date.ToString("dd/MM/yyyy") + "</DatePayement>");
				stringBuilder.AppendLine("<Ref_certif_chez_declarant>" + ligne.Numero + "</Ref_certif_chez_declarant>");
				stringBuilder.AppendLine("<ListeOperations>");
				stringBuilder.AppendLine("<Operation IdTypeOperation=\"" + operationRetenuSource.Code + "\">");
				stringBuilder.AppendLine($"<AnneeFacturation>{ligne.AnneeFacturation}</AnneeFacturation>");
				stringBuilder.AppendLine("<CNPC>" + (ligne.IsConvention ? "1" : "0") + "</CNPC>");
				stringBuilder.AppendLine("<P_Charge>" + (ligne.IsPrisCharge ? "1" : "0") + "</P_Charge>");
				stringBuilder.AppendLine($"<MontantHT>{(long)(ligne.MontantHorsTaxe * 1000m)}</MontantHT>");
				stringBuilder.AppendLine("<TauxRS>" + ligne.Taux.ToString("n2").Replace(',', '.') + "</TauxRS>");
				stringBuilder.AppendLine("<TauxTVA>" + ligne.TauxTva.ToString("n2").Replace(',', '.') + "</TauxTVA>");
				stringBuilder.AppendLine($"<MontantTVA>{(long)(ligne.MontantTva * 1000m)}</MontantTVA>");
				stringBuilder.AppendLine($"<MontantTTC>{(long)(ligne.Base * 1000m)}</MontantTTC>");
				stringBuilder.AppendLine($"<MontantRS>{(long)(ligne.MontantDeviseSociete * 1000m)}</MontantRS>");
				stringBuilder.AppendLine($"<MontantNetServi>{(long)(ligne.MontantNetPaye * 1000m)}</MontantNetServi>");
				if (defaultDeviseSociete.No != ligne.DeviseNo && deviseView.Code != "TND")
				{
					stringBuilder.AppendLine("<Devise>");
					stringBuilder.AppendLine("<CodeDevise>" + deviseView.Code + "</CodeDevise>");
					stringBuilder.AppendLine("<TauxChange>" + num2.ToString("n5").Replace(',', '.') + "</TauxChange>");
					stringBuilder.AppendLine("<MontantRSDevise>" + GetStringValue(ligne.Montant * num2).Replace(',', '.') + "</MontantRSDevise>");
					stringBuilder.AppendLine("<MontantTTCDevise>" + GetStringValue(ligne.Base * num2).Replace(',', '.') + "</MontantTTCDevise>");
					stringBuilder.AppendLine("<MontantNetServiDevise>" + GetStringValue(ligne.MontantNetPaye * num2).Replace(',', '.') + "</MontantNetServiDevise>");
					stringBuilder.AppendLine("</Devise>");
				}
				stringBuilder.AppendLine("</Operation>");
				stringBuilder.AppendLine("</ListeOperations>");
				stringBuilder.AppendLine("<TotalPayement>");
				stringBuilder.AppendLine($"<TotalMontantHT>{(long)(ligne.MontantHorsTaxe * 1000m)}</TotalMontantHT>");
				stringBuilder.AppendLine($"<TotalMontantTVA>{(long)(ligne.MontantTva * 1000m)}</TotalMontantTVA>");
				stringBuilder.AppendLine($"<TotalMontantTTC>{(long)(ligne.Base * 1000m)}</TotalMontantTTC>");
				stringBuilder.AppendLine($"<TotalMontantRS>{(long)(ligne.Montant * 1000m)}</TotalMontantRS>");
				stringBuilder.AppendLine($"<TotalMontantNetServi>{(long)(ligne.MontantNetPaye * 1000m)}</TotalMontantNetServi>");
				if (defaultDeviseSociete.No != ligne.DeviseNo && deviseView.Code != "TND")
				{
					stringBuilder.AppendLine("<TotalDevise>");
					stringBuilder.AppendLine("<TotalMontantDevise Code=\"" + deviseView.Code + "\">");
					stringBuilder.AppendLine("<TotalMontantRS>" + GetStringValue(ligne.Montant * num2).Replace(',', '.') + "</TotalMontantRS>");
					stringBuilder.AppendLine("<TotalMontantTTC>" + GetStringValue(ligne.Base * num2).Replace(',', '.') + "</TotalMontantTTC>");
					stringBuilder.AppendLine("<TotalMontantNetServi>" + GetStringValue(ligne.MontantNetPaye * num2).Replace(',', '.') + "</TotalMontantNetServi>");
					stringBuilder.AppendLine("</TotalMontantDevise>");
					stringBuilder.AppendLine("</TotalDevise>");
				}
				stringBuilder.AppendLine("</TotalPayement>");
				stringBuilder.AppendLine("</Certificat>");
			}
			stringBuilder.AppendLine("</AjouterCertificats>");
			stringBuilder.AppendLine("</DeclarationsRS>");
			path = path + "\\" + $"Declaration TEJ {societe.RaisonSociale} {DateTime.Now:yyyyMMddhhmmss }";
			Directory.CreateDirectory(path);
			File.WriteAllText(path + string.Format("\\{0}-{1}-{2}-{3}.xml", text, declarationRetenuSource.Exercice, new DateTime(1900, (int)declarationRetenuSource.MoisPeriode, 1).ToString("MM"), (declarationRetenuSource.Nature == NatureDeclaration.Initiale) ? "0" : "1"), stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	public void GenerateTunisieTimelek(int declarationNo, string path, int nbDecimal, IProgress<int> progress, CancellationToken cancellationToken)
	{
		SocieteManager societeManager = _groupe.SocieteManager;
		_ = societeManager.CaisseManager;
		Societe societe = societeManager.Societe;
		DeclarationRetenuSource declarationRetenuSource = societeManager.DeclarationRetenuSourceGet(declarationNo);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration [" + declarationRetenuSource.Numero + "] n'est pas clôturée.");
		}
		if (declarationRetenuSource.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier de la déclaration [" + declarationRetenuSource.Numero + "] est déjà généré.");
		}
		if (!societeManager.DeclarationRetenuSourceHasLignes(declarationNo))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		List<RetenuALaSource> list = societeManager.RetenueSourceTejGetAllByDeclaration(declarationNo).ToList();
		if (!list.Any())
		{
			throw new ApplicationException("Aucune ligne pour la déclaration.");
		}
		societe.GetDefaultDeviseSociete();
		List<OperationRetenuSource> all = _groupe.OperationRetenuSourceManager.GetAll();
		if (string.IsNullOrEmpty(societe.Identifiant))
		{
			throw new ApplicationException("Veuillez vérifier l'identifiant de la société.");
		}
		try
		{
			string text = ((societe.Identifiant.Length > 8) ? societe.Identifiant.Substring(0, 8) : societe.Identifiant);
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
			stringBuilder.AppendLine("<DeclarationsRS VersionSchema=\"1.0\">");
			stringBuilder.AppendLine("<Declarant>");
			stringBuilder.AppendLine("<TypeIdentifiant>1</TypeIdentifiant>");
			stringBuilder.AppendLine("<Identifiant>" + text + "</Identifiant>");
			stringBuilder.AppendLine("<CategorieContribuable>PM</CategorieContribuable>");
			stringBuilder.AppendLine("</Declarant>");
			stringBuilder.AppendLine("<ReferenceDeclaration>");
			stringBuilder.AppendLine("<ActeDepot>0</ActeDepot>");
			stringBuilder.AppendLine($"<AnneeDepot>{declarationRetenuSource.Exercice}</AnneeDepot>");
			stringBuilder.AppendLine("<MoisDepot>" + new DateTime(1900, (int)declarationRetenuSource.MoisPeriode, 1).ToString("MM") + "</MoisDepot>");
			stringBuilder.AppendLine("</ReferenceDeclaration>");
			stringBuilder.AppendLine("<AjouterCertificats>");
			decimal num = default(decimal);
			foreach (RetenuALaSource ligne in list)
			{
				cancellationToken.ThrowIfCancellationRequested();
				progress.Report((int)(num++ / (decimal)list.Count * 100m));
				if (!ligne.OperationRetenueNo.HasValue)
				{
					throw new ApplicationException("Code opération invalide. RS: [" + ligne.Numero + "]");
				}
				IErpFournisseur erpFournisseur = _fournisseurErpHelper.Get(ligne.FournisseurNo);
				if (erpFournisseur == null)
				{
					throw new ApplicationException("Impossible de charger le fournisseur [" + ligne.FournisseurCode + "]");
				}
				IErpTiersIce infoFournisseur = _fournisseurErpHelper.GetInfoFournisseur(ligne.FournisseurNo);
				if (infoFournisseur == null)
				{
					throw new ApplicationException("Impossible de charger les informations complementaire du fournisseur [" + ligne.FournisseurCode + "]");
				}
				OperationRetenuSource operationRetenuSource = all.FirstOrDefault((OperationRetenuSource x) => x.No == ligne.OperationRetenueNo);
				if (operationRetenuSource == null)
				{
					throw new ApplicationException("Impossible de déterminer l'opération de retenue [" + ligne.Numero + "].");
				}
				if (string.IsNullOrEmpty(infoFournisseur.TiersIdentifiant))
				{
					throw new ApplicationException("L'identifiant du fournisseur [" + infoFournisseur.TiersCode + "] est obligatoire.");
				}
				if (string.IsNullOrEmpty(infoFournisseur.TiersActivite))
				{
					throw new ApplicationException("L'activité du fournisseur [" + infoFournisseur.TiersCode + "] est obligatoire.");
				}
				if (string.IsNullOrEmpty(infoFournisseur.TiersEmail))
				{
					throw new ApplicationException("L'email du fournisseur [" + infoFournisseur.TiersCode + "] est obligatoire.");
				}
				if (string.IsNullOrEmpty(erpFournisseur.Telephone))
				{
					throw new ApplicationException("Le téléphone du fournisseur [" + infoFournisseur.TiersCode + "] est obligatoire.");
				}
				if (string.IsNullOrEmpty(erpFournisseur.Adresse))
				{
					throw new ApplicationException("L'adresse du fournisseur [" + infoFournisseur.TiersCode + "] est obligatoire.");
				}
				string text2 = ((infoFournisseur.TiersIdentifiant.Length > 8) ? infoFournisseur.TiersIdentifiant.Substring(0, 8) : infoFournisseur.TiersIdentifiant);
				NatureFournisseur natureFournisseur = _fournisseurErpHelper.GetNatureFournisseur(ligne.FournisseurCode);
				_deviseHelper.Get(ligne.DeviseNo);
				decimal num2 = ((ligne.Cours == 0m) ? 1m : Math.Round(ligne.Cours, 5));
				stringBuilder.AppendLine("<Certificat>");
				stringBuilder.AppendLine("<Beneficiaire>");
				stringBuilder.AppendLine("<IdTaxpayer>");
				switch (natureFournisseur)
				{
				case NatureFournisseur.PersonneMorale:
					stringBuilder.AppendLine("<MatriculeFiscal>");
					stringBuilder.AppendLine("<TypeIdentifiant>1</TypeIdentifiant>");
					stringBuilder.AppendLine("<Identifiant>" + text2 + "</Identifiant>");
					stringBuilder.AppendLine("<CategorieContribuable>PM</CategorieContribuable>");
					stringBuilder.AppendLine("</MatriculeFiscal>");
					break;
				case NatureFournisseur.PersonnePhysique:
					stringBuilder.AppendLine("<CIN>");
					stringBuilder.AppendLine("<TypeIdentifiant>2</TypeIdentifiant>");
					stringBuilder.AppendLine("<Identifiant>" + text2 + "</Identifiant>");
					stringBuilder.AppendLine("<DateNaissance>" + $"{infoFournisseur.TiersDateNaissance:MM/dd/yyyy}" + "</DateNaissance>");
					stringBuilder.AppendLine("<CategorieContribuable>PP</CategorieContribuable>");
					stringBuilder.AppendLine("</CIN>");
					break;
				}
				decimal value = GetValue(ligne.MontantHorsTaxe * num2);
				decimal value2 = GetValue(ligne.MontantTva * num2);
				decimal value3 = GetValue(value + value2);
				decimal value4 = GetValue(value3 * ligne.Taux / 100m);
				decimal value5 = GetValue(value3 - value4);
				stringBuilder.AppendLine("</IdTaxpayer>");
				stringBuilder.AppendLine("<Resident>1</Resident>");
				stringBuilder.AppendLine("<NometprenonOuRaisonsociale>" + erpFournisseur.Intitule + "</NometprenonOuRaisonsociale>");
				stringBuilder.AppendLine("<Adresse>" + erpFournisseur.Adresse + "</Adresse>");
				stringBuilder.AppendLine("<Activite>" + infoFournisseur.TiersActivite + "</Activite>");
				stringBuilder.AppendLine("<InfosContact>");
				stringBuilder.AppendLine("<AdresseMail>" + infoFournisseur.TiersEmail + "</AdresseMail>");
				stringBuilder.AppendLine("<NumTel>" + erpFournisseur.Telephone + "</NumTel>");
				stringBuilder.AppendLine("</InfosContact>");
				stringBuilder.AppendLine("</Beneficiaire>");
				stringBuilder.AppendLine("<DatePayement>" + ligne.Date.ToString("dd/MM/yyyy") + "</DatePayement>");
				stringBuilder.AppendLine("<Ref_certif_chez_declarant>" + ligne.Numero + "</Ref_certif_chez_declarant>");
				stringBuilder.AppendLine("<ListeOperations>");
				stringBuilder.AppendLine("<Operation IdTypeOperation=\"" + operationRetenuSource.Code + "\">");
				stringBuilder.AppendLine($"<AnneeFacturation>{ligne.AnneeFacturation}</AnneeFacturation>");
				stringBuilder.AppendLine("<CNPC>" + (ligne.IsConvention ? "1" : "0") + "</CNPC>");
				stringBuilder.AppendLine("<P_Charge>" + (ligne.IsPrisCharge ? "1" : "0") + "</P_Charge>");
				stringBuilder.AppendLine($"<MontantHT>{(long)(value * 1000m)}</MontantHT>");
				stringBuilder.AppendLine("<TauxRS>" + ligne.Taux.ToString("n2").Replace(',', '.') + "</TauxRS>");
				stringBuilder.AppendLine("<TauxTVA>" + ligne.TauxTva.ToString("n2").Replace(',', '.') + "</TauxTVA>");
				stringBuilder.AppendLine($"<MontantTVA>{(long)(value2 * 1000m)}</MontantTVA>");
				stringBuilder.AppendLine($"<MontantTTC>{(long)(value3 * 1000m)}</MontantTTC>");
				stringBuilder.AppendLine($"<MontantRS>{(long)(value4 * 1000m)}</MontantRS>");
				stringBuilder.AppendLine($"<MontantNetServi>{(long)(value5 * 1000m)}</MontantNetServi>");
				stringBuilder.AppendLine("</Operation>");
				stringBuilder.AppendLine("</ListeOperations>");
				stringBuilder.AppendLine("<TotalPayement>");
				stringBuilder.AppendLine($"<TotalMontantHT>{(long)(value * 1000m)}</TotalMontantHT>");
				stringBuilder.AppendLine($"<TotalMontantTVA>{(long)(value2 * 1000m)}</TotalMontantTVA>");
				stringBuilder.AppendLine($"<TotalMontantTTC>{(long)(value3 * 1000m)}</TotalMontantTTC>");
				stringBuilder.AppendLine($"<TotalMontantRS>{(long)(value4 * 1000m)}</TotalMontantRS>");
				stringBuilder.AppendLine($"<TotalMontantNetServi>{(long)(value5 * 1000m)}</TotalMontantNetServi>");
				stringBuilder.AppendLine("</TotalPayement>");
				stringBuilder.AppendLine("</Certificat>");
			}
			stringBuilder.AppendLine("</AjouterCertificats>");
			stringBuilder.AppendLine("</DeclarationsRS>");
			path = path + "\\" + $"Declaration TEJ {societe.RaisonSociale} {DateTime.Now:yyyyMMddhhmmss }";
			Directory.CreateDirectory(path);
			File.WriteAllText(path + string.Format("\\{0}-{1}-{2}-0.xml", text, declarationRetenuSource.Exercice, new DateTime(1900, (int)declarationRetenuSource.MoisPeriode, 1).ToString("MM")), stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	private void GenerateMarroc(int declarationNo, string path, int nbDecimal, IProgress<int> progress, CancellationToken cancellationToken)
	{
		SocieteManager societeManager = _groupe.SocieteManager;
		CaisseManager caisseManager = societeManager.CaisseManager;
		Societe societe = societeManager.Societe;
		DeclarationRetenuSource declarationRetenuSource = societeManager.DeclarationRetenuSourceGet(declarationNo);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration [" + declarationRetenuSource.Numero + "] n'est pas clôturée.");
		}
		if (declarationRetenuSource.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier de la déclaration [" + declarationRetenuSource.Numero + "] est déjà généré.");
		}
		if (!societeManager.DeclarationRetenuSourceHasLignes(declarationNo))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		List<LigneDeclarationRetenuSource> list = societeManager.DeclarationRetenuSourceLigneGetAll(declarationNo).ToList();
		if (!list.Any())
		{
			throw new ApplicationException("Aucune ligne pour la déclaration.");
		}
		List<IErpTiersIce> source = _fournisseurErpHelper.GetAllInfoFournisseur(reload: true).ToList();
		if (!source.Any())
		{
			throw new ApplicationException("");
		}
		List<DesignationDocument> all = _groupe.DesignationDocumentManager.GetAll();
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("<VersementRetenueSources>");
			stringBuilder.AppendLine("<identifiantFiscal>" + societe.Identifiant + "</identifiantFiscal>");
			stringBuilder.AppendLine($"<annee>{declarationRetenuSource.Exercice}</annee>");
			stringBuilder.AppendLine($"<periode>{(int)declarationRetenuSource.MoisPeriode}</periode>");
			stringBuilder.AppendLine("<regime>1</regime>");
			stringBuilder.AppendLine("<fournisseurs>");
			decimal num = default(decimal);
			foreach (LigneDeclarationRetenuSource item in list)
			{
				cancellationToken.ThrowIfCancellationRequested();
				progress.Report((int)(num++ / (decimal)list.Count * 100m));
				RetenuALaSource retenue = caisseManager.RetenueGet(item.RetenueNo);
				if (retenue == null)
				{
					throw new ApplicationException("Impossible de charger la retenue.");
				}
				if (!retenue.RetenuePourEcheanceNo.HasValue)
				{
					continue;
				}
				Echeance echeance = societeManager.EcheanceGet(retenue.RetenuePourEcheanceNo.Value);
				if (echeance == null || !echeance.DesignationDocumentNo.HasValue)
				{
					continue;
				}
				DesignationDocument designationDocument = all.SingleOrDefault((DesignationDocument x) => x.No == echeance.DesignationDocumentNo.Value);
				if (designationDocument == null)
				{
					continue;
				}
				IErpTiersIce erpTiersIce = source.SingleOrDefault((IErpTiersIce x) => x.TiersCode == retenue.FournisseurCode);
				if (erpTiersIce == null || string.IsNullOrEmpty(erpTiersIce.TiersIdentifiant))
				{
					throw new ApplicationException("L'identifiant du fournisseur [" + retenue.FournisseurCode + "] est invalide.");
				}
				if (echeance.Type != EcheanceType.Erp || (echeance.DocumentType != ErpDocumentType.FactureFournisseur && echeance.DocumentType != ErpDocumentType.FactureComptaFournisseur))
				{
					continue;
				}
				ErpDocument erpDocument = _erpCommService.GetDocument(useOm: false, echeance.Domaine, ErpDocumentType.FactureFournisseur, echeance.DocumentNumero) as ErpDocument;
				if (erpDocument == null)
				{
					erpDocument = _erpCommService.GetDocument(useOm: false, echeance.Domaine, ErpDocumentType.FactureComptaFournisseur, echeance.DocumentNumero) as ErpDocument;
				}
				if (erpDocument == null)
				{
					continue;
				}
				List<LigneDossierEcheanceTvaNonRecuperable> list2 = societeManager.LigneDossierEcheanceTvaGetAll(echeance.No);
				if (list2.Any())
				{
					foreach (LigneDossierEcheanceTvaNonRecuperable item2 in list2)
					{
						stringBuilder.AppendLine("<fournisseur>");
						stringBuilder.AppendLine("<numFacture>" + echeance.DocumentNumero + "</numFacture>");
						stringBuilder.AppendLine("<ifuFournisseur>" + erpTiersIce?.TiersIdentifiant + "</ifuFournisseur>");
						stringBuilder.AppendLine("<datePaiement>" + retenue.Date.ToString("yyyy-MM-dd") + "</datePaiement>");
						stringBuilder.AppendLine("<dateOperation>" + retenue.Date.ToString("yyyy-MM-dd") + "</dateOperation>");
						stringBuilder.AppendLine($"<refNatOpt>{(int)designationDocument.NatureOperation}</refNatOpt>");
						stringBuilder.AppendLine($"<montantHT>{Math.Round(item2.BaseTva, nbDecimal)}</montantHT>");
						stringBuilder.AppendLine($"<tauxTva>{item2.TauxTaxe:n0}</tauxTva>");
						stringBuilder.AppendLine($"<tauxRetenuSource>{retenue.Taux:n0}</tauxRetenuSource>");
						stringBuilder.AppendLine("</fournisseur>");
					}
				}
				IEnumerable<ErpLigneTaxe> enumerable = erpDocument.Taxes.Where((ErpLigneTaxe x) => x.Type == ErpTypeTaxe.TvaDebit || x.Type == ErpTypeTaxe.TvaEncaissement);
				if (!enumerable.Any())
				{
					continue;
				}
				foreach (ErpLigneTaxe item3 in enumerable)
				{
					decimal num2 = Math.Round(item3.GetBase(erpDocument), societe.GetDefaultDeviseSociete().NombreDecimales);
					decimal num3 = Math.Round(echeance.Montant / erpDocument.TotalTouteTaxe, 6);
					decimal num4 = Math.Round(num2 * num3, societe.GetDefaultDeviseSociete().NombreDecimales);
					decimal num5 = Math.Round(Math.Round(num4 * (item3.Taux / 100m), societe.GetDefaultDeviseSociete().NombreDecimales) / retenue.Base, 6);
					decimal d = Math.Round(num4 / num5, societe.GetDefaultDeviseSociete().NombreDecimales);
					stringBuilder.AppendLine("<fournisseur>");
					stringBuilder.AppendLine("<numFacture>" + echeance.DocumentNumero + "</numFacture>");
					stringBuilder.AppendLine("<ifuFournisseur>" + erpTiersIce?.TiersIdentifiant + "</ifuFournisseur>");
					stringBuilder.AppendLine("<datePaiement>" + retenue.Date.ToString("yyyy-MM-dd") + "</datePaiement>");
					stringBuilder.AppendLine("<dateOperation>" + retenue.Date.ToString("yyyy-MM-dd") + "</dateOperation>");
					stringBuilder.AppendLine($"<refNatOpt>{(int)designationDocument.NatureOperation}</refNatOpt>");
					stringBuilder.AppendLine($"<montantHT>{Math.Round(d, nbDecimal)}</montantHT>");
					stringBuilder.AppendLine($"<tauxTva>{item3.Taux:n0}</tauxTva>");
					stringBuilder.AppendLine($"<tauxRetenuSource>{retenue.Taux:n0}</tauxRetenuSource>");
					stringBuilder.AppendLine("</fournisseur>");
				}
			}
			stringBuilder.AppendLine("</fournisseurs>");
			stringBuilder.AppendLine("</VersementRetenueSources>");
			string text = $"{societe.Identifiant}-{(int)declarationRetenuSource.MoisPeriode}-{declarationRetenuSource.Exercice}";
			string path2 = path + "\\" + text + ".xml";
			string path3 = path + "\\" + text + ".zip";
			File.WriteAllText(path2, stringBuilder.ToString());
			using FileStream stream = new FileStream(path3, FileMode.Create);
			using ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Create);
			using Stream destination = zipArchive.CreateEntry(Path.GetFileName(path2)).Open();
			using FileStream fileStream = File.OpenRead(path2);
			fileStream.CopyTo(destination);
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	private decimal GetValue(decimal oldValue)
	{
		return (decimal)decimal.ToInt64(oldValue * 1000m) / 1000m;
	}

	private string GetStringValue(decimal oldValue)
	{
		decimal value = GetValue(oldValue);
		return $"{value}";
	}
}
