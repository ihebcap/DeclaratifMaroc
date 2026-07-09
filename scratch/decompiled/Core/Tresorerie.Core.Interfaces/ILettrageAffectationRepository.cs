using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILettrageAffectationRepository
{
	IEnumerable<LettrageAffectation> GetAll(int societeNo, int clientNo, DateTime dateMinReg, DateTime dateMaxReg, DateTime dateMinEch, DateTime dateMaxEch);

	IEnumerable<LettrageAffectation> GetAll(int societeNo, int reglementNo);
}
