using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface INoteRepository
{
	int Create(Note note);

	void Delete(Note note);

	IEnumerable<Note> GetAllNotesByEntite(int entityNo, TypeEntity entityType);

	IEnumerable<Note> GetAllNotesByTypeEntite(TypeEntity entityType, int societeNo);

	IEnumerable<Note> GetAllNotesByClient(int clientNo, int societeNo);

	void Update(Note note);

	IEnumerable<Note> GetAllNotes(int societeNo);

	Note Get(int no);
}
