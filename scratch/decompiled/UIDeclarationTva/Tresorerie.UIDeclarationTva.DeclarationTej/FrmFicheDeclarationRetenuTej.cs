using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
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
using Tresorerie.UIDeclarationTva.DeclarationTej.views;
using Tresorerie.UIDeclarationTva.Infrastructures;
using Tresorerie.UIDeclarationTva.Properties;
using Tresorerie.Win.Commun;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.DeclarationTej;

public class FrmFicheDeclarationRetenuTej : RibbonForm, IDeclarationRetenuSourceNotify, IEntityForm<DeclarationRetenuSourceView>, IGridLayoutCustomizable, IGridViewForm
{
	private readonly ListDeclarationRetenuSourceController _controller;

	private DeclarationRetenuSourceView _current;

	private readonly IFormFactory _formFactory;

	private Guid _gridGuid;

	private readonly LayoutController _layoutController;

	private const int _VISIBLE_INDEX_BTN_LIGNE_AJOUTER = 1;

	private const int _VISIBLE_INDEX_BTN_LIGNE_SUPPRIMER_TOUT = 2;

	private const int _INDEX_BTN_LIGNE_AJOUTER = 0;

	private const int _INDEX_BTN_LIGNE_SUPPRIER_TOUT = 1;

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

	private List<IErpExercice> _exerciceCollection;

	private const string CGridName = "La liste des modèles [Liste des lignes de déclaration de retenue à la source Tej]";

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

	private SplitterItem splitterItem1;

	private LayoutControlGroup lcgLignes;

	private ImageCollection imageCollection;

	private CheckEdit txtIsComptabilise;

	private LayoutControlItem layoutControlItem18;

	private EmptySpaceItem emptySpaceItem3;

	private DropDownButton dropDownButton1;

	private LayoutControlItem btnAction;

	private BarButtonItem btnCloturer;

	private BarButtonItem btnGenererFichier;

	private BarButtonItem btnDeposer;

	private BarButtonItem btnImporter;

	private EmptySpaceItem emptySpaceItem4;

	private ImageComboBoxEdit txtNature;

	private LayoutControlItem layoutControlItem4;

	public int No { get; private set; }

	public Form EntityForm => this;

	public GridLayoutCutomizationMenu LayoutMenu { get; set; }

	public GridView View => gvLignes;

	public GridViewMenu ViewMenu { get; }

	public Guid GridGuid => _gridGuid;

	public int? AppliedLayoutNo { get; set; }

	public string GridName => "La liste des modèles [Liste des lignes de déclaration de retenue à la source Tej]";

	private FrmFicheDeclarationRetenuTej()
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
		lcgLignes.CustomButtonClick += GroupLigneButtonClick;
		gvLignes.KeyDown += GridViewKeyDown;
		txtExercice.EditValueChanging += ExerciceChanged;
		txtMois.EditValueChanging += MoisChanged;
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

	public FrmFicheDeclarationRetenuTej(IFormFactory formFactory, ListDeclarationRetenuSourceController controller, LayoutController layoutController)
		: this()
	{
		_formFactory = formFactory ?? throw new ArgumentNullException("formFactory");
		_controller = controller ?? throw new ArgumentNullException("controller");
		_layoutController = layoutController ?? throw new ArgumentNullException("layoutController");
		_exerciceCollection = _controller.GetAllExercice();
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
		txtNature.Enabled = view.No == 0;
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
		gcLignes.DataSource = _controller.GetLignesDeclarationTej(_current);
	}

