using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISecuriteRepository
{
	SecuriteDto Get();

	void Update(SecuriteDto securite);
}
