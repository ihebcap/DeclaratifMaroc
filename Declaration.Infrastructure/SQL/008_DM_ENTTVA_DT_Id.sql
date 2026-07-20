-- TASK-094 (Option B) : persiste sur DM_ENTTVA la même valeur DT_Id posée sur RT_AFFECTATION à
-- la clôture (DeclarationWorkflowService.CloturerDeclarationAsync), pour permettre un JOIN direct
-- RT_AFFECTATION.DT_Id = DM_ENTTVA.DT_Id sans recalculer DeriveDtId côté diagnostic. Élimine le
-- risque de divergence entre runtimes .NET constaté sur l'incident du 14/07/2026
-- (Guid.GetHashCode() diffère entre .NET Framework et .NET Core/.NET 5+, cf. DONE_DETAIL/
-- TASK-094 — un recalcul manuel avait qualifié à tort d'« orphelin » le tampon réel d'une
-- déclaration Cloturee).
--
-- Colonne additive, NULLABLE, sur DM_ENTTVA UNIQUEMENT (jamais sur RT_* partagées, garde-fou §3
-- de la task). NE RÉSOUT PAS RÉTROACTIVEMENT les déclarations closes AVANT cette migration :
-- leur DT_Id reste NULL (aucune valeur fiable à backfiller en SQL pur, l'algorithme vit côté
-- C#) — seules les clôtures/réouvertures FUTURES la posent/l'effacent.
--
-- Idempotent : n'ajoute la colonne que si elle est absente.
-- Cible : base de persistance dédiée (PersistenceConnection). Jamais GRFN.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_ENTTVA' AND COLUMN_NAME = 'DT_Id'
)
BEGIN
    ALTER TABLE [DM_ENTTVA] ADD [DT_Id] INT NULL;
END;
GO
