using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class UtilisateurManager
{
	private readonly IUtilisateurRepository _utilisateurRepository;

	private readonly IUserPasswordRepository _userPasswordRepository;

	private readonly ISecuriteRepository _securiteRepository;

	private readonly IPasswordHasher _passwordHasher;

	public GroupeService Groupe { get; set; }

	public UtilisateurManager(IUtilisateurRepository utilisateurRepository, IPasswordHasher passwordHasher, IUserPasswordRepository userPasswordRepository, ISecuriteRepository securiteRepository)
	{
		_utilisateurRepository = utilisateurRepository ?? throw new ArgumentNullException("utilisateurRepository");
		_passwordHasher = passwordHasher ?? throw new ArgumentNullException("passwordHasher");
		_userPasswordRepository = userPasswordRepository ?? throw new ArgumentNullException("userPasswordRepository");
		_securiteRepository = securiteRepository ?? throw new ArgumentNullException("securiteRepository");
	}

	public void RefactAllAcount()
	{
		if (!GetAll().Any())
		{
			return;
		}
		foreach (Utilisateur item in GetAll())
		{
			if (item.Hash.Length == 0)
			{
				item.Salt = Guid.NewGuid().ToByteArray();
				item.Hash = _passwordHasher.Hash(item.Password, item.Salt);
				item.Password = "";
				_utilisateurRepository.Update(item);
			}
		}
	}

	public int? Create(Utilisateur utilisateur)
	{
		if (utilisateur == null)
		{
			throw new ArgumentNullException("utilisateur");
		}
		if (!Groupe.SocieteManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreateUtilisateur);
		}
		if (string.IsNullOrEmpty(utilisateur.Login))
		{
			throw new ApplicationException("Login invalide!");
		}
		if (GetByLogin(utilisateur.Login) != null)
		{
			throw new ApplicationException("Utilisateur exist déjà!");
		}
		byte[] salt = Guid.NewGuid().ToByteArray();
		byte[] hash = _passwordHasher.Hash(utilisateur.Password, salt);
		utilisateur.Salt = salt;
		utilisateur.Hash = hash;
		utilisateur.Password = "";
		int? result = _utilisateurRepository.Create(utilisateur);
		if (result.HasValue)
		{
			UserPassword userPassword = new UserPassword
			{
				UserId = result.Value,
				DateModification = DateTime.Now,
				Salt = salt,
				Hash = hash
			};
			_userPasswordRepository.Create(userPassword);
		}
		return result;
	}

	public void Delete(Utilisateur utilisateur)
	{
		if (utilisateur == null)
		{
			throw new ArgumentNullException("utilisateur");
		}
		if (GetByLogin(utilisateur.Login) == null)
		{
			throw new ArgumentNullException("utilisateur");
		}
		SocieteManager societeManager = Groupe.SocieteManager;
		Utilisateur utilisateur2 = societeManager.Utilisateur;
		if (!utilisateur2.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeleteUtilisateur, utilisateur.Login));
		}
		if (utilisateur.Equals(utilisateur2))
		{
			throw new ApplicationException(rcRessources.UtilisateurSuppression);
		}
		if (societeManager.UserHasMouvement(utilisateur.No))
		{
			throw new ApplicationException(rcRessources.UtilisateurMouvement);
		}
		_utilisateurRepository.Delete(utilisateur);
	}

	public IEnumerable<Utilisateur> GetAll()
	{
		return _utilisateurRepository.GetAll();
	}

	public Utilisateur GetByLogin(string login)
	{
		if (string.IsNullOrEmpty(login))
		{
			throw new ArgumentNullException("login");
		}
		return _utilisateurRepository.Get(login);
	}

	public bool IsPasswordExpirer(int userId)
	{
		if (!_securiteRepository.Get().PasswordRenouvellement)
		{
			return false;
		}
		UserPassword lastUserPassword = _userPasswordRepository.GetLastUserPassword(userId);
		if (lastUserPassword == null)
		{
			return true;
		}
		return (int)(DateTime.Now - lastUserPassword.DateModification).TotalDays >= 90;
	}

	public Utilisateur GetByNo(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		return _utilisateurRepository.Get(no);
	}

	public Utilisateur GetNewUtilisateur(string login, string password)
	{
		if (string.IsNullOrEmpty(login))
		{
			throw new ArgumentNullException("login");
		}
		if (string.IsNullOrEmpty(password))
		{
			throw new ArgumentNullException("password");
		}
		return new Utilisateur
		{
			Login = login,
			Password = password
		};
	}

	public void Update(Utilisateur utilisateur)
	{
		if (utilisateur == null)
		{
			throw new ArgumentNullException("utilisateur");
		}
		SocieteManager societeManager = Groupe.SocieteManager;
		_ = societeManager.Societe;
		if (!societeManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateUtilisateur, utilisateur.Login));
		}
		if (GetByLogin(utilisateur.Login) == null)
		{
			throw new ArgumentNullException("utilisateur");
		}
		if (utilisateur.Login.ToUpper() == "ADMIN")
		{
			utilisateur.MenuGrcId = null;
			utilisateur.MenuGrfId = null;
		}
		_utilisateurRepository.Update(utilisateur);
	}

	public void UpdateModeAffichage(Utilisateur utilisateur)
	{
		if (utilisateur == null)
		{
			throw new ArgumentNullException("utilisateur");
		}
		if (GetByLogin(utilisateur.Login) == null)
		{
			throw new ArgumentNullException("utilisateur");
		}
		_utilisateurRepository.Update(utilisateur);
	}

	public void UpdatePwd(string login, string newPwd)
	{
		if (_securiteRepository.Get().PasswordLength && newPwd.Length < 8)
		{
			throw new Exception("longueur minimale du mot de passe 8 caractères");
		}
		Utilisateur utilisateur = _utilisateurRepository.Get(login);
		if (utilisateur == null)
		{
			throw new ArgumentNullException("utilisateur");
		}
		utilisateur.Hash = _passwordHasher.Hash(newPwd, utilisateur.Salt);
		utilisateur.Password = "";
		_utilisateurRepository.Update(utilisateur);
		UserPassword userPassword = new UserPassword
		{
			UserId = utilisateur.No,
			DateModification = DateTime.Now,
			Salt = utilisateur.Salt,
			Hash = utilisateur.Hash
		};
		_userPasswordRepository.Create(userPassword);
	}
}
