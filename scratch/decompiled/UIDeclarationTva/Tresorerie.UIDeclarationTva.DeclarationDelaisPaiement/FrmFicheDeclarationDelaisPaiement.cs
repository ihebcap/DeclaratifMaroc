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
using Tresorerie.Infrastructure;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.UICommun.Components;
using Tresorerie.UICommun.Layout;
using Tresorerie.UICommun.Layout.Controllers;
using Tresorerie.UICommun.LicenceGratuite;
using Tresorerie.UIDeclarationTva.Properties;
using Tresorerie.Win.Commun;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.DeclarationDelaisPaiement;

public class FrmFicheDeclarationDelaisPaiement : RibbonForm, IDeclarationDelaisPaiementNotify, IEntityForm<DeclarationDelaisPaiementView>, IGridLayoutCustomizable, IGridViewForm
{
	private readonly ListDeclarationDelaisPaiementController _controller;

	private DeclarationDelaisPaiementView _current;

	private readonly IFormFactory _formFactory;

	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	private const int _VISIBLE_INDEX_BTN_LIGNE_INTEGRER = 1;

	private const int _VISIBLE_INDEX_BTN_SUPPRIMER_TOUT = 2;

	private const int _INDEX_BTN_LIGNE_INTEGRER = 0;

	private const int _INDEX_BTN_LIGNE_SUPPRIMER = 1;

	private readonly OverlayTextPainter overlayLabel;

	private readonly OverlayImagePainter overlayButton;

	private CancellationTokenSource tokenSource;

	private IOverlaySplashScreenHandle handleValider;

	private readonly LayoutController _layoutController;

	private const string CGridName = "La liste des modèles [Liste des lignes de déclaration DP]";

	private IContainer components;

	private RibbonControl ribbon;

	private RibbonPage ribbonPage1;

	private LayoutControl layoutControl1;

	private LayoutControlGroup Root;

	private EmptySpaceItem emptySpaceItem1;

	private LayoutControlItem layoutControlItem1;

	private LayoutControlItem layoutControlItem2;

	private LayoutControlItem layoutControlItem3;

	private LayoutControlItem layoutControlItem4;

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

	private ImageComboBoxEdit txtType;

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

	private ImageComboBoxEdit txtTrimestre;

	private LayoutControlItem layoutControlItem17;

	private LayoutControlItem lciTxtTrimestre;

	private LayoutControlGroup lcgLignes;

	private ImageCollection imageCollection;

	private EmptySpaceItem emptySpaceItem3;

	private DropDownButton dropDownButton1;

	private LayoutControlItem btnAction;

	private BarButtonItem btnCloturer;

	private BarButtonItem btnGenererFichier;

	private BarButtonItem btnDeposer;

	private BarButtonItem btnImporter;

	private BarSubItem btnExporterEn;

	private RibbonPageGroup rbpgExport;

	private BarButtonItem btnExporterCsv;

	private BarButtonItem btnExporterExcel;

	private BarButtonItem btnExporterPdf;

	private BarButtonItem btnExporterTexte;

	private BarButtonItem btnExporterPrint;

	private RibbonPageGroup rbpgFiltre;

	private BarButtonItem btnFiltre;

	private BarCheckItem btnAppliquerFiltre;

	private BarButtonItem btnReinitialiserFiltre;

	private BarCheckItem btnLigneFilter;

	private BarSubItem btnOption;

	private BarCheckItem btnZoneGroupement;

	private BarCheckItem btnZoneRecherche;

	private SplitterItem splitterItem2;

	private BarButtonItem btnExporterModelCSV;

	private BarButtonItem barButtonItem1;

	private EmptySpaceItem emptySpaceItem2;

	public int No { get; private set; }

	public Form EntityForm => this;

	public GridView View => gvLignes;

	public GridViewMenu ViewMenu { get; }

	public Guid GridGuid { get; set; }

	public int? AppliedLayoutNo { get; set; }

	public string GridName => "La liste des modèles [Liste des lignes de déclaration DP]";

	public GridLayoutCutomizationMenu LayoutMenu { get; set; }

	private FrmFicheDeclarationDelaisPaiement()
	{
		InitializeComponent();
		btnFiltre.ItemClick += AfficherFilter;
		btnLigneFilter.CheckedChanged += LigneFilter;
		btnAppliquerFiltre.CheckedChanged += AppliquerFilter;
		btnReinitialiserFiltre.ItemClick += ReinitialiserFilter;
		btnZoneGroupement.CheckedChanged += Grouper;
		btnZoneRecherche.CheckedChanged += Chercher;
		btnExporterCsv.ItemClick += ExportToCSV;
		btnExporterExcel.ItemClick += ExportToEXCEL;
		btnExporterTexte.ItemClick += ExportToTEXT;
		btnExporterPdf.ItemClick += ExportToPDF;
		btnExporterPrint.ItemClick += Preview;
		txtDate.KeyDown += EnterEvent;
		txtExercice.KeyDown += EnterEvent;
		txtPeriodeDebut.KeyDown += EnterEvent;
		txtPeriodeFin.KeyDown += EnterEvent;
		txtType.KeyDown += EnterEvent;
		txtLibelle.KeyDown += EnterEvent;
		txtTrimestre.KeyDown += EnterEvent;
		btnValider.Click += Valider;
		btnCloturer.ItemClick += Cloturer;
		btnDeposer.ItemClick += Deposer;
		btnGenererFichier.ItemClick += GenererFichier;
		lcgLignes.CustomButtonClick += GroupLigneButtonClick;
		gvLignes.KeyDown += GridViewKeyDown;
		txtExercice.EditValueChanged += ExerciceChanged;
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
	}

