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

namespace Tresorerie.UIDeclarationTva.Infrastructures;

public class DeclarationTvaEncaissementFileGenerator
{
	private readonly IGroupeService _groupe;

	public DeclarationTvaEncaissementFileGenerator(IGroupeService groupe)
	{
		_groupe = groupe ?? throw new ArgumentNullException("groupe");
	}

	public void Generate(int declarationNo, string path, int nbDecimal, IProgress<int> progress, CancellationToken cancellationToken)
	{
		SocieteManager societeManager = _groupe.SocieteManager;
		Societe societe = societeManager.Societe;
		DeclarationTvaEncaissement declarationTvaEncaissement = societeManager.DeclarationTvaEncaissementGet(declarationNo);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration [" + declarationTvaEncaissement.Numero + "] n'est pas clôturée.");
		}
		if (declarationTvaEncaissement.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier de la déclaration [" + declarationTvaEncaissement.Numero + "] est déjà généré.");
		}
		IEnumerable<LigneDeclarationTvaEncaissement> enumerable = from x in societeManager.DeclarationTvaEncaissementLigneGetAll(declarationNo)
			where !x.IsReport
			select x;
		if (!enumerable.Any())
		{
			throw new ApplicationException("La déclaration [" + declarationTvaEncaissement.Numero + "] ne contient aucune ligne.");
		}
		NumberFormatInfo numberFormatInfo = new NumberFormatInfo();
		numberFormatInfo.NumberDecimalSeparator = ".";
		string text = $"\r\n<DeclarationReleveDeduction>\r\n<identifiantFiscal>{societe.Identifiant}</identifiantFiscal>\r\n<annee>{declarationTvaEncaissement.Exercice}</annee>\r\n<periode>{((declarationTvaEncaissement.TypeDeclaration == TypeDeclarationTva.Mensuelle) ? ((int)declarationTvaEncaissement.MoisPeriode) : ((int)declarationTvaEncaissement.TrimestrePeriode))}</periode>\r\n<regime>{((declarationTvaEncaissement.TypeDeclaration == TypeDeclarationTva.Mensuelle) ? 1 : 2)}</regime>\r\n<releveDeductions>";
		decimal num = default(decimal);
		int num2 = 1;
		foreach (LigneDeclarationTvaEncaissement item in enumerable)
		{
			if (societe.LegislationType == Legislation.Maroc)
			{
				if (item.TiersIdentifiant.Length != 8)
				{
					throw new ApplicationException($"Longueur identifiant invalide. 8 caractères requis. ligne {num2}");
				}
				if (item.TiersIce.Length != 15)
				{
					throw new ApplicationException($"Longueur Ice invalide. 15 caractères requis. ligne {num2}");
				}
				if (item.TiersIdentifiant.Contains(" "))
				{
					throw new ApplicationException($"Identifiant, les espaces ne sont pas autorisés. ligne {num2}");
				}
				if (item.TiersIce.Contains(" "))
				{
					throw new ApplicationException($"Ice, les espaces ne sont pas autorisés. ligne {num2}");
				}
			}
			cancellationToken.ThrowIfCancellationRequested();
			progress.Report((int)(num++ / (decimal)enumerable.Count() * 100m));
			int num3 = 0;
			switch (item.TypePayement)
			{
			case ReglementType.Espece:
				num3 = 1;
				break;
			case ReglementType.Cheque:
				num3 = 2;
				break;
			case ReglementType.Traite:
				num3 = 5;
				break;
			case ReglementType.Virement:
				num3 = 4;
				break;
			case ReglementType.Autre:
				num3 = 7;
				break;
			}
			if (item.EntityType == LigneDeclarationTvaEncaissementEntityType.OperationBancaire)
			{
				num3 = 3;
			}
			text += string.Format("\r\n<rd>\r\n    <ord>{0}</ord>\r\n    <num>{1}</num>\r\n    <des>{2}</des>\r\n    <mht>{3}</mht>\r\n    <tva>{4}</tva>\r\n    <ttc>{5}</ttc>\r\n    <refF>\r\n        <if>{6}</if>\r\n        <nom>{7}</nom>\r\n        <ice>{8}</ice>\r\n    </refF>\r\n    <tx>{9}</tx>\r\n    <prorata>{10}</prorata>\r\n    <mp>\r\n        <id>{11}</id>\r\n    </mp>\r\n    <dpai>{12}</dpai>\r\n    <dfac>{13}</dfac>\r\n</rd>\r\n", num, item.DocumentNumero, item.DesignationDocument, Math.Round(item.Assiette, nbDecimal).ToString(numberFormatInfo), Math.Round(item.Montant, nbDecimal).ToString(numberFormatInfo), Math.Round(item.Assiette + item.Montant, nbDecimal).ToString(numberFormatInfo), item.TiersIdentifiant, item.TiersIntitule, item.TiersIce, item.Taux, item.Prorata, num3, item.DateMouvement.ToString("yyyy-MM-dd"), item.DateDocument.ToString("yyyy-MM-dd"));
			num2++;
		}
		text += "\r\n</releveDeductions>\r\n</DeclarationReleveDeduction>\r\n";
		string arg = ((declarationTvaEncaissement.TypeDeclaration == TypeDeclarationTva.Mensuelle) ? $"M{(int)declarationTvaEncaissement.MoisPeriode}" : $"T{(int)declarationTvaEncaissement.TrimestrePeriode}");
		string text2 = $"{declarationTvaEncaissement.Numero}-{declarationTvaEncaissement.Exercice}-{arg}";
		string path2 = path + "\\" + text2 + ".xml";
		string path3 = path + "\\" + text2 + ".zip";
		if (File.Exists(path2))
		{
			throw new ApplicationException("Le fichier XML [" + text2 + "] existe déja.");
		}
		if (File.Exists(path3))
		{
			throw new ApplicationException("Le fichier ZIP [" + text2 + "] existe déja.");
		}
		File.WriteAllText(path2, text);
		using FileStream stream = new FileStream(path3, FileMode.Create);
		using ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Create);
		using Stream destination = zipArchive.CreateEntry(Path.GetFileName(path2)).Open();
		using FileStream fileStream = File.OpenRead(path2);
		fileStream.CopyTo(destination);
	}
}
