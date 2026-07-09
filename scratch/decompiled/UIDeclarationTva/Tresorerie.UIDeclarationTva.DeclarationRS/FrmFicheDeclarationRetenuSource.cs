using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DevExpress.Data;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Docking2010;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.ButtonPanel;
using DevExpress.XtraEditors.ButtonsPanelControl;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Mask;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Menu;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using DevExpress.XtraLayout.Utils;
using DevExpress.XtraSplashScreen;
using Serilog;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.UICommun.Components;
using Tresorerie.UICommun.Layout;
using Tresorerie.UICommun.Layout.Controllers;
using Tresorerie.UICommun.Tiers;
using Tresorerie.UIDeclarationTva.DeclarationRS.Controllers;
using Tresorerie.UIDeclarationTva.DeclarationRS.views;
using Tresorerie.UIDeclarationTva.Infrastructures;
using Tresorerie.UIDeclarationTva.Properties;
using Tresorerie.Win.Commun;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.DeclarationRS;

public class FrmFicheDeclarationRetenuSource : RibbonForm, IDeclarationRetenuSourceNotify, IEntityForm<DeclarationRetenuSourceView>, IGridLayoutCustomizable
{
	private readonly ListDeclarationRetenuSourceController _controller;

	private DeclarationRetenuSourceView _current;

	private readonly IFormFactory _formFactory;

	private Guid _gridGuid;

	private readonly LayoutController _layoutController;

	private const int _VISIBLE_INDEX_BTN_LIGNE_AJOUTER = 1;

	private const int _VISIBLE_INDEX_BTN_SUPPRIMER_TOUT = 2;

	private const int _INDEX_BTN_LIGNE_AJOUTER = 0;

	private const int _INDEX_BTN_SUPPRIMER_TOUT = 1;

	private const string CGridName = "La liste des modèles [Liste des lignes de déclaration de retenue à la source]";

	private readonly OverlayTextPainter overlayLabel;

	private readonly OverlayImagePainter overlayButton;

	private CancellationTokenSource tokenSource;

	private IOverlaySplashScreenHandle handleValider;

	private RepositoryItemGridLookUpEdit _repoCaisse;

	private RepositoryItemGridLookUpEdit _repoMode;

	private RepositoryItemGridLookUpEdit _repoModeCode;

	private RepositoryItemGridLookUpEdit _repoDevise;

	private readonly RepositoryItemHyperLinkEdit _repoLinkTiers;

	private readonly RepositoryItemHyperLinkEdit _repoLinkDossier;

	private GridColumn _colRetirerLigne;

	private IContainer components;

	private RibbonControl ribbon;

	private RibbonPage ribbonPage1;

	private LayoutControl layoutControl1;

	private LayoutControlGroup Root;

	private EmptySpaceItem emptySpaceItem1;

	private LayoutControlItem layoutControlItem1;

	private LayoutControlItem layoutControlItem2;

	private LayoutControlItem layoutControlItem3;

	private CheckEdit txtIsDepose;

	private LayoutControlItem layoutControlItem5;

	private LayoutControlItem layoutControlItem6;

	private LayoutControlItem layoutControlItem7;

	private TextEdit txtLibelle;

	private LayoutControlItem layoutControlItem8;

	private LayoutControlGroup layoutControlGroup1;

	private PopupMenu popupMenuActions;

	private GridControl gcLignes;

	private GridView gvLignes;

	private LayoutControlItem layoutControlItem11;

	private DateEdit txtDate;

	private ImageComboBoxEdit txtStatut;

	private GridLookUpEdit txtExercice;

	private GridView gridLookUpEdit1View;

	private DateEdit txtPeriodeDebut;

	private DateEdit txtPeriodeFin;

	private SimpleButton btnValider;

	private LayoutControlItem layoutControlItem12;

	private TextEdit txtNumero;

	private LayoutControlItem layoutControlItem15;

	private CheckEdit txtIsFichierGenere;

	private ImageComboBoxEdit txtMois;

	private LayoutControlItem layoutControlItem17;

	private LayoutControlItem lciTxtMois;

	private GridControl gcGroupement;

	private GridView gvGroupement;

	private SplitterItem splitterItem1;

	private LayoutControlItem layoutControlItem10;

	private LayoutControlGroup lcgLignes;

	private ImageCollection imageCollection;

	private LayoutControlGroup layoutControlGroup3;

	private CheckEdit txtIsComptabilise;

	private LayoutControlItem layoutControlItem18;

	private EmptySpaceItem emptySpaceItem3;

	private DropDownButton dropDownButton1;

	private LayoutControlItem btnAction;

	private BarButtonItem btnCloturer;

	private BarButtonItem btnGenererFichier;

	private BarButtonItem btnDeposer;

	private BarButtonItem btnImporter;

	private EmptySpaceItem emptySpaceItem2;

	private BarButtonItem btnExporterModelCsv;

	public int No { get; private set; }

	public Form EntityForm => this;

	public GridLayoutCutomizationMenu LayoutMenu { get; set; }

	public GridView View => gvLignes;

	public GridViewMenu ViewMenu { get; }

	public Guid GridGuid => _gridGuid;

	public int? AppliedLayoutNo { get; set; }

	public string GridName => "La liste des modèles [Liste des lignes de déclaration de retenue à la source]";

	private FrmFicheDeclarationRetenuSource()
	{
		InitializeComponent();
		txtDate.KeyDown += EnterEvent;
		txtExercice.KeyDown += EnterEvent;
		txtPeriodeDebut.KeyDown += EnterEvent;
		txtPeriodeFin.KeyDown += EnterEvent;
		txtLibelle.KeyDown += EnterEvent;
		txtMois.KeyDown += EnterEvent;
		btnValider.Click += Valider;
		btnCloturer.ItemClick += Cloturer;
		btnDeposer.ItemClick += Deposer;
		btnGenererFichier.ItemClick += GenererFichier;
		btnExporterModelCsv.ItemClick += BtnExporterModelCsv_ItemClick;
		lcgLignes.CustomButtonClick += GroupLigneButtonClick;
		gvLignes.KeyDown += GridViewKeyDown;
		txtExercice.EditValueChanged += ExerciceChanged;
		gvLignes.FocusedRowChanged += GvLignes_FocusedRowChanged;
		Image image = ImageSvgHelper.CreateImageFromSvg(Resources.cancel_normal);
		Image hoverImage = ImageSvgHelper.CreateImageFromSvg(Resources.cancel_active);
		overlayLabel = new OverlayTextPainter();
		overlayButton = new OverlayImagePainter(image, hoverImage)
		{
			ClickAction = delegate
			{
				Cancel();
			}
		};
		_repoLinkTiers = new RepositoryItemHyperLinkEdit
		{
			SingleClick = true
		};
		_repoLinkDossier = new RepositoryItemHyperLinkEdit
		{
			SingleClick = true
		};
		_repoLinkTiers.OpenLink += delegate
		{
			ShowFicheTiers();
		};
		_repoLinkDossier.OpenLink += delegate
		{
			ShowDossier();
		};
	}

	private void GvLignes_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
	{
		gvLignes.ClearColumnErrors();
		if (gvLignes.GetFocusedRow() is LigneDeclarationRetenueMarocView ligneDeclarationRetenueMarocView)
		{
			GridColumn column = gvLignes.Columns["TiersCode"];
			if (string.IsNullOrEmpty(ligneDeclarationRetenueMarocView.TiersCode))
			{
				gvLignes.SetColumnError(column, "Le tiers est obligatoire");
			}
			GridColumn column2 = gvLignes.Columns["TiersIdentifiant"];
			if (string.IsNullOrEmpty(ligneDeclarationRetenueMarocView.TiersIdentifiant))
			{
				gvLignes.SetColumnError(column2, "L'identifiant est obligatoire");
			}
			GridColumn column3 = gvLignes.Columns["RetenueNumero"];
			if (string.IsNullOrEmpty(ligneDeclarationRetenueMarocView.RetenueNumero))
			{
				gvLignes.SetColumnError(column3, "Le numéro de la retenue est obligatoire");
			}
			GridColumn column4 = gvLignes.Columns["EcheanceDesignationDocumentNo"];
			if (!ligneDeclarationRetenueMarocView.EcheanceDesignationDocumentNo.HasValue)
			{
				gvLignes.SetColumnError(column4, "La désignation du document est obligatoire");
			}
			gvLignes.UpdateCurrentRow();
		}
	}

