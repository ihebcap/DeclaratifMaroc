using System;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class SecuriteService
{
	private ISecuriteRepository _securiteRepository;

	public SecuriteService(ISecuriteRepository securiteRepository)
	{
		_securiteRepository = securiteRepository ?? throw new ArgumentNullException("securiteRepository");
	}

	public void Update(SecuriteDto securite)
	{
		_securiteRepository.Update(securite);
	}

	public SecuriteDto Get()
	{
		return _securiteRepository.Get();
	}
}