	public FrmFicheDeclarationDelaisPaiement(IFormFactory formFactory, ListDeclarationDelaisPaiementController controller, ILicenceApplicationVersion licenceApplicationVersion, LayoutController layoutController)
		: this()
	{
		_formFactory = formFactory ?? throw new ArgumentNullException("formFactory");
		_controller = controller ?? throw new ArgumentNullException("controller");
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
		_layoutController = layoutController ?? throw new ArgumentNullException("layoutController");
		Initialize();
		string defaultDeviseFormat = _controller.GetDefaultDeviseFormat();
		this.SetDecimalFormat(defaultDeviseFormat);
	}

	protected override void OnLoad(EventArgs e)
	{
		LayoutMenu = new GridLayoutCutomizationMenu(_layoutController, _formFactory, this);
		GridGuid = (Guid)gvLignes.Tag;
		SetDefaultGridLayout();
		base.OnLoad(e);
	}

	private void SetDefaultGridLayout()
	{
		UtilisateurGrid utilisateurGrid = _layoutController.GetUtilisateurGrid(GridGuid);
		if (utilisateurGrid != null)
		{
			GridLayout gridLayout = _layoutController.GetGridLayout(utilisateurGrid.GridLayoutNo);
			ApplyModel(gridLayout);
		}
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

	public void SetCurrentView(DeclarationDelaisPaiementView view)
	{
		_current = view;
		Binding();
		txtDate.Enabled = view.No == 0;
		txtExercice.Enabled = view.No == 0;
		txtPeriodeDebut.Enabled = view.No == 0;
		txtPeriodeFin.Enabled = view.No == 0;
		txtType.Enabled = view.No == 0;
		txtTrimestre.Enabled = view.No == 0;
		lciTxtTrimestre.Visibility = ((view.TypeDeclaration != TypeDeclarationDelaisPaiement.Trimestrielle) ? LayoutVisibility.Never : LayoutVisibility.Always);
		txtLibelle.Enabled = view.Statut == StatutDeclaration.EnCours;
		lcgLignes.CustomHeaderButtons[0].Properties.Enabled = view.No != 0 && view.Statut == StatutDeclaration.EnCours;
		lcgLignes.CustomHeaderButtons[1].Properties.Enabled = view.No != 0 && view.Statut == StatutDeclaration.EnCours;
		btnValider.Enabled = view.Statut == StatutDeclaration.EnCours;
		btnCloturer.Caption = ((view.No != 0 && view.Statut == StatutDeclaration.Cloture) ? "Déclôturer" : "Clôturer");
		btnCloturer.Enabled = view.No != 0 && !view.IsDepose && !view.IsFichierGenerer;
		btnGenererFichier.Enabled = view.No != 0 && view.Statut == StatutDeclaration.Cloture && !view.IsDepose;
		btnGenererFichier.Caption = ((view.No != 0 && view.IsFichierGenerer) ? "Annuler génération fic." : "Générer fichier");
		btnDeposer.Enabled = view.Statut == StatutDeclaration.Cloture && view.IsFichierGenerer && !view.IsDepose;
		btnImporter.Enabled = view.No != 0 && view.Statut == StatutDeclaration.EnCours;
		if (view.No != 0)
		{
			gcLignes.DataSource = _controller.GetLignesDeclaration(view);
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
		txtType.DataBindings.Clear();
		txtType.DataBindings.Add("EditValue", _current, "TypeDeclaration", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtLibelle.DataBindings.Clear();
		txtLibelle.DataBindings.Add("EditValue", _current, "Libelle", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtIsDepose.DataBindings.Clear();
		txtIsDepose.DataBindings.Add("EditValue", _current, "IsDepose", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtIsFichierGenere.DataBindings.Clear();
		txtIsFichierGenere.DataBindings.Add("EditValue", _current, "IsFichierGenerer", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtTrimestre.DataBindings.Clear();
		txtTrimestre.DataBindings.Add("EditValue", _current, "TrimestrePeriode", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
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
			if (e.Button.Properties.VisibleIndex == 1)
			{
				FrmSelectLigneDelaisPaiement frmSelectLigneDelaisPaiement = _formFactory.Create<FrmSelectLigneDelaisPaiement>();
				frmSelectLigneDelaisPaiement.SetDeclaration(_current);
				frmSelectLigneDelaisPaiement.ShowDialog();
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
		if (gvLignes.IsNewItemRow(focusedRowHandle) || !(gvLignes.GetFocusedRow() is LigneDeclarationDelaisPaiementView view) || XtraMessageBox.Show("Voulez vous supprimer cette ligne?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		try
		{
			_controller.DeleteLigneDeclaration(view);
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
		List<LigneDeclarationDelaisPaiementView> list = (await gvLignes.GetSelectedObjectsAsync<LigneDeclarationDelaisPaiementView>()).ToList();
		if (!list.Any() || XtraMessageBox.Show("Voulez-vous supprimer les lignes sélectionnées ?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		try
		{
			foreach (LigneDeclarationDelaisPaiementView item in list)
			{
				_controller.DeleteLigneDeclaration(item);
			}
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

	private void GenererFichier(object sender, EventArgs e)
	{
		try
		{
			if (XtraMessageBox.Show(_current.IsFichierGenerer ? ("Voulez vous annuler la génération du fichier de la déclaration n° [" + _current.Numero + "]?") : ("Voulez vous générer le fichier de la déclaration n° [" + _current.Numero + "]?"), Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
			Log.Error($"Déclaration TVA/ENC generate file: \n {ex}.");
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

	public void DeclarationDelaisPaiementChanged(DeclarationDelaisPaiementChangedArgs e)
	{
		if (_current == null || !base.IsHandleCreated || (e.Action != TypeAction.Modification && e.Action != TypeAction.Ajout) || e.EntityNo != _current.No)
		{
			return;
		}
		DeclarationDelaisPaiementView view = _controller.Get(e.EntityNo);
		if (view != null)
		{
			Action method = delegate
			{
				SetCurrentView(view);
			};
			BeginInvoke(method);
		}
	}

	public void Inititalize(DeclarationDelaisPaiementView view)
	{
		if (view == null)
		{
			view = _controller.InitView();
		}
		SetCurrentView(view);
		Text = "Déclaration " + view.Numero;
		No = view.No;
	}

	private bool IsLicenceGratuit()
	{
		try
		{
			_licenceApplicationVersion.ThrowIfGratuit();
			return true;
		}
		catch (Exception ex)
		{
			new FrmMessageBoxLicenceGratuite(ex.Message).ShowDialog();
			return false;
		}
	}

	public void AfficherFilter(object sender, ItemClickEventArgs e)
	{
		if (IsLicenceGratuit())
		{
			gvLignes.ShowFilterEditor(gvLignes.Columns[0]);
		}
	}

	public void LigneFilter(object sender, EventArgs e)
	{
		gvLignes.OptionsView.ShowAutoFilterRow = btnLigneFilter.Checked;
	}

	private void AppliquerFilter(object sender, EventArgs e)
	{
		if (IsLicenceGratuit())
		{
			gvLignes.ActiveFilterEnabled = btnAppliquerFiltre.Checked;
		}
	}

	private void ReinitialiserFilter(object sender, EventArgs e)
	{
		if (IsLicenceGratuit())
		{
			gvLignes.ActiveFilter.Clear();
		}
	}

	private void Grouper(object sender, EventArgs e)
	{
		gvLignes.OptionsView.ShowGroupPanel = btnZoneGroupement.Checked;
	}

	private void Chercher(object sender, EventArgs e)
	{
		gvLignes.OptionsFind.AlwaysVisible = btnZoneRecherche.Checked;
	}

	private void ExportToCSV(object sender, EventArgs e)
	{
		gvLignes.ExportToCSV();
	}

	private void ExportToEXCEL(object sender, EventArgs e)
	{
		gvLignes.ExportToEXCEL();
	}

	private void ExportToTEXT(object sender, EventArgs e)
	{
		gvLignes.ExportToTEXT();
	}

	private void ExportToPDF(object sender, EventArgs e)
	{
		gvLignes.ExportToPDF();
	}

	private void Preview(object sender, EventArgs e)
	{
		if (IsLicenceGratuit())
		{
			gvLignes.ShowRibbonPrintPreview();
		}
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Tresorerie.UIDeclarationTva.DeclarationDelaisPaiement.FrmFicheDeclarationDelaisPaiement));
		this.ribbon = new DevExpress.XtraBars.Ribbon.RibbonControl();
		this.btnCloturer = new DevExpress.XtraBars.BarButtonItem();
		this.btnGenererFichier = new DevExpress.XtraBars.BarButtonItem();
		this.btnDeposer = new DevExpress.XtraBars.BarButtonItem();
		this.btnImporter = new DevExpress.XtraBars.BarButtonItem();
		this.btnExporterEn = new DevExpress.XtraBars.BarSubItem();
		this.btnExporterExcel = new DevExpress.XtraBars.BarButtonItem();
		this.btnExporterCsv = new DevExpress.XtraBars.BarButtonItem();
		this.btnExporterPdf = new DevExpress.XtraBars.BarButtonItem();
		this.btnExporterTexte = new DevExpress.XtraBars.BarButtonItem();
		this.btnExporterPrint = new DevExpress.XtraBars.BarButtonItem();
		this.btnFiltre = new DevExpress.XtraBars.BarButtonItem();
		this.btnAppliquerFiltre = new DevExpress.XtraBars.BarCheckItem();
		this.btnReinitialiserFiltre = new DevExpress.XtraBars.BarButtonItem();
		this.btnLigneFilter = new DevExpress.XtraBars.BarCheckItem();
		this.btnOption = new DevExpress.XtraBars.BarSubItem();
		this.btnZoneGroupement = new DevExpress.XtraBars.BarCheckItem();
		this.btnZoneRecherche = new DevExpress.XtraBars.BarCheckItem();
		this.btnExporterModelCSV = new DevExpress.XtraBars.BarButtonItem();
		this.barButtonItem1 = new DevExpress.XtraBars.BarButtonItem();
		this.ribbonPage1 = new DevExpress.XtraBars.Ribbon.RibbonPage();
		this.rbpgExport = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
		this.rbpgFiltre = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
		this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
		this.dropDownButton1 = new DevExpress.XtraEditors.DropDownButton();
		this.popupMenuActions = new DevExpress.XtraBars.PopupMenu(this.components);
		this.txtIsFichierGenere = new DevExpress.XtraEditors.CheckEdit();
		this.txtNumero = new DevExpress.XtraEditors.TextEdit();
		this.btnValider = new DevExpress.XtraEditors.SimpleButton();
		this.gcLignes = new DevExpress.XtraGrid.GridControl();
		this.gvLignes = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtLibelle = new DevExpress.XtraEditors.TextEdit();
		this.txtIsDepose = new DevExpress.XtraEditors.CheckEdit();
		this.txtDate = new DevExpress.XtraEditors.DateEdit();
		this.txtType = new DevExpress.XtraEditors.ImageComboBoxEdit();
		this.txtStatut = new DevExpress.XtraEditors.ImageComboBoxEdit();
		this.txtExercice = new DevExpress.XtraEditors.GridLookUpEdit();
		this.gridLookUpEdit1View = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtPeriodeDebut = new DevExpress.XtraEditors.DateEdit();
		this.txtPeriodeFin = new DevExpress.XtraEditors.DateEdit();
		this.txtTrimestre = new DevExpress.XtraEditors.ImageComboBoxEdit();
		this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem12 = new DevExpress.XtraLayout.LayoutControlItem();
		this.lcgLignes = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem11 = new DevExpress.XtraLayout.LayoutControlItem();
		this.emptySpaceItem3 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.layoutControlGroup1 = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem3 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem8 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem4 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem15 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem2 = new DevExpress.XtraLayout.LayoutControlItem();
		this.lciTxtTrimestre = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem5 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem6 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem17 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem7 = new DevExpress.XtraLayout.LayoutControlItem();
		this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.btnAction = new DevExpress.XtraLayout.LayoutControlItem();
		this.splitterItem2 = new DevExpress.XtraLayout.SplitterItem();
		this.imageCollection = new DevExpress.Utils.ImageCollection(this.components);
		this.emptySpaceItem2 = new DevExpress.XtraLayout.EmptySpaceItem();
		((System.ComponentModel.ISupportInitialize)this.ribbon).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.popupMenuActions).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsFichierGenere.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtNumero.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gcLignes).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gvLignes).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtLibelle.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsDepose.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtType.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtStatut.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtExercice.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gridLookUpEdit1View).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeDebut.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeDebut.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeFin.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeFin.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtTrimestre.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.Root).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem12).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.lcgLignes).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem11).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem3).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlGroup1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem8).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem15).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.lciTxtTrimestre).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem6).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem17).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem7).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.btnAction).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.splitterItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.imageCollection).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).BeginInit();
		base.SuspendLayout();
		this.ribbon.ExpandCollapseItem.Id = 0;
		this.ribbon.Items.AddRange(new DevExpress.XtraBars.BarItem[21]
		{
			this.ribbon.ExpandCollapseItem,
			this.ribbon.SearchEditItem,
			this.btnCloturer,
			this.btnGenererFichier,
			this.btnDeposer,
			this.btnImporter,
			this.btnExporterEn,
			this.btnExporterCsv,
			this.btnExporterExcel,
			this.btnExporterPdf,
			this.btnExporterTexte,
			this.btnExporterPrint,
			this.btnFiltre,
			this.btnAppliquerFiltre,
			this.btnReinitialiserFiltre,
			this.btnLigneFilter,
			this.btnOption,
			this.btnZoneGroupement,
			this.btnZoneRecherche,
			this.btnExporterModelCSV,
			this.barButtonItem1
		});
		this.ribbon.Location = new System.Drawing.Point(0, 0);
		this.ribbon.MaxItemId = 20;
		this.ribbon.Name = "ribbon";
		this.ribbon.Pages.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPage[1] { this.ribbonPage1 });
		this.ribbon.Size = new System.Drawing.Size(1362, 162);
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
		this.btnExporterEn.Caption = "Exporter en";
		this.btnExporterEn.Id = 5;
		this.btnExporterEn.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.export_32x32;
		this.btnExporterEn.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[5]
		{
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.btnExporterExcel, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.btnExporterCsv, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.btnExporterPdf, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.btnExporterTexte, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.btnExporterPrint, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph)
		});
		this.btnExporterEn.Name = "btnExporterEn";
		this.btnExporterEn.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.All;
		this.btnExporterExcel.Caption = "Excel";
		this.btnExporterExcel.Id = 7;
		this.btnExporterExcel.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttoxlsx_16x16;
		this.btnExporterExcel.Name = "btnExporterExcel";
		this.btnExporterCsv.Caption = "CSV";
		this.btnExporterCsv.Id = 6;
		this.btnExporterCsv.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttocsv_16x16;
		this.btnExporterCsv.Name = "btnExporterCsv";
		this.btnExporterPdf.Caption = "PDF";
		this.btnExporterPdf.Id = 8;
		this.btnExporterPdf.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttopdf_16x16;
		this.btnExporterPdf.Name = "btnExporterPdf";
		this.btnExporterTexte.Caption = "Texte";
		this.btnExporterTexte.Id = 9;
		this.btnExporterTexte.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttotxt_16x16;
		this.btnExporterTexte.Name = "btnExporterTexte";
		this.btnExporterPrint.Caption = "Aperçu";
		this.btnExporterPrint.Id = 10;
		this.btnExporterPrint.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.preview_16x16;
		this.btnExporterPrint.ImageOptions.LargeImage = Tresorerie.UIDeclarationTva.Properties.Resources.preview_32x32;
		this.btnExporterPrint.Name = "btnExporterPrint";
		this.btnFiltre.Caption = "Filtre";
		this.btnFiltre.Id = 11;
		this.btnFiltre.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_filtre_32;
		this.btnFiltre.Name = "btnFiltre";
		this.btnFiltre.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.All;
		this.btnAppliquerFiltre.Caption = "Appliquer";
		this.btnAppliquerFiltre.Id = 12;
		this.btnAppliquerFiltre.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.apply_16x16;
		this.btnAppliquerFiltre.Name = "btnAppliquerFiltre";
		this.btnAppliquerFiltre.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.SmallWithText;
		this.btnReinitialiserFiltre.Caption = "Réinitialiser";
		this.btnReinitialiserFiltre.Id = 13;
		this.btnReinitialiserFiltre.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.delete_16x16;
		this.btnReinitialiserFiltre.Name = "btnReinitialiserFiltre";
		this.btnReinitialiserFiltre.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.SmallWithText;
		this.btnLigneFilter.BindableChecked = true;
		this.btnLigneFilter.Caption = "Ligne filtre";
		this.btnLigneFilter.Checked = true;
		this.btnLigneFilter.Id = 14;
		this.btnLigneFilter.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.chartsshowlegend_16x16;
		this.btnLigneFilter.Name = "btnLigneFilter";
		this.btnLigneFilter.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.SmallWithText;
		this.btnOption.Caption = "Options";
		this.btnOption.Id = 15;
		this.btnOption.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.Option_32;
		this.btnOption.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[2]
		{
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.btnZoneGroupement, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.btnZoneRecherche, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph)
		});
		this.btnOption.Name = "btnOption";
		this.btnOption.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.All;
		this.btnZoneGroupement.Caption = "Zone de groupement";
		this.btnZoneGroupement.Id = 16;
		this.btnZoneGroupement.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.New_Group_16x16;
		this.btnZoneGroupement.Name = "btnZoneGroupement";
		this.btnZoneRecherche.Caption = "Zone de recherche";
		this.btnZoneRecherche.Id = 17;
		this.btnZoneRecherche.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_look_16;
		this.btnZoneRecherche.Name = "btnZoneRecherche";
		this.btnExporterModelCSV.Caption = "Exporter model CSV";
		this.btnExporterModelCSV.Id = 18;
		this.btnExporterModelCSV.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttocsv_32x32;
		this.btnExporterModelCSV.Name = "btnExporterModelCSV";
		this.barButtonItem1.Caption = "Exporter model Csv";
		this.barButtonItem1.Id = 19;
		this.barButtonItem1.Name = "barButtonItem1";
		this.ribbonPage1.Groups.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPageGroup[2] { this.rbpgExport, this.rbpgFiltre });
		this.ribbonPage1.Name = "ribbonPage1";
		this.ribbonPage1.Text = "Déclaratif";
		this.rbpgExport.ItemLinks.Add(this.btnExporterEn);
		this.rbpgExport.Name = "rbpgExport";
		this.rbpgExport.Text = "Export";
		this.rbpgFiltre.ItemLinks.Add(this.btnFiltre);
		this.rbpgFiltre.ItemLinks.Add(this.btnAppliquerFiltre);
		this.rbpgFiltre.ItemLinks.Add(this.btnReinitialiserFiltre);
		this.rbpgFiltre.ItemLinks.Add(this.btnLigneFilter);
		this.rbpgFiltre.ItemLinks.Add(this.btnOption);
		this.rbpgFiltre.Name = "rbpgFiltre";
		this.rbpgFiltre.Text = "Filtre";
		this.layoutControl1.Controls.Add(this.dropDownButton1);
		this.layoutControl1.Controls.Add(this.txtIsFichierGenere);
		this.layoutControl1.Controls.Add(this.txtNumero);
		this.layoutControl1.Controls.Add(this.btnValider);
		this.layoutControl1.Controls.Add(this.gcLignes);
		this.layoutControl1.Controls.Add(this.txtLibelle);
		this.layoutControl1.Controls.Add(this.txtIsDepose);
		this.layoutControl1.Controls.Add(this.txtDate);
		this.layoutControl1.Controls.Add(this.txtType);
		this.layoutControl1.Controls.Add(this.txtStatut);
		this.layoutControl1.Controls.Add(this.txtExercice);
		this.layoutControl1.Controls.Add(this.txtPeriodeDebut);
		this.layoutControl1.Controls.Add(this.txtPeriodeFin);
		this.layoutControl1.Controls.Add(this.txtTrimestre);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 162);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.OptionsCustomizationForm.DesignTimeCustomizationFormPositionAndSize = new System.Drawing.Rectangle(578, 58, 650, 760);
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(1362, 599);
		this.layoutControl1.TabIndex = 1;
		this.layoutControl1.Text = "layoutControl1";
		this.dropDownButton1.DropDownArrowStyle = DevExpress.XtraEditors.DropDownArrowStyle.Show;
		this.dropDownButton1.DropDownControl = this.popupMenuActions;
		this.dropDownButton1.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_consulter_32;
		this.dropDownButton1.Location = new System.Drawing.Point(1244, 2);
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
		this.txtIsFichierGenere.Location = new System.Drawing.Point(5, 154);
		this.txtIsFichierGenere.MenuManager = this.ribbon;
		this.txtIsFichierGenere.Name = "txtIsFichierGenere";
		this.txtIsFichierGenere.Properties.Caption = "Fichier généré";
		this.txtIsFichierGenere.Properties.ReadOnly = true;
		this.txtIsFichierGenere.Size = new System.Drawing.Size(97, 20);
		this.txtIsFichierGenere.StyleController = this.layoutControl1;
		this.txtIsFichierGenere.TabIndex = 20;
		this.txtNumero.Enabled = false;
		this.txtNumero.Location = new System.Drawing.Point(80, 30);
		this.txtNumero.MenuManager = this.ribbon;
		this.txtNumero.Name = "txtNumero";
		this.txtNumero.Size = new System.Drawing.Size(121, 20);
		this.txtNumero.StyleController = this.layoutControl1;
		this.txtNumero.TabIndex = 18;
		this.btnValider.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.menu_save_32;
		this.btnValider.Location = new System.Drawing.Point(1124, 2);
		this.btnValider.Name = "btnValider";
		this.btnValider.Size = new System.Drawing.Size(116, 36);
		this.btnValider.StyleController = this.layoutControl1;
		this.btnValider.TabIndex = 15;
		this.btnValider.Text = "Valider";
		this.gcLignes.Location = new System.Drawing.Point(421, 70);
		this.gcLignes.MainView = this.gvLignes;
		this.gcLignes.MenuManager = this.ribbon;
		this.gcLignes.Name = "gcLignes";
		this.gcLignes.Size = new System.Drawing.Size(936, 524);
		this.gcLignes.TabIndex = 14;
		this.gcLignes.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gvLignes });
		this.gvLignes.GridControl = this.gcLignes;
		this.gvLignes.Name = "gvLignes";
		this.txtLibelle.Location = new System.Drawing.Point(80, 130);
		this.txtLibelle.MenuManager = this.ribbon;
		this.txtLibelle.Name = "txtLibelle";
		this.txtLibelle.Size = new System.Drawing.Size(321, 20);
		this.txtLibelle.StyleController = this.layoutControl1;
		this.txtLibelle.TabIndex = 11;
		this.txtIsDepose.Location = new System.Drawing.Point(106, 154);
		this.txtIsDepose.MenuManager = this.ribbon;
		this.txtIsDepose.Name = "txtIsDepose";
		this.txtIsDepose.Properties.Caption = "Déposée";
		this.txtIsDepose.Properties.ReadOnly = true;
		this.txtIsDepose.Size = new System.Drawing.Size(80, 20);
		this.txtIsDepose.StyleController = this.layoutControl1;
		this.txtIsDepose.TabIndex = 10;
		this.txtDate.EditValue = null;
		this.txtDate.Location = new System.Drawing.Point(80, 54);
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
		this.txtType.Location = new System.Drawing.Point(280, 55);
		this.txtType.MenuManager = this.ribbon;
		this.txtType.Name = "txtType";
		this.txtType.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtType.Properties.ReadOnly = true;
		this.txtType.Size = new System.Drawing.Size(121, 20);
		this.txtType.StyleController = this.layoutControl1;
		this.txtType.TabIndex = 7;
		this.txtStatut.Location = new System.Drawing.Point(280, 30);
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
		this.txtExercice.Location = new System.Drawing.Point(80, 80);
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
		this.txtPeriodeDebut.Location = new System.Drawing.Point(80, 105);
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
		this.txtPeriodeFin.Location = new System.Drawing.Point(280, 105);
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
		this.txtTrimestre.Location = new System.Drawing.Point(280, 80);
		this.txtTrimestre.MenuManager = this.ribbon;
		this.txtTrimestre.Name = "txtTrimestre";
		this.txtTrimestre.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtTrimestre.Size = new System.Drawing.Size(121, 20);
		this.txtTrimestre.StyleController = this.layoutControl1;
		this.txtTrimestre.TabIndex = 22;
		this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
		this.Root.GroupBordersVisible = false;
		this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[6] { this.layoutControlItem12, this.lcgLignes, this.emptySpaceItem3, this.layoutControlGroup1, this.btnAction, this.splitterItem2 });
		this.Root.Name = "Root";
		this.Root.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.Root.Size = new System.Drawing.Size(1362, 599);
		this.Root.TextVisible = false;
		this.layoutControlItem12.Control = this.btnValider;
		this.layoutControlItem12.Location = new System.Drawing.Point(1122, 0);
		this.layoutControlItem12.MaxSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem12.MinSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem12.Name = "layoutControlItem12";
		this.layoutControlItem12.Size = new System.Drawing.Size(120, 40);
		this.layoutControlItem12.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem12.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem12.TextVisible = false;
		this.lcgLignes.CustomHeaderButtons.AddRange(new DevExpress.XtraEditors.ButtonPanel.IBaseButton[2]
		{
			new DevExpress.XtraEditors.ButtonsPanelControl.GroupBoxButton("Intégrer lignes", true, imageOptions, DevExpress.XtraBars.Docking2010.ButtonStyle.PushButton, "", 1, true, null, true, false, true, null, 1),
			new DevExpress.XtraEditors.ButtonsPanelControl.GroupBoxButton("Supprimer", true, imageOptions2, DevExpress.XtraBars.Docking2010.ButtonStyle.PushButton, "", 2, true, null, true, false, true, null, 2)
		});
		this.lcgLignes.HeaderButtonsLocation = DevExpress.Utils.GroupElementLocation.AfterText;
		this.lcgLignes.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[1] { this.layoutControlItem11 });
		this.lcgLignes.Location = new System.Drawing.Point(416, 40);
		this.lcgLignes.Name = "lcgLignes";
		this.lcgLignes.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.lcgLignes.Size = new System.Drawing.Size(946, 559);
		this.lcgLignes.Text = "Lignes";
		this.layoutControlItem11.Control = this.gcLignes;
		this.layoutControlItem11.Location = new System.Drawing.Point(0, 0);
		this.layoutControlItem11.Name = "layoutControlItem11";
		this.layoutControlItem11.Size = new System.Drawing.Size(940, 528);
		this.layoutControlItem11.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem11.TextVisible = false;
		this.emptySpaceItem3.AllowHotTrack = false;
		this.emptySpaceItem3.Location = new System.Drawing.Point(416, 0);
		this.emptySpaceItem3.Name = "emptySpaceItem3";
		this.emptySpaceItem3.Size = new System.Drawing.Size(706, 40);
		this.emptySpaceItem3.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlGroup1.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[13]
		{
			this.layoutControlItem1, this.layoutControlItem3, this.layoutControlItem8, this.layoutControlItem4, this.layoutControlItem15, this.layoutControlItem2, this.lciTxtTrimestre, this.layoutControlItem5, this.layoutControlItem6, this.layoutControlItem17,
			this.layoutControlItem7, this.emptySpaceItem1, this.emptySpaceItem2
		});
		this.layoutControlGroup1.Location = new System.Drawing.Point(0, 0);
		this.layoutControlGroup1.Name = "layoutControlGroup1";
		this.layoutControlGroup1.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.layoutControlGroup1.Size = new System.Drawing.Size(406, 599);
		this.layoutControlGroup1.Text = "Entête";
		this.layoutControlItem1.Control = this.txtDate;
		this.layoutControlItem1.Location = new System.Drawing.Point(0, 24);
		this.layoutControlItem1.MaxSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem1.MinSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem1.Name = "layoutControlItem1";
		this.layoutControlItem1.Size = new System.Drawing.Size(200, 26);
		this.layoutControlItem1.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem1.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem1.Text = "Date";
		this.layoutControlItem1.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem1.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem1.TextToControlDistance = 0;
		this.layoutControlItem3.Control = this.txtExercice;
		this.layoutControlItem3.Location = new System.Drawing.Point(0, 50);
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
		this.layoutControlItem4.Control = this.txtType;
		this.layoutControlItem4.Location = new System.Drawing.Point(200, 25);
		this.layoutControlItem4.MaxSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem4.MinSize = new System.Drawing.Size(200, 25);
		this.layoutControlItem4.Name = "layoutControlItem4";
		this.layoutControlItem4.Size = new System.Drawing.Size(200, 25);
		this.layoutControlItem4.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem4.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem4.Text = "Type";
		this.layoutControlItem4.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem4.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem4.TextToControlDistance = 0;
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
		this.lciTxtTrimestre.Control = this.txtTrimestre;
		this.lciTxtTrimestre.Location = new System.Drawing.Point(200, 50);
		this.lciTxtTrimestre.MaxSize = new System.Drawing.Size(200, 25);
		this.lciTxtTrimestre.MinSize = new System.Drawing.Size(200, 25);
		this.lciTxtTrimestre.Name = "lciTxtTrimestre";
		this.lciTxtTrimestre.Size = new System.Drawing.Size(200, 25);
		this.lciTxtTrimestre.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.lciTxtTrimestre.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.lciTxtTrimestre.Text = "Trimestre";
		this.lciTxtTrimestre.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.lciTxtTrimestre.TextSize = new System.Drawing.Size(70, 13);
		this.lciTxtTrimestre.TextToControlDistance = 0;
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
		this.emptySpaceItem1.AllowHotTrack = false;
		this.emptySpaceItem1.Location = new System.Drawing.Point(185, 124);
		this.emptySpaceItem1.Name = "emptySpaceItem1";
		this.emptySpaceItem1.Size = new System.Drawing.Size(215, 24);
		this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
		this.btnAction.Control = this.dropDownButton1;
		this.btnAction.Location = new System.Drawing.Point(1242, 0);
		this.btnAction.MaxSize = new System.Drawing.Size(120, 40);
		this.btnAction.MinSize = new System.Drawing.Size(120, 40);
		this.btnAction.Name = "btnAction";
		this.btnAction.Size = new System.Drawing.Size(120, 40);
		this.btnAction.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.btnAction.TextSize = new System.Drawing.Size(0, 0);
		this.btnAction.TextVisible = false;
		this.splitterItem2.AllowHotTrack = true;
		this.splitterItem2.IsCollapsible = DevExpress.Utils.DefaultBoolean.True;
		this.splitterItem2.Location = new System.Drawing.Point(406, 0);
		this.splitterItem2.Name = "splitterItem2";
		this.splitterItem2.Size = new System.Drawing.Size(10, 599);
		this.imageCollection.ImageStream = (DevExpress.Utils.ImageCollectionStreamer)resources.GetObject("imageCollection.ImageStream");
		this.imageCollection.Images.SetKeyName(0, "SensEntree.png");
		this.imageCollection.Images.SetKeyName(1, "SensSortie.png");
		this.emptySpaceItem2.AllowHotTrack = false;
		this.emptySpaceItem2.Location = new System.Drawing.Point(0, 148);
		this.emptySpaceItem2.Name = "emptySpaceItem2";
		this.emptySpaceItem2.Size = new System.Drawing.Size(400, 420);
		this.emptySpaceItem2.TextSize = new System.Drawing.Size(0, 0);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(1362, 761);
		base.Controls.Add(this.layoutControl1);
		base.Controls.Add(this.ribbon);
		base.Name = "FrmFicheDeclarationDelaisPaiement";
		this.Ribbon = this.ribbon;
		this.Text = "FrmFicheDeclarationTvaEncaissement";
		((System.ComponentModel.ISupportInitialize)this.ribbon).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.popupMenuActions).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsFichierGenere.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtNumero.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gcLignes).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gvLignes).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtLibelle.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtIsDepose.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtType.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtStatut.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtExercice.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gridLookUpEdit1View).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeDebut.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeDebut.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeFin.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtPeriodeFin.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtTrimestre.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.Root).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem12).EndInit();
		((System.ComponentModel.ISupportInitialize)this.lcgLignes).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem11).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem3).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlGroup1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem8).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem15).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.lciTxtTrimestre).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem6).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem17).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem7).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.btnAction).EndInit();
		((System.ComponentModel.ISupportInitialize)this.splitterItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.imageCollection).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).EndInit();
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
		txtExercice.Properties.DataSource = _controller.GetAllExercice();
		txtExercice.Properties.DisplayMember = "Annee";
		txtExercice.Properties.ValueMember = "Annee";
		txtType.Properties.Items.Add(new ImageComboBoxItem("Annuelle", TypeDeclarationDelaisPaiement.Annuelle));
		txtType.Properties.Items.Add(new ImageComboBoxItem("Trimestrielle", TypeDeclarationDelaisPaiement.Trimestrielle));
		txtStatut.Properties.Items.Add(new ImageComboBoxItem("En cours", StatutDeclaration.EnCours));
		txtStatut.Properties.Items.Add(new ImageComboBoxItem("Clôturé", StatutDeclaration.Cloture));
		txtTrimestre.Properties.AddEnum<DeclarationTvaEncaissementTrimestrePeriode>();
	}

	private void InitGridLignes()
	{
		PersistentRepository persistentRepository = RepositoryItemHelper.Get();
		gcLignes.ExternalRepository = persistentRepository;
		_ = persistentRepository.Items[6];
		RepositoryItemGridLookUpEdit repositoryItemGridLookUpEdit = new RepositoryItemGridLookUpEdit();
		repositoryItemGridLookUpEdit.View.Columns.AddVisible("Code", "Code");
		repositoryItemGridLookUpEdit.View.Columns.AddVisible("Designation", "Intitulé");
		repositoryItemGridLookUpEdit.ValueMember = "No";
		repositoryItemGridLookUpEdit.DisplayMember = "Designation";
		repositoryItemGridLookUpEdit.DataSource = _controller.GetAllMode();
		repositoryItemGridLookUpEdit.NullText = string.Empty;
		RepositoryItemButtonEdit repositoryItemButtonEdit = new RepositoryItemButtonEdit();
		repositoryItemButtonEdit.Buttons[0].Kind = ButtonPredefines.Glyph;
		repositoryItemButtonEdit.Buttons[0].Image = Resources.delete_16x16;
		repositoryItemButtonEdit.TextEditStyle = TextEditStyles.HideTextEditor;
		repositoryItemButtonEdit.Buttons[0].ToolTip = "Retirer ligne";
		repositoryItemButtonEdit.ButtonClick += DeleteLigne;
		GridColumn gridColumn = new GridColumn
		{
			Caption = "Tiers",
			FieldName = "TiersCode",
			Visible = true
		};
		GridColumn gridColumn2 = new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "TiersIntitule",
			Visible = true
		};
		GridColumn gridColumn3 = new GridColumn
		{
			Caption = "Numéro",
			FieldName = "DocumentNumero",
			Visible = true
		};
		GridColumn gridColumn4 = new GridColumn
		{
			Caption = "Date",
			FieldName = "DocumentDate",
			Visible = true
		};
		new GridColumn
		{
			Caption = "Echéance prévue",
			FieldName = "DocumentEcheancePrevue",
			Visible = true
		};
		GridColumn gridColumn5 = new GridColumn
		{
			Caption = "Echéance légale",
			FieldName = "DocumentEcheanceLegale",
			Visible = true
		};
		GridColumn gridColumn6 = new GridColumn
		{
			Caption = "Montant",
			FieldName = "DocumentMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn7 = new GridColumn
		{
			Caption = "Solde",
			FieldName = "DocumentSolde",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn8 = new GridColumn
		{
			Caption = "Cours",
			FieldName = "DocumentCours",
			Visible = false,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn9 = new GridColumn
		{
			Caption = "Doc. info 1",
			FieldName = "DocumentInfoLibre1",
			Visible = false
		};
		GridColumn gridColumn10 = new GridColumn
		{
			Caption = "Doc. info 2",
			FieldName = "DocumentInfoLibre2",
			Visible = false
		};
		GridColumn gridColumn11 = new GridColumn
		{
			Caption = "Doc. info 3",
			FieldName = "DocumentInfoLibre3",
			Visible = false
		};
		GridColumn gridColumn12 = new GridColumn
		{
			Caption = "Doc. info 4",
			FieldName = "DocumentInfoLibre4",
			Visible = false
		};
		GridColumn gridColumn13 = new GridColumn
		{
			Caption = "Numéro Règ.",
			FieldName = "ReglementNumero",
			Visible = true
		};
		GridColumn gridColumn14 = new GridColumn
		{
			Caption = "Date Règ.",
			FieldName = "ReglementDate",
			Visible = true
		};
		GridColumn gridColumn15 = new GridColumn
		{
			Caption = "Mode",
			FieldName = "ReglementModeNo",
			Visible = true,
			ColumnEdit = repositoryItemGridLookUpEdit
		};
		GridColumn gridColumn16 = new GridColumn
		{
			Caption = "N° pièce",
			FieldName = "ReglementPieceNumero",
			Visible = false
		};
		GridColumn gridColumn17 = new GridColumn
		{
			Caption = "Date rapprochement",
			FieldName = "ReglementDatePoint",
			Visible = true
		};
		GridColumn gridColumn18 = new GridColumn
		{
			Caption = "Echéance Règ.",
			FieldName = "ReglementEcheance",
			Visible = true
		};
		GridColumn gridColumn19 = new GridColumn
		{
			Caption = "Montant Règ.",
			FieldName = "ReglementMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn20 = new GridColumn
		{
			Caption = "Solde Règ.",
			FieldName = "ReglementSolde",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn21 = new GridColumn
		{
			Caption = "Cours Règ.",
			FieldName = "ReglementCours",
			Visible = false,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn22 = new GridColumn
		{
			Caption = "Règ. info 1",
			FieldName = "ReglementInfoLibre1",
			Visible = false
		};
		GridColumn gridColumn23 = new GridColumn
		{
			Caption = "Règ. info 2",
			FieldName = "ReglementInfoLibre2",
			Visible = false
		};
		GridColumn gridColumn24 = new GridColumn
		{
			Caption = "Règ. info 3",
			FieldName = "ReglementInfoLibre3",
			Visible = false
		};
		GridColumn gridColumn25 = new GridColumn
		{
			Caption = "Règ. info 4",
			FieldName = "ReglementInfoLibre4",
			Visible = false
		};
		GridColumn gridColumn26 = new GridColumn
		{
			Caption = "Montant Aff.",
			FieldName = "AffectationMontant",
			Visible = false,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn27 = new GridColumn();
		gridColumn27.Caption = "Dépassement";
		gridColumn27.FieldName = "Depassement";
		gridColumn27.Visible = true;
		gridColumn27.DisplayFormat.FormatString = "n0";
		gridColumn27.DisplayFormat.FormatType = FormatType.Numeric;
		GridColumn gridColumn28 = gridColumn27;
		GridColumn gridColumn29 = new GridColumn
		{
			ColumnEdit = repositoryItemButtonEdit,
			FieldName = "",
			Visible = true,
			MinWidth = 20,
			MaxWidth = 20,
			ToolTip = "Retirer ligne"
		};
		gvLignes.Columns.AddRange(new GridColumn[28]
		{
			gridColumn, gridColumn2, gridColumn3, gridColumn4, gridColumn5, gridColumn6, gridColumn7, gridColumn8, gridColumn9, gridColumn10,
			gridColumn11, gridColumn12, gridColumn13, gridColumn14, gridColumn15, gridColumn16, gridColumn18, gridColumn17, gridColumn19, gridColumn20,
			gridColumn21, gridColumn22, gridColumn23, gridColumn24, gridColumn25, gridColumn26, gridColumn28, gridColumn29
		});
		gvLignes.Init();
		gvLignes.Tag = new Guid("{455FCE8F-2B79-40A0-8CBA-E6A5D2D76249}");
		gvLignes.Columns.OfType<GridColumn>().ToList().ForEach(delegate(GridColumn x)
		{
			x.OptionsColumn.AllowEdit = string.IsNullOrEmpty(x.FieldName);
			x.OptionsColumn.AllowSort = DefaultBoolean.False;
		});
		gvLignes.OptionsView.ShowFooter = true;
		gvLignes.OptionsView.ShowGroupPanel = false;
		gvLignes.OptionsSelection.MultiSelect = true;
		gvLignes.BestFitColumns();
	}
}