	public async void ShowFicheTiers()
	{
		IEnumerable<LigneDeclarationTejView> source = await gvLignes.GetSelectedObjectsAsync<LigneDeclarationTejView>();
		if (!source.Any() || source.Count() > 1)
		{
			return;
		}
		LigneDeclarationTejView ligneDeclarationTejView = source.FirstOrDefault();
		if (ligneDeclarationTejView == null)
		{
			return;
		}
		try
		{
			handleValider = SplashScreenManager.ShowOverlayForm(this);
			FrmFicheTiers frmFicheTiers = _formFactory.Create<FrmFicheTiers>();
			frmFicheTiers.SetTiers(ligneDeclarationTejView.FournisseurNo);
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
		IEnumerable<LigneDeclarationTejView> source = await gvLignes.GetSelectedObjectsAsync<LigneDeclarationTejView>();
		if (!source.Any() || source.Count() > 1)
		{
			return;
		}
		LigneDeclarationTejView ligneDeclarationTejView = source.FirstOrDefault();
		if (ligneDeclarationTejView != null && ligneDeclarationTejView.DossierNo.HasValue)
		{
			if (_controller.GetDossierView(ligneDeclarationTejView.DossierNo.Value) == null)
			{
				throw new ApplicationException($"Impossible de  charger le dossier n°[{ligneDeclarationTejView.DossierNo.Value}]");
			}
			((IParentFormDossierReglementFournisseur)base.MdiParent).OpenFrmListDossierReglementFrsView(ligneDeclarationTejView.DossierNo.Value);
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
		txtNature.DataBindings.Clear();
		txtNature.DataBindings.Add("EditValue", _current, "Nature", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
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
				FrmSelectLigneDeclarationRetenuTej frmSelectLigneDeclarationRetenuTej = _formFactory.Create<FrmSelectLigneDeclarationRetenuTej>();
				frmSelectLigneDeclarationRetenuTej.SetDeclaration(_current);
				if (frmSelectLigneDeclarationRetenuTej.ShowDialog() == DialogResult.OK)
				{
					IEnumerable<LigneDeclarationTejView> selectedRetenueSource = frmSelectLigneDeclarationRetenuTej.SelectedRetenueSource;
					_controller.AddLignesDeclarationTej(_current, selectedRetenueSource);
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
		if (gvLignes.IsNewItemRow(focusedRowHandle) || !(gvLignes.GetFocusedRow() is LigneDeclarationTejView ligneDeclarationTejView) || XtraMessageBox.Show("Voulez vous retirer la retenue [" + ligneDeclarationTejView.Numero + "]?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		try
		{
			_controller.DeleteLigneDeclarationTej(ligneDeclarationTejView);
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
		List<LigneDeclarationTejView> list = (await gvLignes.GetSelectedObjectsAsync<LigneDeclarationTejView>()).ToList();
		if (!list.Any() || XtraMessageBox.Show("Voulez-vous supprimer les lignes sélectionnées ?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		try
		{
			foreach (LigneDeclarationTejView item in list)
			{
				_controller.DeleteLigneDeclarationTej(item);
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

	private void GvLignes_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
	{
		gvLignes.ClearColumnErrors();
		if (gvLignes.GetFocusedRow() is LigneDeclarationTejView ligneDeclarationTejView)
		{
			GridColumn column = gvLignes.Columns["FournisseurNumero"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurNumero))
			{
				gvLignes.SetColumnError(column, "Le fournisseur est obligatoire.");
			}
			GridColumn column2 = gvLignes.Columns["FournisseurIdentifiant"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurIdentifiant))
			{
				gvLignes.SetColumnError(column2, "L'identifiant est obligatoire.");
			}
			GridColumn column3 = gvLignes.Columns["FournisseurActivite"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurActivite))
			{
				gvLignes.SetColumnError(column3, "L'activité est obligatoire.");
			}
			GridColumn column4 = gvLignes.Columns["FournisseurEmail"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurEmail))
			{
				gvLignes.SetColumnError(column4, "L'email est obligatoire.");
			}
			GridColumn column5 = gvLignes.Columns["FournisseurTelephone"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurTelephone))
			{
				gvLignes.SetColumnError(column5, "Le numéro de téléphone est obligatoire.");
			}
			GridColumn column6 = gvLignes.Columns["FournisseurDateNaissance"];
			if (ligneDeclarationTejView.FournisseurDateNaissance.IsNotLogique() && ligneDeclarationTejView.NatureFournisseur == NatureFournisseur.PersonnePhysique)
			{
				gvLignes.SetColumnError(column6, "La date de naissance est obligatoire.");
			}
			GridColumn column7 = gvLignes.Columns["FournisseurAdresse"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurAdresse))
			{
				gvLignes.SetColumnError(column7, "L'adresse est obligatoire.");
			}
			gvLignes.UpdateCurrentRow();
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
			if (!CheckValue())
			{
				return;
			}
			if (_current.No != 0)
			{
				_controller.UpdateDeclaration(_current);
				return;
			}
			if (!(txtExercice.GetSelectedDataRow() is IErpExercice))
			{
				txtExercice.ErrorText = "Exercice obligatoire.";
				return;
			}
			object editValue = txtMois.EditValue;
			if (editValue is DeclarationMoisPeriode)
			{
				_ = (DeclarationMoisPeriode)editValue;
				_current.No = _controller.CreateDeclaration(_current);
				Inititalize(_current);
				No = _current.No;
			}
			else
			{
				txtMois.ErrorText = "Mois obligatoire.";
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
		if (_current.MoisPeriode != (DeclarationMoisPeriode)_current.DateDebut.Month)
		{
			txtPeriodeDebut.ErrorText = "Le mois de la date de début est invalide.";
			return false;
		}
		if (_current.MoisPeriode != (DeclarationMoisPeriode)_current.DateFin.Month)
		{
			txtPeriodeFin.ErrorText = "Le mois de la date de fin est invalide.";
			return false;
		}
		return true;
	}

	private void ExerciceChanged(object sender, ChangingEventArgs e)
	{
		object newValue = e.NewValue;
		if (newValue is int)
		{
			int exerciceAnnee = (int)newValue;
			if (_exerciceCollection?.FirstOrDefault((IErpExercice x) => x.Annee == exerciceAnnee) == null)
			{
				txtExercice.ErrorText = "Exercice obligatoire.";
			}
			else if (_current != null)
			{
				int moisPeriode = (int)_current.MoisPeriode;
				txtPeriodeDebut.EditValue = new DateTime(exerciceAnnee, moisPeriode, 1);
				txtPeriodeFin.EditValue = new DateTime(exerciceAnnee, moisPeriode, DateTime.DaysInMonth(exerciceAnnee, moisPeriode));
			}
		}
		else
		{
			txtExercice.ErrorText = "Exercice obligatoire.";
		}
	}

	private void MoisChanged(object sender, ChangingEventArgs e)
	{
		if (!(e.NewValue is DeclarationMoisPeriode month))
		{
			txtMois.ErrorText = "Mois obligatoire.";
		}
		else if (_current != null)
		{
			txtPeriodeDebut.EditValue = new DateTime(_current.Exercice, (int)month, 1);
			txtPeriodeFin.EditValue = new DateTime(_current.Exercice, (int)month, DateTime.DaysInMonth(_current.Exercice, (int)month));
		}
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

	public int GetRowsCount()
	{
		return View.RowCount;
	}

	public int GetSelectedRowsCount()
	{
		return View.SelectedRowsCount;
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Tresorerie.UIDeclarationTva.DeclarationTej.FrmFicheDeclarationRetenuTej));
		this.ribbon = new DevExpress.XtraBars.Ribbon.RibbonControl();
		this.btnCloturer = new DevExpress.XtraBars.BarButtonItem();
		this.btnGenererFichier = new DevExpress.XtraBars.BarButtonItem();
		this.btnDeposer = new DevExpress.XtraBars.BarButtonItem();
		this.btnImporter = new DevExpress.XtraBars.BarButtonItem();
		this.ribbonPage1 = new DevExpress.XtraBars.Ribbon.RibbonPage();
		this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
		this.dropDownButton1 = new DevExpress.XtraEditors.DropDownButton();
		this.popupMenuActions = new DevExpress.XtraBars.PopupMenu(this.components);
		this.txtIsComptabilise = new DevExpress.XtraEditors.CheckEdit();
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
		this.emptySpaceItem4 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.btnAction = new DevExpress.XtraLayout.LayoutControlItem();
		this.splitterItem1 = new DevExpress.XtraLayout.SplitterItem();
		this.imageCollection = new DevExpress.Utils.ImageCollection(this.components);
		this.txtNature = new DevExpress.XtraEditors.ImageComboBoxEdit();
		this.layoutControlItem4 = new DevExpress.XtraLayout.LayoutControlItem();
		((System.ComponentModel.ISupportInitialize)this.ribbon).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.popupMenuActions).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsComptabilise.Properties).BeginInit();
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
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem4).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.btnAction).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.splitterItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.imageCollection).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtNature.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).BeginInit();
		base.SuspendLayout();
		this.ribbon.ExpandCollapseItem.Id = 0;
		this.ribbon.Items.AddRange(new DevExpress.XtraBars.BarItem[6]
		{
			this.ribbon.ExpandCollapseItem,
			this.ribbon.SearchEditItem,
			this.btnCloturer,
			this.btnGenererFichier,
			this.btnDeposer,
			this.btnImporter
		});
		this.ribbon.Location = new System.Drawing.Point(0, 0);
		this.ribbon.MaxItemId = 5;
		this.ribbon.Name = "ribbon";
		this.ribbon.Pages.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPage[1] { this.ribbonPage1 });
		this.ribbon.Size = new System.Drawing.Size(1364, 157);
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
		this.ribbonPage1.Name = "ribbonPage1";
		this.ribbonPage1.Text = "Déclaratif";
		this.layoutControl1.Controls.Add(this.dropDownButton1);
		this.layoutControl1.Controls.Add(this.txtIsComptabilise);
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
		this.layoutControl1.Controls.Add(this.txtNature);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 157);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.OptionsCustomizationForm.DesignTimeCustomizationFormPositionAndSize = new System.Drawing.Rectangle(2121, 188, 650, 760);
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(1364, 605);
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
		this.popupMenuActions.Name = "popupMenuActions";
		this.popupMenuActions.Ribbon = this.ribbon;
		this.txtIsComptabilise.Location = new System.Drawing.Point(190, 150);
		this.txtIsComptabilise.MenuManager = this.ribbon;
		this.txtIsComptabilise.Name = "txtIsComptabilise";
		this.txtIsComptabilise.Properties.Caption = "Comptabilisée";
		this.txtIsComptabilise.Properties.ReadOnly = true;
		this.txtIsComptabilise.Size = new System.Drawing.Size(87, 18);
		this.txtIsComptabilise.StyleController = this.layoutControl1;
		this.txtIsComptabilise.TabIndex = 25;
		this.txtIsFichierGenere.Location = new System.Drawing.Point(5, 150);
		this.txtIsFichierGenere.MenuManager = this.ribbon;
		this.txtIsFichierGenere.Name = "txtIsFichierGenere";
		this.txtIsFichierGenere.Properties.Caption = "Fichier généré";
		this.txtIsFichierGenere.Properties.ReadOnly = true;
		this.txtIsFichierGenere.Size = new System.Drawing.Size(97, 18);
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
		this.gcLignes.Size = new System.Drawing.Size(938, 534);
		this.gcLignes.TabIndex = 14;
		this.gcLignes.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gvLignes });
		this.gvLignes.GridControl = this.gcLignes;
		this.gvLignes.Name = "gvLignes";
		this.txtLibelle.Location = new System.Drawing.Point(80, 126);
		this.txtLibelle.MenuManager = this.ribbon;
		this.txtLibelle.Name = "txtLibelle";
		this.txtLibelle.Size = new System.Drawing.Size(321, 20);
		this.txtLibelle.StyleController = this.layoutControl1;
		this.txtLibelle.TabIndex = 11;
		this.txtIsDepose.Location = new System.Drawing.Point(106, 150);
		this.txtIsDepose.MenuManager = this.ribbon;
		this.txtIsDepose.Name = "txtIsDepose";
		this.txtIsDepose.Properties.Caption = "Déposée";
		this.txtIsDepose.Properties.ReadOnly = true;
		this.txtIsDepose.Size = new System.Drawing.Size(80, 18);
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
		this.txtPeriodeDebut.Location = new System.Drawing.Point(80, 101);
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
		this.txtPeriodeDebut.Size = new System.Drawing.Size(121, 20);
		this.txtPeriodeDebut.StyleController = this.layoutControl1;
		this.txtPeriodeDebut.TabIndex = 8;
		this.txtPeriodeFin.EditValue = null;
		this.txtPeriodeFin.Location = new System.Drawing.Point(280, 101);
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
		this.Root.Size = new System.Drawing.Size(1364, 605);
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
		this.lcgLignes.Size = new System.Drawing.Size(948, 565);
		this.lcgLignes.Text = "Lignes";
		this.layoutControlItem11.Control = this.gcLignes;
		this.layoutControlItem11.Location = new System.Drawing.Point(0, 0);
		this.layoutControlItem11.Name = "layoutControlItem11";
		this.layoutControlItem11.Size = new System.Drawing.Size(942, 538);
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
			this.layoutControlItem18, this.emptySpaceItem1, this.emptySpaceItem4, this.layoutControlItem4
		});
		this.layoutControlGroup1.Location = new System.Drawing.Point(0, 0);
		this.layoutControlGroup1.Name = "layoutControlGroup1";
		this.layoutControlGroup1.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.layoutControlGroup1.Size = new System.Drawing.Size(406, 605);
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
		this.layoutControlItem3.Size = new System.Drawing.Size(200, 26);
		this.layoutControlItem3.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem3.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem3.Text = "Exercice";
		this.layoutControlItem3.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem3.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem3.TextToControlDistance = 0;
		this.layoutControlItem8.Control = this.txtLibelle;
		this.layoutControlItem8.Location = new System.Drawing.Point(0, 100);
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
		this.layoutControlItem5.Location = new System.Drawing.Point(0, 75);
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
		this.layoutControlItem6.Location = new System.Drawing.Point(200, 75);
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
		this.layoutControlItem17.Location = new System.Drawing.Point(0, 124);
		this.layoutControlItem17.MaxSize = new System.Drawing.Size(101, 24);
		this.layoutControlItem17.MinSize = new System.Drawing.Size(101, 24);
		this.layoutControlItem17.Name = "layoutControlItem17";
		this.layoutControlItem17.Size = new System.Drawing.Size(101, 24);
		this.layoutControlItem17.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem17.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem17.TextVisible = false;
		this.layoutControlItem7.Control = this.txtIsDepose;
		this.layoutControlItem7.Location = new System.Drawing.Point(101, 124);
		this.layoutControlItem7.MaxSize = new System.Drawing.Size(84, 24);
		this.layoutControlItem7.MinSize = new System.Drawing.Size(84, 24);
		this.layoutControlItem7.Name = "layoutControlItem7";
		this.layoutControlItem7.Size = new System.Drawing.Size(84, 24);
		this.layoutControlItem7.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem7.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem7.TextVisible = false;
		this.layoutControlItem18.Control = this.txtIsComptabilise;
		this.layoutControlItem18.Location = new System.Drawing.Point(185, 124);
		this.layoutControlItem18.Name = "layoutControlItem18";
		this.layoutControlItem18.Size = new System.Drawing.Size(91, 24);
		this.layoutControlItem18.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem18.TextVisible = false;
		this.layoutControlItem18.Visibility = DevExpress.XtraLayout.Utils.LayoutVisibility.Never;
		this.emptySpaceItem1.AllowHotTrack = false;
		this.emptySpaceItem1.Location = new System.Drawing.Point(276, 124);
		this.emptySpaceItem1.Name = "emptySpaceItem1";
		this.emptySpaceItem1.Size = new System.Drawing.Size(124, 24);
		this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
		this.emptySpaceItem4.AllowHotTrack = false;
		this.emptySpaceItem4.Location = new System.Drawing.Point(0, 148);
		this.emptySpaceItem4.Name = "emptySpaceItem4";
		this.emptySpaceItem4.Size = new System.Drawing.Size(400, 430);
		this.emptySpaceItem4.TextSize = new System.Drawing.Size(0, 0);
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
		this.splitterItem1.Size = new System.Drawing.Size(10, 605);
		this.imageCollection.ImageStream = (DevExpress.Utils.ImageCollectionStreamer)resources.GetObject("imageCollection.ImageStream");
		this.imageCollection.Images.SetKeyName(0, "SensEntree.png");
		this.imageCollection.Images.SetKeyName(1, "SensSortie.png");
		this.txtNature.Location = new System.Drawing.Point(280, 76);
		this.txtNature.Name = "txtNature";
		this.txtNature.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtNature.Size = new System.Drawing.Size(121, 20);
		this.txtNature.StyleController = this.layoutControl1;
		this.txtNature.TabIndex = 5;
		this.layoutControlItem4.Control = this.txtNature;
		this.layoutControlItem4.ControlAlignment = System.Drawing.ContentAlignment.TopLeft;
		this.layoutControlItem4.CustomizationFormText = "Statut";
		this.layoutControlItem4.Location = new System.Drawing.Point(200, 50);
		this.layoutControlItem4.MaxSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem4.MinSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem4.Name = "layoutControlItem4";
		this.layoutControlItem4.Size = new System.Drawing.Size(200, 25);
		this.layoutControlItem4.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem4.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem4.Text = "Nature";
		this.layoutControlItem4.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem4.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem4.TextToControlDistance = 0;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(1364, 762);
		base.Controls.Add(this.layoutControl1);
		base.Controls.Add(this.ribbon);
		base.Name = "FrmFicheDeclarationRetenuTej";
		this.Ribbon = this.ribbon;
		this.Text = "FrmFicheDeclarationRetenueTej";
		((System.ComponentModel.ISupportInitialize)this.ribbon).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.popupMenuActions).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsComptabilise.Properties).EndInit();
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
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem4).EndInit();
		((System.ComponentModel.ISupportInitialize)this.btnAction).EndInit();
		((System.ComponentModel.ISupportInitialize)this.splitterItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.imageCollection).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtNature.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}

	private void Initialize()
	{
		InitControls();
		InitGridLignes();
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
		txtExercice.Properties.DataSource = _exerciceCollection;
		txtExercice.Properties.DisplayMember = "Annee";
		txtExercice.Properties.ValueMember = "Annee";
		txtStatut.Properties.Items.Add(new ImageComboBoxItem("En cours", StatutDeclaration.EnCours));
		txtStatut.Properties.Items.Add(new ImageComboBoxItem("Clôturé", StatutDeclaration.Cloture));
		txtNature.Properties.AddEnum<NatureDeclaration>();
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
		RepositoryItemButtonEdit repositoryItemButtonEdit = new RepositoryItemButtonEdit();
		repositoryItemButtonEdit.Buttons[0].Kind = ButtonPredefines.Glyph;
		repositoryItemButtonEdit.Buttons[0].Image = Resources.delete_16x16;
		repositoryItemButtonEdit.TextEditStyle = TextEditStyles.HideTextEditor;
		repositoryItemButtonEdit.Buttons[0].ToolTip = "Retirer ligne";
		repositoryItemButtonEdit.ButtonClick += DeleteLigne;
		RepositoryItemImageComboBox repositoryItemImageComboBox = new RepositoryItemImageComboBox();
		repositoryItemImageComboBox.Items.Add(new ImageComboBoxItem("Non comptabilisé", EtatComptabilite.NonComptabilise));
		repositoryItemImageComboBox.Items.Add(new ImageComboBoxItem("Comptabilisé", EtatComptabilite.Comptabilise, 0));
		repositoryItemImageComboBox.Items.Add(new ImageComboBoxItem("Traite Fournisseur Comptabilisé", EtatComptabilite.TraiteFournisseurComptabilise, 1));
		repositoryItemImageComboBox.SmallImages = new ImageCollection(allowModifyImages: false)
		{
			Images = 
			{
				(Image)Resources.projectdirectory_16x16,
				(Image)Resources.traite
			}
		};
		repositoryItemImageComboBox.GlyphAlignment = HorzAlignment.Far;
		repositoryItemImageComboBox.ShowToolTipForTrimmedText = DefaultBoolean.True;
		RepositoryItemImageComboBox repositoryItemImageComboBox2 = new RepositoryItemImageComboBox();
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Erreur", true, 0));
		repositoryItemImageComboBox2.SmallImages = new ImageCollection(allowModifyImages: false)
		{
			Images = { (Image)Resources.delete }
		};
		repositoryItemImageComboBox2.GlyphAlignment = HorzAlignment.Far;
		repositoryItemImageComboBox2.ShowToolTipForTrimmedText = DefaultBoolean.True;
		RepositoryItemGridLookUpEdit repositoryItemGridLookUpEdit = new RepositoryItemGridLookUpEdit();
		repositoryItemGridLookUpEdit.View.Columns.AddVisible("Code", "Code");
		repositoryItemGridLookUpEdit.View.Columns.AddVisible("Intitule", "Intitulé");
		repositoryItemGridLookUpEdit.View.Columns.AddVisible("TypeOperation", "Type opération");
		repositoryItemGridLookUpEdit.View.Columns.AddVisible("Operation", "Opération");
		repositoryItemGridLookUpEdit.DataSource = _controller.GetAllOperationRetenue();
		repositoryItemGridLookUpEdit.ValueMember = "No";
		repositoryItemGridLookUpEdit.DisplayMember = "TypeOperation";
		repositoryItemGridLookUpEdit.NullText = string.Empty;
		RepositoryItemGridLookUpEdit repositoryItemGridLookUpEdit2 = new RepositoryItemGridLookUpEdit();
		repositoryItemGridLookUpEdit2.View.Columns.AddVisible("Code", "Code");
		repositoryItemGridLookUpEdit2.View.Columns.AddVisible("Intitule", "Intitulé");
		repositoryItemGridLookUpEdit2.View.Columns.AddVisible("TypeOperation", "Type opération");
		repositoryItemGridLookUpEdit2.View.Columns.AddVisible("Operation", "Opération");
		repositoryItemGridLookUpEdit2.DataSource = _controller.GetAllOperationRetenue();
		repositoryItemGridLookUpEdit2.ValueMember = "No";
		repositoryItemGridLookUpEdit2.DisplayMember = "Operation";
		repositoryItemGridLookUpEdit2.NullText = string.Empty;
		GridColumn gridColumn = new GridColumn
		{
			Caption = "Numéro",
			FieldName = "Numero",
			Visible = true
		};
		GridColumn gridColumn2 = new GridColumn
		{
			Caption = "Date",
			FieldName = "Date",
			Visible = true
		};
		GridColumn gridColumn3 = new GridColumn
		{
			Caption = "Fournisseur",
			FieldName = "FournisseurNumero",
			Visible = true,
			ColumnEdit = _repoLinkTiers
		};
		GridColumn gridColumn4 = new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "FournisseurIntitule",
			Visible = true
		};
		GridColumn gridColumn5 = new GridColumn
		{
			Caption = "Identifiant",
			FieldName = "FournisseurIdentifiant",
			Visible = true
		};
		GridColumn gridColumn6 = new GridColumn
		{
			Caption = "Activite",
			FieldName = "FournisseurActivite",
			Visible = true
		};
		GridColumn gridColumn7 = new GridColumn
		{
			Caption = "Adresse",
			FieldName = "FournisseurAdresse",
			Visible = true
		};
		GridColumn gridColumn8 = new GridColumn
		{
			Caption = "Date de naissance",
			FieldName = "FournisseurDateNaissance",
			Visible = true
		};
		GridColumn gridColumn9 = new GridColumn
		{
			Caption = "Email",
			FieldName = "FournisseurEmail",
			Visible = true
		};
		GridColumn gridColumn10 = new GridColumn
		{
			Caption = "Telephone",
			FieldName = "FournisseurTelephone",
			Visible = true
		};
		GridColumn gridColumn11 = new GridColumn
		{
			Caption = "P",
			FieldName = "IsPrisCharge",
			Visible = true,
			ToolTip = "Pris en charge"
		};
		GridColumn gridColumn12 = new GridColumn
		{
			Caption = "C",
			FieldName = "IsConvention",
			Visible = true,
			ToolTip = "Convention"
		};
		GridColumn gridColumn13 = new GridColumn
		{
			Caption = "Exercice Fac.",
			FieldName = "ExerciceFacturation",
			Visible = true
		};
		GridColumn gridColumn14 = new GridColumn
		{
			Caption = "Caisse",
			FieldName = "CaisseNo",
			Visible = true,
			ColumnEdit = _repoCaisse
		};
		GridColumn gridColumn15 = new GridColumn
		{
			Caption = "Type opération",
			FieldName = "OperationNo",
			Visible = true,
			ColumnEdit = repositoryItemGridLookUpEdit
		};
		GridColumn gridColumn16 = new GridColumn
		{
			Caption = "Opération",
			FieldName = "OperationNo",
			Visible = true,
			ColumnEdit = repositoryItemGridLookUpEdit2
		};
		GridColumn gridColumn17 = new GridColumn
		{
			Caption = "Montant HT",
			FieldName = "MontantHorsTaxe",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn18 = new GridColumn
		{
			Caption = "Taux TVA",
			FieldName = "TauxTva",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn19 = new GridColumn
		{
			Caption = "Montant TVA",
			FieldName = "MontantTva",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn20 = new GridColumn
		{
			Caption = "Montant TTC",
			FieldName = "MontantTtc",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn21 = new GridColumn
		{
			Caption = "Type",
			FieldName = "ModeReglementNo",
			Visible = true,
			ColumnEdit = _repoMode
		};
		GridColumn gridColumn22 = new GridColumn
		{
			Caption = "Taux",
			FieldName = "TauxRetenue",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn23 = new GridColumn
		{
			Caption = "Montant",
			FieldName = "MontantRetenue",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn24 = new GridColumn
		{
			Caption = "Net payé",
			FieldName = "MontantNetPaye",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn25 = new GridColumn
		{
			Caption = "Référence",
			FieldName = "Reference",
			Visible = true
		};
		GridColumn gridColumn26 = new GridColumn
		{
			Caption = "N° dossier",
			FieldName = "DossierNumero",
			Visible = true,
			ColumnEdit = _repoLinkDossier
		};
		GridColumn gridColumn27 = new GridColumn
		{
			Caption = "C",
			FieldName = "IsComptabilise",
			Visible = true,
			ToolTip = "Comptabilisé",
			ColumnEdit = repositoryItemImageComboBox,
			MinWidth = 20,
			MaxWidth = 20
		};
		GridColumn gridColumn28 = new GridColumn
		{
			Caption = "E",
			FieldName = "HasError",
			Visible = true,
			ToolTip = "Erreur",
			ColumnEdit = repositoryItemImageComboBox2,
			MinWidth = 20,
			MaxWidth = 20
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
		gvLignes.Columns.AddRange(new GridColumn[29]
		{
			gridColumn, gridColumn2, gridColumn3, gridColumn4, gridColumn5, gridColumn6, gridColumn7, gridColumn8, gridColumn9, gridColumn10,
			gridColumn11, gridColumn12, gridColumn13, gridColumn14, gridColumn15, gridColumn16, gridColumn17, gridColumn18, gridColumn19, gridColumn20,
			gridColumn21, gridColumn22, gridColumn23, gridColumn24, gridColumn25, gridColumn26, gridColumn27, gridColumn28, _colRetirerLigne
		});
		gvLignes.Init();
		gvLignes.Tag = new Guid("{F61B7F4B-9EA6-449F-B6DF-F5FD892F3EE9}");
		gvLignes.BestFitColumns();
		gvLignes.OptionsFilter.DefaultFilterEditorView = FilterEditorViewMode.VisualAndText;
		gvLignes.OptionsView.ShowFilterPanelMode = ShowFilterPanelMode.Default;
		gvLignes.OptionsSelection.MultiSelect = true;
		gvLignes.Appearance.HideSelectionRow.Assign(gvLignes.Appearance.FocusedRow);
		gvLignes.Columns.OfType<GridColumn>().ToList().ForEach(delegate(GridColumn x)
		{
			x.OptionsColumn.AllowEdit = string.IsNullOrEmpty(x.FieldName);
		});
		gridColumn26.OptionsColumn.AllowEdit = true;
		gridColumn3.OptionsColumn.AllowEdit = true;
		gvLignes.OptionsView.ShowFooter = true;
	}
}
