using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class DesignationDocumentManager
{
	private readonly IDesignationDocumentRepository _designationDocumentRepository;

	public GroupeService Groupe { get; set; }

	public DesignationDocumentManager(IDesignationDocumentRepository designationDocumentRepository)
	{
		_designationDocumentRepository = designationDocumentRepository ?? throw new ArgumentNullException("designationDocumentRepository");
	}

	public int Create(string code, string description, DesignationDocumentNatureOperation natureOperation)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		if (string.IsNullOrEmpty(description))
		{
			throw new ArgumentNullException("description");
		}
		if (_designationDocumentRepository.Get(code) != null)
		{
			throw new InvalidOperationException("Désignation document existe déjà!");
		}
		DesignationDocument designationDocument = new DesignationDocument
		{
			Code = code,
			Intitule = description,
			NatureOperation = natureOperation
		};
		int? num = _designationDocumentRepository.Create(designationDocument);
		if (!num.HasValue)
		{
			throw new InvalidOperationException("Insertion invalide!");
		}
		return num.Value;
	}

	public void Delete(int designationDocumentNo)
	{
		if (designationDocumentNo <= 0)
		{
			throw new ArgumentNullException("designationDocumentNo");
		}
		DesignationDocument designationDocument = _designationDocumentRepository.Get(designationDocumentNo);
		if (designationDocument == null)
		{
			throw new ArgumentException("Désignation document invalide!");
		}
		if (_designationDocumentRepository.IsUsed(designationDocumentNo))
		{
			throw new ArgumentException("La désignation document est utilisé!");
		}
		_designationDocumentRepository.Delete(designationDocument);
	}

	public DesignationDocument Get(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		return _designationDocumentRepository.Get(code);
	}

	public DesignationDocument Get(int no)
	{
		return _designationDocumentRepository.Get(no);
	}

	public List<DesignationDocument> GetAll()
	{
		return _designationDocumentRepository.GetAll();
	}

	public DesignationDocument Init()
	{
		return new DesignationDocument();
	}

	public bool IsDesignationDocumentUsed(int designationDocumentNo)
	{
		if (_designationDocumentRepository.Get(designationDocumentNo) == null)
		{
			throw new ArgumentException("Désignation document invalide!");
		}
		return _designationDocumentRepository.IsUsed(designationDocumentNo);
	}

	public void Update(int no, string description, DesignationDocumentNatureOperation natureOperation)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		if (string.IsNullOrEmpty(description))
		{
			throw new ArgumentNullException("description");
		}
		DesignationDocument designationDocument = _designationDocumentRepository.Get(no);
		if (designationDocument == null)
		{
			throw new ArgumentException("Désignation document invalide!");
		}
		designationDocument.Intitule = description;
		designationDocument.NatureOperation = natureOperation;
		_designationDocumentRepository.Update(designationDocument);
	}
}
