-- =====================================================================
-- TASK-161 (GRF) — ALTER additif isolé, NON inclus dans DeclarationTVA.sql
-- =====================================================================
-- Contrairement aux ALTER de DeclarationTVA.sql (table DM_LGTVA, entierement possedee par le
-- module Declaration TVA / GRF), P_SOCIETECODEACTIVITETIERS est une table possedee par
-- l'application Tresorerie WinForms (apbs-gr_winform, EF Migrations —
-- Tresorerie.EF.Migrations\Migrations\202412311319299_AddSocieteCodeActiviteTiers.cs). Meme
-- principe que DeclarationTVA.sql ne touche jamais P_SOCIETE (cf. commentaire OrchestrateurDeclaration
-- "reference LOGIQUE a P_SOCIETE.SO_Id, volontairement SANS FK ... proprietaire de P_SOCIETE") :
-- ce script n'est PAS fusionne dans DeclarationTVA.sql et n'est PAS execute automatiquement par
-- Declaration.Setup. A executer manuellement, en coordination avec le proprietaire de
-- apbs-gr_winform (point d'arbitrage documente dans VERIFY/TASK-161_verify.md).
--
-- Objet : fiabiliser le matching tiers->activite (decision PO TASK-161 point 1) — la table clait
-- aujourd'hui le tiers sur un texte libre (SCAT_ErpIntitule, saisi manuellement dans l'ecran
-- UcSocieteCodeActiviteTiers) plutot que sur un identifiant stable. Ajout du numero tiers Sage
-- (SCAT_NumeroTiers) comme cle de matching fiable prioritaire, SCAT_ErpIntitule conservee pour
-- compatibilite/repli et pour l'affichage existant.
--
-- Additif strict : aucune colonne existante retiree/renommee. Confirme sans impact sur
-- SocieteCodeActiviteTiersRepository1.cs (apbs-gr_winform) qui utilise une liste de colonnes
-- explicite (SCAT_Id, SO_Id, CAT_Id, SCAT_ErpIntitule) sans jamais faire SELECT * ni dependre du
-- nombre de colonnes de la table (verifie par lecture du fichier avant cette migration).
-- La colonne restera NULL pour tout mapping cree via l'ecran WinForms existant jusqu'a ce que
-- cet ecran soit lui-meme mis a jour pour la capturer (dependance cross-applicatif hors perimetre
-- du depot GRF, cf. TASK-161.md section Risques).
--
-- Idempotent : peut etre rejoue sans risque.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'P_SOCIETECODEACTIVITETIERS' AND COLUMN_NAME = 'SCAT_NumeroTiers'
)
BEGIN
    ALTER TABLE [P_SOCIETECODEACTIVITETIERS] ADD [SCAT_NumeroTiers] NVARCHAR(50) NULL;
END;
GO
