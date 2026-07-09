using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IWorkflowHistoryRepository
{
	WorkflowHistory Get(int no);

	List<WorkflowHistory> GetAll(TypeEntity entityType, int entityNo);

	int Create(WorkflowHistory workflow);

	void Delete(WorkflowHistory workflow);
}