	private void BtnExporterModelCsv_ItemClick(object sender, ItemClickEventArgs e)
	{
		try
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Title = "Enregistrer",
				DefaultExt = "CSV",
				Filter = "CSV document (*.CSV)|*.CSV",
				FileName = "LignesDeclarationRetenuSource.csv"
			};
			if (saveFileDialog.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(saveFileDialog.FileName))
			{
				_controller.GenerateCSVFile(saveFileDialog.FileName);
				XtraMessageBox.Show("Fichier créé avec succès.");
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, ex.Message);
			XtraMessageBox.Show(ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	public FrmFicheDeclarationRetenuSource(IFormFactory formFactory, ListDeclarationRetenuSourceController controller, LayoutController layoutController)
		: this()
	{
		_formFactory = formFactory ?? throw new ArgumentNullException("formFactory");
		_controller = controller ?? throw new ArgumentNullException("controller");
		_layoutController = layoutController ?? throw new ArgumentNullException("layoutController");
		Initialize();
		string defaultDeviseFormat = _controller.GetDefaultDeviseFormat();
		this.SetDecimalFormat(defaultDeviseFormat);
	}

	protected override void OnLoad(EventArgs e)
	{
		LayoutMenu = new GridLayoutCutomizationMenu(_layoutController, _formFactory, this);
		_gridGuid = (Guid)gvLignes.Tag;
		SetDefaultGridLayout();
		base.OnLoad(e);
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (tokenSource != null)
		{
			tokenSource.Cancel();
		}
	}

	private void Cancel()
	{
		if (tokenSource != null)
		{
			tokenSource.Cancel();
			if (handleValider != null)
			{
				SplashScreenManager.CloseOverlayForm(handleValider);
			}
		}
	}

	public void SetCurrentView(DeclarationRetenuSourceView view)
	{
		_current = view;
		Binding();
		txtDate.Enabled = view.No == 0;
		txtExercice.Enabled = view.No == 0;
		txtPeriodeDebut.Enabled = view.No == 0;
		txtPeriodeFin.Enabled = view.No == 0;
		txtMois.Enabled = view.No == 0;
		txtLibelle.Enabled = view.Statut == StatutDeclaration.EnCours;
		lcgLignes.CustomHeaderButtons[0].Properties.Enabled = view.No != 0 && view.Statut == StatutDeclaration.EnCours;
		lcgLignes.CustomHeaderButtons[1].Properties.Enabled = view.No != 0 && view.Statut == StatutDeclaration.EnCours;
		btnValider.Enabled = view.Statut == StatutDeclaration.EnCours;
		_colRetirerLigne.Visible = view.Statut == StatutDeclaration.EnCours;
		btnCloturer.Caption = ((view.No != 0 && view.Statut == StatutDeclaration.Cloture) ? "Déclôturer" : "Clôturer");
		btnCloturer.Enabled = view.No != 0 && !view.IsDepose && !view.IsFichierGenerer;
		btnGenererFichier.Caption = ((view.No != 0 && view.IsFichierGenerer) ? "Annuler génération fic." : "Générer fichier");
		btnGenererFichier.Enabled = view.No != 0 && view.Statut == StatutDeclaration.Cloture && !view.IsDepose;
		btnDeposer.Enabled = view.Statut == StatutDeclaration.Cloture && view.IsFichierGenerer && !view.IsDepose;
		btnImporter.Enabled = view.No != 0 && view.Statut == StatutDeclaration.EnCours;
		if (view.No != 0)
		{
			RefrechData();
		}
	}

	private void RefrechData()
	{
		gcLignes.DataSource = _controller.GetLignesDeclaration(_current);
		gcGroupement.DataSource = _controller.GetRecapDeclaration(_current);
	}

	public async void ShowFicheTiers()
	{
		IEnumerable<LigneDeclarationRetenueMarocView> source = await gvLignes.GetSelectedObjectsAsync<LigneDeclarationRetenueMarocView>();
		if (!source.Any() || source.Count() > 1)
		{
			return;
		}
		LigneDeclarationRetenueMarocView ligneDeclarationRetenueMarocView = source.FirstOrDefault();
		if (ligneDeclarationRetenueMarocView == null)
		{
			return;
		}
		try
		{
			handleValider = SplashScreenManager.ShowOverlayForm(this);
			FrmFicheTiers frmFicheTiers = _formFactory.Create<FrmFicheTiers>();
			frmFicheTiers.SetTiers(ligneDeclarationRetenueMarocView.TiersNo);
			SplashScreenManager.CloseOverlayForm(handleValider);
			frmFicheTiers.ShowDialog();
		}
		catch (Exception ex)
		{
			if (handleValider != null)
			{
				SplashScreenManager.CloseOverlayForm(handleValider);
			}
			Log.Error(ex, ex.Message);
			XtraMessageBox.Show(ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		finally
		{
			if (handleValider != null)
			{
				SplashScreenManager.CloseOverlayForm(handleValider);
			}
		}
	}

	public async void ShowDossier()
	{
		IEnumerable<LigneDeclarationRetenueMarocView> source = await gvLignes.GetSelectedObjectsAsync<LigneDeclarationRetenueMarocView>();
		if (!source.Any() || source.Count() > 1)
		{
			return;
		}
		LigneDeclarationRetenueMarocView ligneDeclarationRetenueMarocView = source.FirstOrDefault();
		if (ligneDeclarationRetenueMarocView != null)
		{
			if (_controller.GetDossierView(ligneDeclarationRetenueMarocView.DossierReglementNo) == null)
			{
				throw new ApplicationException($"Impossible de  charger le dossier n°[{ligneDeclarationRetenueMarocView.DossierReglementNo}]");
			}
			((IParentFormDossierReglementFournisseur)base.MdiParent).OpenFrmListDossierReglementFrsView(ligneDeclarationRetenueMarocView.DossierReglementNo);
		}
	}

	private void Binding()
	{
		_current = _current ?? _controller.InitView();
		txtNumero.DataBindings.Clear();
		txtNumero.DataBindings.Add("EditValue", _current, "Numero", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtDate.DataBindings.Clear();
		txtDate.DataBindings.Add("EditValue", _current, "Date", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtExercice.DataBindings.Clear();
		txtExercice.DataBindings.Add("EditValue", _current, "Exercice", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtPeriodeDebut.DataBindings.Clear();
		txtPeriodeDebut.DataBindings.Add("EditValue", _current, "DateDebut", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtPeriodeFin.DataBindings.Clear();
		txtPeriodeFin.DataBindings.Add("EditValue", _current, "DateFin", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtStatut.DataBindings.Clear();
		txtStatut.DataBindings.Add("EditValue", _current, "Statut", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtLibelle.DataBindings.Clear();
		txtLibelle.DataBindings.Add("EditValue", _current, "Libelle", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtIsDepose.DataBindings.Clear();
		txtIsDepose.DataBindings.Add("EditValue", _current, "IsDepose", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtIsFichierGenere.DataBindings.Clear();
		txtIsFichierGenere.DataBindings.Add("EditValue", _current, "IsFichierGenerer", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtMois.DataBindings.Clear();
		txtMois.DataBindings.Add("EditValue", _current, "MoisPeriode", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtIsComptabilise.DataBindings.Clear();
		txtIsComptabilise.DataBindings.Add("EditValue", _current, "IsComptabilise", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
	}

	private void EnterEvent(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Return)
		{
			Valider(null, null);
		}
	}

	private void GroupLigneButtonClick(object sender, BaseButtonEventArgs e)
	{
		try
		{
			handleValider = SplashScreenManager.ShowOverlayForm(this);
			if (e.Button.Properties.VisibleIndex == 1)
			{
				FrmSelectLigneDeclarationRetenuSource frmSelectLigneDeclarationRetenuSource = _formFactory.Create<FrmSelectLigneDeclarationRetenuSource>();
				frmSelectLigneDeclarationRetenuSource.SetDeclaration(_current);
				if (frmSelectLigneDeclarationRetenuSource.ShowDialog() == DialogResult.OK)
				{
					IEnumerable<LigneDeclarationRetenueMarocView> selectedRetenueSource = frmSelectLigneDeclarationRetenuSource.SelectedRetenueSource;
					_controller.AddLignesDeclaration(_current, selectedRetenueSource);
					RefrechData();
				}
			}
			else if (e.Button.Properties.VisibleIndex == 2)
			{
				DeleteAll();
			}
		}
		catch (Exception ex)
		{
			XtraMessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		finally
		{
			if (handleValider != null)
			{
				SplashScreenManager.CloseOverlayForm(handleValider);
			}
		}
	}

	private void DeleteLigne(object sender, ButtonPressedEventArgs e)
	{
		int focusedRowHandle = gvLignes.FocusedRowHandle;
		if (gvLignes.IsNewItemRow(focusedRowHandle) || !(gvLignes.GetFocusedRow() is LigneDeclarationRetenueMarocView ligneDeclarationRetenueMarocView) || XtraMessageBox.Show("Voulez vous retirer la retenue [" + ligneDeclarationRetenueMarocView.RetenueNumero + "]?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		try
		{
			_controller.DeleteLigneDeclaration(ligneDeclarationRetenueMarocView);
			RefrechData();
		}
		catch (Exception ex)
		{
			Log.Error(ex, ex.Message);
			XtraMessageBox.Show(ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void GridViewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Delete)
		{
			DeleteAll();
		}
	}

	private async void DeleteAll()
	{
		if (_current.Statut != StatutDeclaration.EnCours)
		{
			return;
		}
		List<LigneDeclarationRetenueMarocView> list = (await gvLignes.GetSelectedObjectsAsync<LigneDeclarationRetenueMarocView>()).ToList();
		if (!list.Any() || XtraMessageBox.Show("Voulez-vous supprimer les lignes sélectionnées ?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		try
		{
			foreach (LigneDeclarationRetenueMarocView item in list)
			{
				_controller.DeleteLigneDeclaration(item);
			}
			RefrechData();
		}
		catch (Exception ex)
		{
			Log.Error(ex, ex.Message);
			XtraMessageBox.Show(ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void Cloturer(object sender, EventArgs e)
	{
		try
		{
			if (_current.Statut == StatutDeclaration.EnCours)
			{
				if (XtraMessageBox.Show("Voulez vous clôturer la déclaration n° [" + _current.Numero + "]?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				{
					return;
				}
				_controller.ClotureDeclaration(_current);
			}
			else
			{
				if (XtraMessageBox.Show("Voulez vous déclôturer la déclaration n° [" + _current.Numero + "]?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				{
					return;
				}
				_controller.AnnulerClotureDeclaration(_current);
			}
			Inititalize(_current);
			No = _current.No;
		}
		catch (Exception ex)
		{
			Log.Error(ex, ex.Message);
			XtraMessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void Deposer(object sender, EventArgs e)
	{
		try
		{
			if (XtraMessageBox.Show("Voulez vous déposer la déclaration n° [" + _current.Numero + "]?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				_controller.DeposeDeclaration(_current);
				Inititalize(_current);
				No = _current.No;
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, ex.Message);
			XtraMessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private bool VerifError()
	{
		for (int i = 0; i < gvLignes.RowCount; i++)
		{
			gvLignes.ClearSelection();
			gvLignes.FocusedRowHandle = i;
			gvLignes.SelectRow(i);
			if (gvLignes.HasColumnErrors)
			{
				return false;
			}
		}
		return true;
	}

	private void GenererFichier(object sender, EventArgs e)
	{
		try
		{
			if (!VerifError() || XtraMessageBox.Show(_current.IsFichierGenerer ? ("Voulez vous annuler la génération du fichier de la déclaration n° [" + _current.Numero + "]?") : ("Voulez vous générer le fichier de la déclaration n° [" + _current.Numero + "]?"), Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				return;
			}
			if (_current.IsFichierGenerer)
			{
				_controller.AnnulerGenerationFichierDeclaration(_current);
			}
			else
			{
				FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
				if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				OverlayWindowCompositePainter overlayWindowCompositePainter = new OverlayWindowCompositePainter(overlayLabel, overlayButton);
				IOverlayWindowPainter customPainter = overlayWindowCompositePainter;
				handleValider = SplashScreenManager.ShowOverlayForm(this, null, null, null, null, null, null, customPainter);
				Progress<int> progress = new Progress<int>(delegate(int x)
				{
					overlayLabel.Text = $"{x}%";
				});
				_controller.GenererFichierDeclaration(_current, folderBrowserDialog.SelectedPath, progress, default(CancellationToken));
			}
			Inititalize(_current);
			No = _current.No;
		}
		catch (Exception ex)
		{
			Log.Error(ex, ex.Message);
			XtraMessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
			Log.Error($"Déclaration RAS generate file: \n {ex}.");
		}
		finally
		{
			if (handleValider != null)
			{
				SplashScreenManager.CloseOverlayForm(handleValider);
			}
		}
	}

	private void Valider(object sender, EventArgs e)
	{
		try
		{
			if (CheckValue())
			{
				if (_current.No != 0)
				{
					_controller.UpdateDeclaration(_current);
					return;
				}
				if (!(txtExercice.GetSelectedDataRow() is IErpExercice erpExercice))
				{
					txtExercice.ErrorText = "Exercice obligatoire.";
					return;
				}
				if (!(txtMois.EditValue is DeclarationMoisPeriode month))
				{
					txtMois.ErrorText = "Mois obligatoire.";
					return;
				}
				DateTime dateTime = new DateTime(erpExercice.Annee, (int)month, 1);
				txtPeriodeDebut.EditValue = dateTime;
				txtPeriodeFin.EditValue = dateTime.AddMonths(1).AddSeconds(-1.0);
				_current.No = _controller.CreateDeclaration(_current);
				Inititalize(_current);
				No = _current.No;
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, ex.Message);
			XtraMessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private bool CheckValue()
	{
		txtExercice.ErrorText = "";
		txtPeriodeDebut.ErrorText = "";
		txtPeriodeFin.ErrorText = "";
		if (!(txtExercice.GetSelectedDataRow() is IErpExercice erpExercice))
		{
			txtExercice.ErrorText = "Exercice obligatoire.";
			return false;
		}
		if (txtPeriodeDebut.DateTime.Date < erpExercice.Debut.Date || txtPeriodeDebut.DateTime.Date > erpExercice.Fin.Date)
		{
			txtPeriodeDebut.ErrorText = "La date début doit être inclut dans la pérode de l'exercice.";
			return false;
		}
		if (txtPeriodeFin.DateTime.Date < erpExercice.Debut.Date || txtPeriodeFin.DateTime.Date > erpExercice.Fin.Date)
		{
			txtPeriodeFin.ErrorText = "La date fin doit être inclut dans la pérode de l'exercice.";
			return false;
		}
		if (txtPeriodeFin.DateTime.Date < txtPeriodeFin.DateTime.Date)
		{
			txtPeriodeFin.ErrorText = "La date fin doit être supérieur ou égale à la date début.";
			return false;
		}
		return true;
	}

	private void ExerciceChanged(object sender, EventArgs e)
	{
		if (!(txtExercice.GetSelectedDataRow() is IErpExercice erpExercice))
		{
			txtExercice.ErrorText = "Exercice obligatoire.";
			return;
		}
		txtPeriodeDebut.EditValue = erpExercice.Debut;
		txtPeriodeFin.EditValue = erpExercice.Fin;
	}

	public void DeclarationRetenuSourceChanged(DeclarationRetenuSourceChangedArgs e)
	{
		if (_current == null || !base.IsHandleCreated || (e.Action != TypeAction.Modification && e.Action != TypeAction.Ajout) || e.EntityNo != _current.No)
		{
			return;
		}
		DeclarationRetenuSourceView view = _controller.Get(e.EntityNo);
		if (view != null)
		{
			Action method = delegate
			{
				SetCurrentView(view);
			};
			BeginInvoke(method);
		}
	}

	public void Inititalize(DeclarationRetenuSourceView view)
	{
		if (view == null)
		{
			view = _controller.InitView();
		}
		SetCurrentView(view);
		Text = "Déclaration retenue à la source " + view.Numero;
		No = view.No;
	}

	public void ApplyModel(GridLayout layout)
	{
		if (layout != null)
		{
			MemoryStream stream = new MemoryStream(layout.Layout);
			gvLignes.RestoreLayoutFromStream(stream, GridLayoutOptionCustomization.LayoutVisualOption);
			AppliedLayoutNo = layout.No;
		}
	}

	private void SetDefaultGridLayout()
	{
		UtilisateurGrid utilisateurGrid = _layoutController.GetUtilisateurGrid(_gridGuid);
		if (utilisateurGrid != null)
		{
			GridLayout gridLayout = _layoutController.GetGridLayout(utilisateurGrid.GridLayoutNo);
			ApplyModel(gridLayout);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.components = new System.ComponentModel.Container();
		DevExpress.XtraEditors.ButtonsPanelControl.ButtonImageOptions imageOptions = new DevExpress.XtraEditors.ButtonsPanelControl.ButtonImageOptions();
		DevExpress.XtraEditors.ButtonsPanelControl.ButtonImageOptions imageOptions2 = new DevExpress.XtraEditors.ButtonsPanelControl.ButtonImageOptions();
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Tresorerie.UIDeclarationTva.DeclarationRS.FrmFicheDeclarationRetenuSource));
		this.ribbon = new DevExpress.XtraBars.Ribbon.RibbonControl();
		this.btnCloturer = new DevExpress.XtraBars.BarButtonItem();
		this.btnGenererFichier = new DevExpress.XtraBars.BarButtonItem();
		this.btnDeposer = new DevExpress.XtraBars.BarButtonItem();
		this.btnImporter = new DevExpress.XtraBars.BarButtonItem();
		this.btnExporterModelCsv = new DevExpress.XtraBars.BarButtonItem();
		this.ribbonPage1 = new DevExpress.XtraBars.Ribbon.RibbonPage();
		this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
		this.dropDownButton1 = new DevExpress.XtraEditors.DropDownButton();
		this.popupMenuActions = new DevExpress.XtraBars.PopupMenu(this.components);
		this.txtIsComptabilise = new DevExpress.XtraEditors.CheckEdit();
		this.gcGroupement = new DevExpress.XtraGrid.GridControl();
		this.gvGroupement = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtIsFichierGenere = new DevExpress.XtraEditors.CheckEdit();
		this.txtNumero = new DevExpress.XtraEditors.TextEdit();
		this.btnValider = new DevExpress.XtraEditors.SimpleButton();
		this.gcLignes = new DevExpress.XtraGrid.GridControl();
		this.gvLignes = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtLibelle = new DevExpress.XtraEditors.TextEdit();
		this.txtIsDepose = new DevExpress.XtraEditors.CheckEdit();
		this.txtDate = new DevExpress.XtraEditors.DateEdit();
		this.txtStatut = new DevExpress.XtraEditors.ImageComboBoxEdit();
		this.txtExercice = new DevExpress.XtraEditors.GridLookUpEdit();
		this.gridLookUpEdit1View = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtPeriodeDebut = new DevExpress.XtraEditors.DateEdit();
		this.txtPeriodeFin = new DevExpress.XtraEditors.DateEdit();
		this.txtMois = new DevExpress.XtraEditors.ImageComboBoxEdit();
		this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem12 = new DevExpress.XtraLayout.LayoutControlItem();
		this.lcgLignes = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem11 = new DevExpress.XtraLayout.LayoutControlItem();
		this.emptySpaceItem3 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.layoutControlGroup1 = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem3 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem8 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem15 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem2 = new DevExpress.XtraLayout.LayoutControlItem();
		this.lciTxtMois = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem5 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem6 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem17 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem7 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem18 = new DevExpress.XtraLayout.LayoutControlItem();
		this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.emptySpaceItem2 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.layoutControlGroup3 = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem10 = new DevExpress.XtraLayout.LayoutControlItem();
		this.btnAction = new DevExpress.XtraLayout.LayoutControlItem();
		this.splitterItem1 = new DevExpress.XtraLayout.SplitterItem();
		this.imageCollection = new DevExpress.Utils.ImageCollection(this.components);
		((System.ComponentModel.ISupportInitialize)this.ribbon).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.popupMenuActions).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsComptabilise.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gcGroupement).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gvGroupement).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsFichierGenere.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtNumero.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gcLignes).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gvLignes).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtLibelle.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsDepose.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtStatut.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtExercice.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gridLookUpEdit1View).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeDebut.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeDebut.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeFin.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeFin.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtMois.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.Root).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem12).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.lcgLignes).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem11).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem3).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlGroup1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem8).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem15).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.lciTxtMois).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem6).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem17).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem7).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem18).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlGroup3).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem10).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.btnAction).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.splitterItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.imageCollection).BeginInit();
		base.SuspendLayout();
		this.ribbon.ExpandCollapseItem.Id = 0;
		this.ribbon.Items.AddRange(new DevExpress.XtraBars.BarItem[7]
		{
			this.ribbon.ExpandCollapseItem,
			this.ribbon.SearchEditItem,
			this.btnCloturer,
			this.btnGenererFichier,
			this.btnDeposer,
			this.btnImporter,
			this.btnExporterModelCsv
		});
		this.ribbon.Location = new System.Drawing.Point(0, 0);
		this.ribbon.MaxItemId = 6;
		this.ribbon.Name = "ribbon";
		this.ribbon.Pages.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPage[1] { this.ribbonPage1 });
		this.ribbon.Size = new System.Drawing.Size(1364, 158);
		this.btnCloturer.Caption = "Clôturer";
		this.btnCloturer.Id = 1;
		this.btnCloturer.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.menu_new_deblocage;
		this.btnCloturer.Name = "btnCloturer";
		this.btnGenererFichier.Caption = "Générer";
		this.btnGenererFichier.Id = 2;
		this.btnGenererFichier.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.menu_new_generer_liasse;
		this.btnGenererFichier.Name = "btnGenererFichier";
		this.btnDeposer.Caption = "Déposer";
		this.btnDeposer.Id = 3;
		this.btnDeposer.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.menu_new_site;
		this.btnDeposer.Name = "btnDeposer";
		this.btnImporter.Caption = "Importer";
		this.btnImporter.Id = 4;
		this.btnImporter.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_download_2_32;
		this.btnImporter.Name = "btnImporter";
		this.btnExporterModelCsv.Caption = "Exporter Model CSV";
		this.btnExporterModelCsv.Id = 5;
		this.btnExporterModelCsv.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttocsv_32x32;
		this.btnExporterModelCsv.Name = "btnExporterModelCsv";
		this.ribbonPage1.Name = "ribbonPage1";
		this.ribbonPage1.Text = "Déclaratif";
		this.layoutControl1.Controls.Add(this.dropDownButton1);
		this.layoutControl1.Controls.Add(this.txtIsComptabilise);
		this.layoutControl1.Controls.Add(this.gcGroupement);
		this.layoutControl1.Controls.Add(this.txtIsFichierGenere);
		this.layoutControl1.Controls.Add(this.txtNumero);
		this.layoutControl1.Controls.Add(this.btnValider);
		this.layoutControl1.Controls.Add(this.gcLignes);
		this.layoutControl1.Controls.Add(this.txtLibelle);
		this.layoutControl1.Controls.Add(this.txtIsDepose);
		this.layoutControl1.Controls.Add(this.txtDate);
		this.layoutControl1.Controls.Add(this.txtStatut);
		this.layoutControl1.Controls.Add(this.txtExercice);
		this.layoutControl1.Controls.Add(this.txtPeriodeDebut);
		this.layoutControl1.Controls.Add(this.txtPeriodeFin);
		this.layoutControl1.Controls.Add(this.txtMois);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 158);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.OptionsCustomizationForm.DesignTimeCustomizationFormPositionAndSize = new System.Drawing.Rectangle(2121, 188, 650, 760);
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(1364, 604);
		this.layoutControl1.TabIndex = 1;
		this.layoutControl1.Text = "layoutControl1";
		this.dropDownButton1.DropDownArrowStyle = DevExpress.XtraEditors.DropDownArrowStyle.Show;
		this.dropDownButton1.DropDownControl = this.popupMenuActions;
		this.dropDownButton1.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_consulter_32;
		this.dropDownButton1.Location = new System.Drawing.Point(1246, 2);
		this.dropDownButton1.MenuManager = this.ribbon;
		this.dropDownButton1.Name = "dropDownButton1";
		this.dropDownButton1.Size = new System.Drawing.Size(116, 36);
		this.dropDownButton1.StyleController = this.layoutControl1;
		this.dropDownButton1.TabIndex = 26;
		this.dropDownButton1.Text = "Actions";
		this.popupMenuActions.ItemLinks.Add(this.btnCloturer);
		this.popupMenuActions.ItemLinks.Add(this.btnGenererFichier);
		this.popupMenuActions.ItemLinks.Add(this.btnDeposer);
		this.popupMenuActions.ItemLinks.Add(this.btnExporterModelCsv);
		this.popupMenuActions.Name = "popupMenuActions";
		this.popupMenuActions.Ribbon = this.ribbon;
		this.txtIsComptabilise.Location = new System.Drawing.Point(190, 149);
		this.txtIsComptabilise.MenuManager = this.ribbon;
		this.txtIsComptabilise.Name = "txtIsComptabilise";
		this.txtIsComptabilise.Properties.Caption = "Comptabilisée";
		this.txtIsComptabilise.Properties.ReadOnly = true;
		this.txtIsComptabilise.Size = new System.Drawing.Size(89, 20);
		this.txtIsComptabilise.StyleController = this.layoutControl1;
		this.txtIsComptabilise.TabIndex = 25;
		this.gcGroupement.Location = new System.Drawing.Point(8, 197);
		this.gcGroupement.MainView = this.gvGroupement;
		this.gcGroupement.MenuManager = this.ribbon;
		this.gcGroupement.Name = "gcGroupement";
		this.gcGroupement.Size = new System.Drawing.Size(390, 399);
		this.gcGroupement.TabIndex = 23;
		this.gcGroupement.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gvGroupement });
		this.gvGroupement.GridControl = this.gcGroupement;
		this.gvGroupement.Name = "gvGroupement";
		this.txtIsFichierGenere.Location = new System.Drawing.Point(5, 149);
		this.txtIsFichierGenere.MenuManager = this.ribbon;
		this.txtIsFichierGenere.Name = "txtIsFichierGenere";
		this.txtIsFichierGenere.Properties.Caption = "Fichier généré";
		this.txtIsFichierGenere.Properties.ReadOnly = true;
		this.txtIsFichierGenere.Size = new System.Drawing.Size(97, 20);
		this.txtIsFichierGenere.StyleController = this.layoutControl1;
		this.txtIsFichierGenere.TabIndex = 20;
		this.txtNumero.Enabled = false;
		this.txtNumero.Location = new System.Drawing.Point(80, 26);
		this.txtNumero.MenuManager = this.ribbon;
		this.txtNumero.Name = "txtNumero";
		this.txtNumero.Size = new System.Drawing.Size(121, 20);
		this.txtNumero.StyleController = this.layoutControl1;
		this.txtNumero.TabIndex = 18;
		this.btnValider.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.menu_save_32;
		this.btnValider.Location = new System.Drawing.Point(1126, 2);
		this.btnValider.Name = "btnValider";
		this.btnValider.Size = new System.Drawing.Size(116, 36);
		this.btnValider.StyleController = this.layoutControl1;
		this.btnValider.TabIndex = 15;
		this.btnValider.Text = "Valider";
		this.gcLignes.Location = new System.Drawing.Point(421, 66);
		this.gcLignes.MainView = this.gvLignes;
		this.gcLignes.MenuManager = this.ribbon;
		this.gcLignes.Name = "gcLignes";
		this.gcLignes.Size = new System.Drawing.Size(938, 533);
		this.gcLignes.TabIndex = 14;
		this.gcLignes.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gvLignes });
		this.gvLignes.GridControl = this.gcLignes;
		this.gvLignes.Name = "gvLignes";
		this.txtLibelle.Location = new System.Drawing.Point(80, 125);
		this.txtLibelle.MenuManager = this.ribbon;
		this.txtLibelle.Name = "txtLibelle";
		this.txtLibelle.Size = new System.Drawing.Size(321, 20);
		this.txtLibelle.StyleController = this.layoutControl1;
		this.txtLibelle.TabIndex = 11;
		this.txtIsDepose.Location = new System.Drawing.Point(106, 149);
		this.txtIsDepose.MenuManager = this.ribbon;
		this.txtIsDepose.Name = "txtIsDepose";
		this.txtIsDepose.Properties.Caption = "Déposée";
		this.txtIsDepose.Properties.ReadOnly = true;
		this.txtIsDepose.Size = new System.Drawing.Size(80, 20);
		this.txtIsDepose.StyleController = this.layoutControl1;
		this.txtIsDepose.TabIndex = 10;
		this.txtDate.EditValue = null;
		this.txtDate.Location = new System.Drawing.Point(80, 50);
		this.txtDate.MenuManager = this.ribbon;
		this.txtDate.Name = "txtDate";
		this.txtDate.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDate.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDate.Properties.Mask.EditMask = "ddMMyy";
		this.txtDate.Properties.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.DateTimeAdvancingCaret;
		this.txtDate.Size = new System.Drawing.Size(121, 20);
		this.txtDate.StyleController = this.layoutControl1;
		this.txtDate.TabIndex = 4;
		this.txtStatut.Location = new System.Drawing.Point(280, 26);
		this.txtStatut.MenuManager = this.ribbon;
		this.txtStatut.Name = "txtStatut";
		this.txtStatut.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtStatut.Properties.ReadOnly = true;
		this.txtStatut.Size = new System.Drawing.Size(121, 20);
		this.txtStatut.StyleController = this.layoutControl1;
		this.txtStatut.TabIndex = 5;
		this.txtExercice.Location = new System.Drawing.Point(80, 75);
		this.txtExercice.MenuManager = this.ribbon;
		this.txtExercice.Name = "txtExercice";
		this.txtExercice.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtExercice.Properties.NullText = "";
		this.txtExercice.Properties.PopupView = this.gridLookUpEdit1View;
		this.txtExercice.Size = new System.Drawing.Size(121, 20);
		this.txtExercice.StyleController = this.layoutControl1;
		this.txtExercice.TabIndex = 6;
		this.gridLookUpEdit1View.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
		this.gridLookUpEdit1View.Name = "gridLookUpEdit1View";
		this.gridLookUpEdit1View.OptionsSelection.EnableAppearanceFocusedCell = false;
		this.gridLookUpEdit1View.OptionsView.ShowGroupPanel = false;
		this.txtPeriodeDebut.EditValue = null;
		this.txtPeriodeDebut.Location = new System.Drawing.Point(80, 100);
		this.txtPeriodeDebut.MenuManager = this.ribbon;
		this.txtPeriodeDebut.Name = "txtPeriodeDebut";
		this.txtPeriodeDebut.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtPeriodeDebut.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtPeriodeDebut.Properties.Mask.EditMask = "ddMMyy";
		this.txtPeriodeDebut.Properties.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.DateTimeAdvancingCaret;
		this.txtPeriodeDebut.Properties.ReadOnly = true;
		this.txtPeriodeDebut.Size = new System.Drawing.Size(121, 20);
		this.txtPeriodeDebut.StyleController = this.layoutControl1;
		this.txtPeriodeDebut.TabIndex = 8;
		this.txtPeriodeFin.EditValue = null;
		this.txtPeriodeFin.Location = new System.Drawing.Point(280, 100);
		this.txtPeriodeFin.MenuManager = this.ribbon;
		this.txtPeriodeFin.Name = "txtPeriodeFin";
		this.txtPeriodeFin.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtPeriodeFin.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtPeriodeFin.Properties.Mask.EditMask = "ddMMyy";
		this.txtPeriodeFin.Properties.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.DateTimeAdvancingCaret;
		this.txtPeriodeFin.Properties.ReadOnly = true;
		this.txtPeriodeFin.Size = new System.Drawing.Size(121, 20);
		this.txtPeriodeFin.StyleController = this.layoutControl1;
		this.txtPeriodeFin.TabIndex = 9;
		this.txtMois.Location = new System.Drawing.Point(280, 51);
		this.txtMois.MenuManager = this.ribbon;
		this.txtMois.Name = "txtMois";
		this.txtMois.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtMois.Size = new System.Drawing.Size(121, 20);
		this.txtMois.StyleController = this.layoutControl1;
		this.txtMois.TabIndex = 21;
		this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
		this.Root.GroupBordersVisible = false;
		this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[6] { this.layoutControlItem12, this.lcgLignes, this.emptySpaceItem3, this.layoutControlGroup1, this.btnAction, this.splitterItem1 });
		this.Root.Name = "Root";
		this.Root.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.Root.Size = new System.Drawing.Size(1364, 604);
		this.Root.TextVisible = false;
		this.layoutControlItem12.Control = this.btnValider;
		this.layoutControlItem12.Location = new System.Drawing.Point(1124, 0);
		this.layoutControlItem12.MaxSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem12.MinSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem12.Name = "layoutControlItem12";
		this.layoutControlItem12.Size = new System.Drawing.Size(120, 40);
		this.layoutControlItem12.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem12.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem12.TextVisible = false;
		this.lcgLignes.CustomHeaderButtons.AddRange(new DevExpress.XtraEditors.ButtonPanel.IBaseButton[2]
		{
			new DevExpress.XtraEditors.ButtonsPanelControl.GroupBoxButton("Intégrer ligne", true, imageOptions, DevExpress.XtraBars.Docking2010.ButtonStyle.PushButton, "", 1, true, null, true, false, true, null, 1),
			new DevExpress.XtraEditors.ButtonsPanelControl.GroupBoxButton("Supprimer", true, imageOptions2, DevExpress.XtraBars.Docking2010.ButtonStyle.PushButton, "", 2, true, null, true, false, true, null, 2)
		});
		this.lcgLignes.HeaderButtonsLocation = DevExpress.Utils.GroupElementLocation.AfterText;
		this.lcgLignes.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[1] { this.layoutControlItem11 });
		this.lcgLignes.Location = new System.Drawing.Point(416, 40);
		this.lcgLignes.Name = "lcgLignes";
		this.lcgLignes.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.lcgLignes.Size = new System.Drawing.Size(948, 564);
		this.lcgLignes.Text = "Lignes";
		this.layoutControlItem11.Control = this.gcLignes;
		this.layoutControlItem11.Location = new System.Drawing.Point(0, 0);
		this.layoutControlItem11.Name = "layoutControlItem11";
		this.layoutControlItem11.Size = new System.Drawing.Size(942, 537);
		this.layoutControlItem11.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem11.TextVisible = false;
		this.emptySpaceItem3.AllowHotTrack = false;
		this.emptySpaceItem3.Location = new System.Drawing.Point(416, 0);
		this.emptySpaceItem3.Name = "emptySpaceItem3";
		this.emptySpaceItem3.Size = new System.Drawing.Size(708, 40);
		this.emptySpaceItem3.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlGroup1.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[14]
		{
			this.layoutControlItem1, this.layoutControlItem3, this.layoutControlItem8, this.layoutControlItem15, this.layoutControlItem2, this.lciTxtMois, this.layoutControlItem5, this.layoutControlItem6, this.layoutControlItem17, this.layoutControlItem7,
			this.layoutControlItem18, this.emptySpaceItem1, this.emptySpaceItem2, this.layoutControlGroup3
		});
		this.layoutControlGroup1.Location = new System.Drawing.Point(0, 0);
		this.layoutControlGroup1.Name = "layoutControlGroup1";
		this.layoutControlGroup1.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.layoutControlGroup1.Size = new System.Drawing.Size(406, 604);
		this.layoutControlGroup1.Text = "Entête";
		this.layoutControlItem1.Control = this.txtDate;
		this.layoutControlItem1.Location = new System.Drawing.Point(0, 24);
		this.layoutControlItem1.MaxSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem1.MinSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem1.Name = "layoutControlItem1";
		this.layoutControlItem1.Size = new System.Drawing.Size(200, 25);
		this.layoutControlItem1.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem1.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem1.Text = "Date";
		this.layoutControlItem1.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem1.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem1.TextToControlDistance = 0;
		this.layoutControlItem3.Control = this.txtExercice;
		this.layoutControlItem3.Location = new System.Drawing.Point(0, 49);
		this.layoutControlItem3.MaxSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem3.MinSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem3.Name = "layoutControlItem3";
		this.layoutControlItem3.Size = new System.Drawing.Size(200, 25);
		this.layoutControlItem3.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem3.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem3.Text = "Exercice";
		this.layoutControlItem3.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem3.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem3.TextToControlDistance = 0;
		this.layoutControlItem8.Control = this.txtLibelle;
		this.layoutControlItem8.Location = new System.Drawing.Point(0, 99);
		this.layoutControlItem8.MaxSize = new System.Drawing.Size(400, 24);
		this.layoutControlItem8.MinSize = new System.Drawing.Size(400, 24);
		this.layoutControlItem8.Name = "layoutControlItem8";
		this.layoutControlItem8.Size = new System.Drawing.Size(400, 24);
		this.layoutControlItem8.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem8.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem8.Text = "Libellé";
		this.layoutControlItem8.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem8.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem8.TextToControlDistance = 0;
		this.layoutControlItem15.Control = this.txtNumero;
		this.layoutControlItem15.Location = new System.Drawing.Point(0, 0);
		this.layoutControlItem15.Name = "layoutControlItem15";
		this.layoutControlItem15.Size = new System.Drawing.Size(200, 24);
		this.layoutControlItem15.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem15.Text = "Numéro";
		this.layoutControlItem15.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem15.TextSize = new System.Drawing.Size(70, 13);
		this.layoutControlItem15.TextToControlDistance = 0;
		this.layoutControlItem2.Control = this.txtStatut;
		this.layoutControlItem2.Location = new System.Drawing.Point(200, 0);
		this.layoutControlItem2.MaxSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem2.MinSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem2.Name = "layoutControlItem2";
		this.layoutControlItem2.Size = new System.Drawing.Size(200, 25);
		this.layoutControlItem2.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem2.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem2.Text = "Statut";
		this.layoutControlItem2.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem2.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem2.TextToControlDistance = 0;
		this.lciTxtMois.Control = this.txtMois;
		this.lciTxtMois.Location = new System.Drawing.Point(200, 25);
		this.lciTxtMois.MaxSize = new System.Drawing.Size(200, 25);
		this.lciTxtMois.MinSize = new System.Drawing.Size(200, 25);
		this.lciTxtMois.Name = "lciTxtMois";
		this.lciTxtMois.Size = new System.Drawing.Size(200, 25);
		this.lciTxtMois.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.lciTxtMois.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.lciTxtMois.Text = "Mois";
		this.lciTxtMois.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.lciTxtMois.TextSize = new System.Drawing.Size(70, 20);
		this.lciTxtMois.TextToControlDistance = 0;
		this.layoutControlItem5.Control = this.txtPeriodeDebut;
		this.layoutControlItem5.Location = new System.Drawing.Point(0, 74);
		this.layoutControlItem5.MaxSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem5.MinSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem5.Name = "layoutControlItem5";
		this.layoutControlItem5.Size = new System.Drawing.Size(200, 25);
		this.layoutControlItem5.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem5.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem5.Text = "Période du";
		this.layoutControlItem5.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem5.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem5.TextToControlDistance = 0;
		this.layoutControlItem6.Control = this.txtPeriodeFin;
		this.layoutControlItem6.Location = new System.Drawing.Point(200, 74);
		this.layoutControlItem6.MaxSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem6.MinSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem6.Name = "layoutControlItem6";
		this.layoutControlItem6.Size = new System.Drawing.Size(200, 25);
		this.layoutControlItem6.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem6.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem6.Text = "Au";
		this.layoutControlItem6.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem6.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem6.TextToControlDistance = 0;
		this.layoutControlItem17.Control = this.txtIsFichierGenere;
		this.layoutControlItem17.Location = new System.Drawing.Point(0, 123);
		this.layoutControlItem17.MaxSize = new System.Drawing.Size(101, 24);
		this.layoutControlItem17.MinSize = new System.Drawing.Size(101, 24);
		this.layoutControlItem17.Name = "layoutControlItem17";
		this.layoutControlItem17.Size = new System.Drawing.Size(101, 24);
		this.layoutControlItem17.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem17.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem17.TextVisible = false;
		this.layoutControlItem7.Control = this.txtIsDepose;
		this.layoutControlItem7.Location = new System.Drawing.Point(101, 123);
		this.layoutControlItem7.MaxSize = new System.Drawing.Size(84, 24);
		this.layoutControlItem7.MinSize = new System.Drawing.Size(84, 24);
		this.layoutControlItem7.Name = "layoutControlItem7";
		this.layoutControlItem7.Size = new System.Drawing.Size(84, 24);
		this.layoutControlItem7.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem7.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem7.TextVisible = false;
		this.layoutControlItem18.Control = this.txtIsComptabilise;
		this.layoutControlItem18.Location = new System.Drawing.Point(185, 123);
		this.layoutControlItem18.Name = "layoutControlItem18";
		this.layoutControlItem18.Size = new System.Drawing.Size(93, 24);
		this.layoutControlItem18.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem18.TextVisible = false;
		this.layoutControlItem18.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;
		this.emptySpaceItem1.AllowHotTrack = false;
		this.emptySpaceItem1.Location = new System.Drawing.Point(278, 123);
		this.emptySpaceItem1.Name = "emptySpaceItem1";
		this.emptySpaceItem1.Size = new System.Drawing.Size(122, 24);
		this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
		this.emptySpaceItem2.AllowHotTrack = false;
		this.emptySpaceItem2.Location = new System.Drawing.Point(200, 50);
		this.emptySpaceItem2.Name = "emptySpaceItem2";
		this.emptySpaceItem2.Size = new System.Drawing.Size(200, 24);
		this.emptySpaceItem2.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlGroup3.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[1] { this.layoutControlItem10 });
		this.layoutControlGroup3.Location = new System.Drawing.Point(0, 147);
		this.layoutControlGroup3.Name = "layoutControlGroup3";
		this.layoutControlGroup3.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.layoutControlGroup3.Size = new System.Drawing.Size(400, 430);
		this.layoutControlGroup3.Text = "Récap déclaration";
		this.layoutControlItem10.Control = this.gcGroupement;
		this.layoutControlItem10.Location = new System.Drawing.Point(0, 0);
		this.layoutControlItem10.Name = "layoutControlItem10";
		this.layoutControlItem10.Size = new System.Drawing.Size(394, 403);
		this.layoutControlItem10.Text = "Récap déclaration";
		this.layoutControlItem10.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem10.TextVisible = false;
		this.btnAction.Control = this.dropDownButton1;
		this.btnAction.Location = new System.Drawing.Point(1244, 0);
		this.btnAction.MaxSize = new System.Drawing.Size(120, 40);
		this.btnAction.MinSize = new System.Drawing.Size(120, 40);
		this.btnAction.Name = "btnAction";
		this.btnAction.Size = new System.Drawing.Size(120, 40);
		this.btnAction.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.btnAction.TextSize = new System.Drawing.Size(0, 0);
		this.btnAction.TextVisible = false;
		this.splitterItem1.AllowHotTrack = true;
		this.splitterItem1.IsCollapsible = DevExpress.Utils.DefaultBoolean.True;
		this.splitterItem1.Location = new System.Drawing.Point(406, 0);
		this.splitterItem1.Name = "splitterItem1";
		this.splitterItem1.Size = new System.Drawing.Size(10, 604);
		this.imageCollection.ImageStream = (DevExpress.Utils.ImageCollectionStreamer)resources.GetObject("imageCollection.ImageStream");
		this.imageCollection.Images.SetKeyName(0, "SensEntree.png");
		this.imageCollection.Images.SetKeyName(1, "SensSortie.png");
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(1364, 762);
		base.Controls.Add(this.layoutControl1);
		base.Controls.Add(this.ribbon);
		base.Name = "FrmFicheDeclarationRetenuSource";
		this.Ribbon = this.ribbon;
		this.Text = "FrmFicheDeclarationTvaEncaissement";
		((System.ComponentModel.ISupportInitialize)this.ribbon).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.popupMenuActions).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsComptabilise.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gcGroupement).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gvGroupement).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsFichierGenere.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtNumero.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gcLignes).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gvLignes).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtLibelle.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsDepose.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtStatut.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtExercice.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gridLookUpEdit1View).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeDebut.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeDebut.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeFin.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeFin.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtMois.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.Root).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem12).EndInit();
		((System.ComponentModel.ISupportInitialize)this.lcgLignes).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem11).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem3).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlGroup1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem8).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem15).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.lciTxtMois).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem6).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem17).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem7).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem18).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlGroup3).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem10).EndInit();
		((System.ComponentModel.ISupportInitialize)this.btnAction).EndInit();
		((System.ComponentModel.ISupportInitialize)this.splitterItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.imageCollection).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}

	private void Initialize()
	{
		InitControls();
		InitGridLignes();
		InitGridRecap();
	}

	private void InitControls()
	{
		GridColumn gridColumn = new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "Intitule",
			Visible = true
		};
		txtExercice.Properties.View.Columns.AddRange(new GridColumn[1] { gridColumn });
		txtExercice.Properties.DataSource = _controller.GetAllExercice();
		txtExercice.Properties.DisplayMember = "Annee";
		txtExercice.Properties.ValueMember = "Annee";
		txtStatut.Properties.Items.Add(new ImageComboBoxItem("En cours", StatutDeclaration.EnCours));
		txtStatut.Properties.Items.Add(new ImageComboBoxItem("Clôturé", StatutDeclaration.Cloture));
		txtMois.Properties.AddEnum<DeclarationMoisPeriode>();
	}

	private RepositoryItemGridLookUpEdit GetRepoTaxe()
	{
		return new RepositoryItemGridLookUpEdit
		{
			DataSource = _controller.GetAllErpTaxe(),
			DisplayMember = "Intitule",
			ValueMember = "Code",
			View = 
			{
				Columns = 
				{
					new GridColumn
					{
						Caption = "Code",
						FieldName = "Code",
						Visible = true
					},
					new GridColumn
					{
						Caption = "Intitulé",
						FieldName = "Intitule",
						Visible = true
					}
				}
			},
			NullText = string.Empty
		};
	}

	private void InitGridLignes()
	{
		if (_controller.GetDefaultDeviseSociete() == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		_repoDevise = new RepositoryItemGridLookUpEdit();
		_repoDevise.View.Columns.AddVisible("Designation", "Intitulé");
		_repoDevise.View.Columns.AddVisible("Sigle", "Sigle");
		_repoDevise.ValueMember = "No";
		_repoDevise.DisplayMember = "Sigle";
		_repoDevise.NullText = string.Empty;
		_repoDevise.DataSource = _controller.GetAllDevise();
		_repoMode = new RepositoryItemGridLookUpEdit();
		_repoMode.View.Columns.AddVisible("Code", "Code");
		_repoMode.View.Columns.AddVisible("Designation", "Intitulé");
		_repoMode.ValueMember = "No";
		_repoMode.DisplayMember = "Designation";
		_repoMode.NullText = string.Empty;
		_repoMode.DataSource = _controller.GetAllMode();
		_repoModeCode = new RepositoryItemGridLookUpEdit();
		_repoModeCode.View.Columns.AddVisible("Code", "Code");
		_repoModeCode.View.Columns.AddVisible("Designation", "Intitulé");
		_repoModeCode.ValueMember = "No";
		_repoModeCode.DisplayMember = "Code";
		_repoModeCode.NullText = string.Empty;
		_repoModeCode.DataSource = _controller.GetAllMode();
		_repoCaisse = new RepositoryItemGridLookUpEdit();
		_repoCaisse.View.Columns.AddVisible("Code", "Code");
		_repoCaisse.View.Columns.AddVisible("Designation", "Intitulé");
		_repoCaisse.ValueMember = "No";
		_repoCaisse.DisplayMember = "Designation";
		_repoCaisse.NullText = string.Empty;
		_repoCaisse.DataSource = _controller.GetAllCaisse();
		RepositoryItemImageComboBox repositoryItemImageComboBox = new RepositoryItemImageComboBox();
		repositoryItemImageComboBox.Items.Add(new ImageComboBoxItem("Erreur", true, 0));
		repositoryItemImageComboBox.SmallImages = new ImageCollection(allowModifyImages: false)
		{
			Images = { (Image)Resources.delete }
		};
		repositoryItemImageComboBox.GlyphAlignment = HorzAlignment.Far;
		repositoryItemImageComboBox.ShowToolTipForTrimmedText = DefaultBoolean.True;
		RepositoryItemButtonEdit repositoryItemButtonEdit = new RepositoryItemButtonEdit();
		repositoryItemButtonEdit.Buttons[0].Kind = ButtonPredefines.Glyph;
		repositoryItemButtonEdit.Buttons[0].Image = Resources.delete_16x16;
		repositoryItemButtonEdit.TextEditStyle = TextEditStyles.HideTextEditor;
		repositoryItemButtonEdit.Buttons[0].ToolTip = "Retirer ligne";
		repositoryItemButtonEdit.ButtonClick += DeleteLigne;
		RepositoryItemGridLookUpEdit repositoryItemGridLookUpEdit = new RepositoryItemGridLookUpEdit
		{
			DataSource = _controller.GetAllDesignationDocument(),
			ValueMember = "No",
			DisplayMember = "Intitule",
			NullText = ""
		};
		repositoryItemGridLookUpEdit.View.Columns.Add(new GridColumn
		{
			Caption = "Code",
			FieldName = "Code",
			Visible = true
		});
		repositoryItemGridLookUpEdit.View.Columns.Add(new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "Intitule",
			Visible = true
		});
		repositoryItemGridLookUpEdit.View.Columns.Add(new GridColumn
		{
			Caption = "Nature",
			FieldName = "NatureOperation",
			Visible = true
		});
		RepositoryItemImageComboBox repositoryItemImageComboBox2 = new RepositoryItemImageComboBox();
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Non comptabilisé", EtatComptabilite.NonComptabilise));
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Comptabilisé", EtatComptabilite.Comptabilise, 0));
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Traite Fournisseur Comptabilisé", EtatComptabilite.TraiteFournisseurComptabilise, 1));
		repositoryItemImageComboBox2.SmallImages = new ImageCollection(allowModifyImages: false)
		{
			Images = 
			{
				(Image)Resources.projectdirectory_16x16,
				(Image)Resources.traite
			}
		};
		repositoryItemImageComboBox2.GlyphAlignment = HorzAlignment.Far;
		repositoryItemImageComboBox2.ShowToolTipForTrimmedText = DefaultBoolean.True;
		GridColumn gridColumn = new GridColumn
		{
			Caption = "Caisse",
			FieldName = "CaisseNo",
			Visible = false,
			ColumnEdit = _repoCaisse
		};
		GridColumn gridColumn2 = new GridColumn
		{
			Caption = "N° dossier",
			FieldName = "DossierReglementNumero",
			Visible = true,
			ColumnEdit = _repoLinkDossier
		};
		GridColumn gridColumn3 = new GridColumn
		{
			Caption = "Date",
			FieldName = "DossierReglementDate",
			Visible = true
		};
		GridColumn gridColumn4 = new GridColumn
		{
			Caption = "Tiers",
			FieldName = "TiersCode",
			Visible = true,
			ColumnEdit = _repoLinkTiers
		};
		GridColumn gridColumn5 = new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "TiersIntitule",
			Visible = true
		};
		GridColumn gridColumn6 = new GridColumn
		{
			Caption = "Identifiant",
			FieldName = "TiersIdentifiant",
			Visible = true
		};
		GridColumn gridColumn7 = new GridColumn
		{
			Caption = "N° retenue",
			FieldName = "RetenueNumero",
			Visible = true
		};
		GridColumn gridColumn8 = new GridColumn
		{
			Caption = "Date ret.",
			FieldName = "RetenueDate",
			Visible = false
		};
		GridColumn gridColumn9 = new GridColumn
		{
			Caption = "Type ret.",
			FieldName = "RetenueModeNo",
			Visible = true,
			ColumnEdit = _repoMode
		};
		GridColumn gridColumn10 = new GridColumn
		{
			Caption = "Base",
			FieldName = "RetenueBase",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn11 = new GridColumn
		{
			Caption = "Taux",
			FieldName = "RetenueTaux",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn12 = new GridColumn
		{
			Caption = "Mt. ret.",
			FieldName = "RetenueMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn13 = new GridColumn
		{
			Caption = "N° échéance",
			FieldName = "EcheanceNumero",
			Visible = true
		};
		GridColumn gridColumn14 = new GridColumn
		{
			Caption = "Date document",
			FieldName = "EcheanceDateDocument",
			Visible = false
		};
		GridColumn gridColumn15 = new GridColumn
		{
			Caption = "Echéance date",
			FieldName = "EcheanceDatePrevue",
			Visible = true
		};
		GridColumn gridColumn16 = new GridColumn
		{
			Caption = "Désign. doc.",
			FieldName = "EcheanceDesignationDocumentNo",
			Visible = true,
			ColumnEdit = repositoryItemGridLookUpEdit
		};
		GridColumn gridColumn17 = new GridColumn
		{
			Caption = "Mt. échéance",
			FieldName = "EcheanceMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn18 = new GridColumn
		{
			Caption = "N° règlement",
			FieldName = "ReglementNumero",
			Visible = true
		};
		GridColumn gridColumn19 = new GridColumn
		{
			Caption = "Date règ.",
			FieldName = "ReglementDate",
			Visible = false
		};
		GridColumn gridColumn20 = new GridColumn
		{
			Caption = "Echéance règ.",
			FieldName = "ReglementEcheance",
			Visible = true
		};
		GridColumn gridColumn21 = new GridColumn
		{
			Caption = "Mode règ.",
			FieldName = "ReglementModeNo",
			Visible = true,
			ColumnEdit = _repoMode
		};
		GridColumn gridColumn22 = new GridColumn
		{
			Caption = "Mt. règ.",
			FieldName = "ReglementMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		new GridColumn
		{
			Caption = "C",
			FieldName = "ReglementIsComptabilise",
			Visible = true,
			ToolTip = "Règlement comptabilisé",
			ColumnEdit = repositoryItemImageComboBox2,
			MinWidth = 20,
			MaxWidth = 20
		};
		GridColumn gridColumn23 = new GridColumn
		{
			Caption = "Montant",
			FieldName = "DeclarationMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		_colRetirerLigne = new GridColumn
		{
			ColumnEdit = repositoryItemButtonEdit,
			FieldName = "",
			Visible = true,
			MinWidth = 20,
			MaxWidth = 20,
			ToolTip = "Retirer ligne"
		};
		GridColumn gridColumn24 = new GridColumn
		{
			Caption = "E",
			FieldName = "HasError",
			Visible = true,
			ToolTip = "Erreur",
			ColumnEdit = repositoryItemImageComboBox,
			MinWidth = 20,
			MaxWidth = 20
		};
		gvLignes.Columns.AddRange(new GridColumn[25]
		{
			gridColumn, gridColumn2, gridColumn3, gridColumn4, gridColumn5, gridColumn6, gridColumn7, gridColumn8, gridColumn9, gridColumn10,
			gridColumn11, gridColumn12, gridColumn13, gridColumn14, gridColumn15, gridColumn16, gridColumn17, gridColumn18, gridColumn19, gridColumn20,
			gridColumn21, gridColumn22, gridColumn23, gridColumn24, _colRetirerLigne
		});
		gvLignes.Init();
		gvLignes.Tag = new Guid("{73384FB4-02FC-4A76-B556-AFFAF65F0533}");
		gvLignes.BestFitColumns();
		gvLignes.OptionsFilter.DefaultFilterEditorView = FilterEditorViewMode.VisualAndText;
		gvLignes.OptionsView.ShowFilterPanelMode = ShowFilterPanelMode.Default;
		gvLignes.OptionsSelection.MultiSelect = true;
		gvLignes.Appearance.HideSelectionRow.Assign(gvLignes.Appearance.FocusedRow);
		gvLignes.Columns.OfType<GridColumn>().ToList().ForEach(delegate(GridColumn x)
		{
			x.OptionsColumn.AllowEdit = string.IsNullOrEmpty(x.FieldName);
		});
		gridColumn2.OptionsColumn.AllowEdit = true;
		gridColumn4.OptionsColumn.AllowEdit = true;
		gvLignes.OptionsView.ShowFooter = true;
		gvLignes.CustomColumnDisplayText += delegate(object s, CustomColumnDisplayTextEventArgs e)
		{
			string fieldName = e.Column.FieldName;
			if ((!(fieldName != "Montant") || !(fieldName != "MontantTimbre")) && decimal.TryParse(e.Value?.ToString(), out var _))
			{
				int listSourceRowIndex = e.ListSourceRowIndex;
				int rowHandle = gvLignes.GetRowHandle(listSourceRowIndex);
				if (gvLignes.GetRow(rowHandle) is LigneDeclarationRetenueSourceView)
				{
					string defaultDeviseFormat = _controller.GetDefaultDeviseFormat();
					e.DisplayText = string.Format("{0:" + defaultDeviseFormat + "}", e.Value);
				}
			}
		};
	}

	private void InitGridRecap()
	{
		int nombreDecimalDefaultDevise = _controller.GetNombreDecimalDefaultDevise();
		SocieteDevise defaultDeviseSociete = _controller.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		GridColumn gridColumn = new GridColumn
		{
			Caption = "Type",
			FieldName = "ModeReglementNo",
			Visible = true,
			ColumnEdit = _repoModeCode
		};
		GridColumn gridColumn2 = new GridColumn
		{
			Caption = "Intitule",
			FieldName = "ModeReglementNo",
			Visible = true,
			ColumnEdit = _repoMode
		};
		GridColumn gridColumn3 = new GridColumn
		{
			Caption = "Taux",
			FieldName = "Taux",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn4 = new GridColumn
		{
			Caption = "Montant " + defaultDeviseSociete.Sigle,
			FieldName = "Total",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		gvGroupement.Columns.AddRange(new GridColumn[4] { gridColumn, gridColumn2, gridColumn3, gridColumn4 });
		gvGroupement.BestFitColumns();
		gvGroupement.FocusRectStyle = DrawFocusRectStyle.RowFullFocus;
		gvGroupement.OptionsSelection.EnableAppearanceFocusedCell = false;
		gvGroupement.Appearance.SelectedRow.Options.UseBackColor = true;
		gvGroupement.Appearance.FocusedRow.BackColor = Color.Transparent;
		gvGroupement.OptionsFilter.DefaultFilterEditorView = FilterEditorViewMode.VisualAndText;
		gvGroupement.OptionsView.ShowFilterPanelMode = ShowFilterPanelMode.Default;
		gvGroupement.OptionsFind.AlwaysVisible = false;
		gvGroupement.OptionsView.ShowGroupPanel = true;
		gvGroupement.OptionsView.ShowAutoFilterRow = true;
		gvGroupement.OptionsView.ShowFooter = true;
		gvGroupement.Appearance.HideSelectionRow.Assign(gvGroupement.Appearance.FocusedRow);
		gvGroupement.OptionsView.ShowIndicator = false;
		gvGroupement.OptionsBehavior.Editable = false;
		gvGroupement.OptionsMenu.ShowGroupSummaryEditorItem = true;
		gvGroupement.OptionsView.GroupFooterShowMode = GroupFooterShowMode.VisibleAlways;
		gvGroupement.GroupSummary.Add(new GridGroupSummaryItem(SummaryItemType.Sum, "Total", gridColumn4, "{0:n" + nombreDecimalDefaultDevise + "}"));
		GridColumnSummaryItem item = new GridColumnSummaryItem(SummaryItemType.Sum, "Total", "{0:n" + nombreDecimalDefaultDevise + "}");
		gridColumn4.Summary.Add(item);
	}
}
