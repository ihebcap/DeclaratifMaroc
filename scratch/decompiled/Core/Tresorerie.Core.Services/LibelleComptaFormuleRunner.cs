using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Serilog;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure;

namespace Tresorerie.Core.Services;

public class LibelleComptaFormuleRunner : ILibelleComptaFormuleRunner
{
	private readonly IFormuleProvider _formuleProvider;

	private readonly string _classCode = "\r\nusing Tresorerie.Core.Models;\r\nusing Tresorerie.Core.Enum;\r\nusing System.Linq;\r\nusing System;\r\n\r\nnamespace Tresorerie.Core.Services\r\n{\r\n    public class LibelleComptaFormulaGenerator\r\n    {\r\n            #FUNCTION\r\n    }\r\n}";

	public LibelleComptaFormuleRunner(IFormuleProvider formuleProvider)
	{
		_formuleProvider = formuleProvider ?? throw new ArgumentNullException("formuleProvider");
	}

	public string Run(IList<ParameterLibelleCompta> list)
	{
		string text = list.SingleOrDefault((ParameterLibelleCompta x) => x.Type == TypePropriety.Formula)?.Value?.ToString();
		string text2 = _formuleProvider.Get(text);
		if (string.IsNullOrEmpty(text2))
		{
			throw new ApplicationException("Impossible de charger le code de la fonction [" + text + "]!");
		}
		dynamic val = CodeDomEx.CompileThenCreateInstance(GenerateCode(text2), "Tresorerie.Core.Services.LibelleComptaFormulaGenerator");
		if (val is CompilerErrorCollection source)
		{
			List<CompilerError> source2 = source.OfType<CompilerError>().ToList();
			ApplicationException ex = new ApplicationException("Code classe invalide : " + source2.Select((CompilerError x) => $"[L{x.Line}]{x.ErrorText}").Aggregate((string x, string y) => x + Environment.NewLine + y));
			Log.Error(ex, ex.Message);
			throw ex;
		}
		MethodInfo methodInfo = val.GetType().GetMethod(text);
		object[] array = (from x in list
			where x.Type != TypePropriety.Formula
			select x.Value).ToArray();
		return methodInfo.Invoke(val, array);
	}

	private string GenerateCode(string formule)
	{
		return _classCode.Replace("#FUNCTION", formule);
	}
}
