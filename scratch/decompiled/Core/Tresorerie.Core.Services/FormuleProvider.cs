using System;
using System.IO;
using System.Reflection;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class FormuleProvider : IFormuleProvider
{
	private readonly IGroupeService _groupeService;

	private const string DOSSIER_FORMULES = "Formula";

	public FormuleProvider(IGroupeService groupeService)
	{
		_groupeService = groupeService ?? throw new ArgumentNullException("groupeService");
	}

	public string Get(string classeName)
	{
		DossierCommun dossierCommun = _groupeService.GetDossierCommun();
		if (!string.IsNullOrEmpty(dossierCommun.DossierSurcharge) && Directory.Exists(dossierCommun.DossierSurcharge))
		{
			string path = Path.Combine(dossierCommun.DossierSurcharge, "Formula", classeName + ".txt");
			if (File.Exists(path))
			{
				return File.ReadAllText(path);
			}
		}
		string path2 = Path.Combine(Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName)).FullName, "Formula", classeName + ".txt");
		if (!File.Exists(path2))
		{
			return string.Empty;
		}
		return File.ReadAllText(path2);
	}
}
