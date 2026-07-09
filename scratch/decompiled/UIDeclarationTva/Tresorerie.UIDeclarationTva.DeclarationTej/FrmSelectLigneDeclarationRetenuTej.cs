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
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Menu;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using DevExpress.XtraLayout.Utils;
using DevExpress.XtraSplashScreen;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.UICommun.Components;
using Tresorerie.UICommun.Layout;
using Tresorerie.UICommun.Layout.Controllers;
using Tresorerie.UIDeclarationTva.DeclarationRS.Controllers;
using Tresorerie.UIDeclarationTva.DeclarationRS.views;
using Tresorerie.UIDeclarationTva.DeclarationTej.views;
using Tresorerie.UIDeclarationTva.Properties;
using Tresorerie.Win.Commun;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.DeclarationTej;

public class FrmSelectLigneDeclarationRetenuTej : XtraForm, IGridLayoutCustomizable
{
	private readonly ListeRetenueFournisseurController _controller;

	private readonly OverlayTextPainter overlayLabel;

	private readonly OverlayImagePainter overlayButton;

	private readonly IFormFactory _formFactory;

	private CancellationTokenSource tokenSource;

	private IOverlaySplashScreenHandle handleValider;

	private Guid _gridGuid;

	private readonly LayoutController _layoutController;

	private const string CGridName = "La liste des modèles [Liste des lignes de déclaration de retenue à la source]";

	private RepositoryItemGridLookUpEdit _repoCaisse;

	private RepositoryItemGridLookUpEdit _repoMode;

	private RepositoryItemGridLookUpEdit _repoDevise;

	private DeclarationRetenuSourceView _currentView;

	private IContainer components;

	private LayoutControl layoutControl1;

	private LayoutControlGroup Root;

	private GridControl gc;

	private GridView gv;

	private LayoutControlItem layoutControlItem4;

	private SimpleButton btnActualiser;

	private EmptySpaceItem emptySpaceItem1;

	private LayoutControlItem layoutControlItem1;

	private SimpleButton btnValider;

	private SimpleButton btnAnnuler;

	private LayoutControlItem layoutControlItem2;

	private LayoutControlItem layoutControlItem3;

	private DropDownButton dropDownButton1;

	private LayoutControlItem layoutControlItem5;

	private PopupMenu popupMenuExport;

	private BarButtonItem bbiExportExcel;

	private BarButtonItem bbiExportCsv;

	private BarButtonItem bbiExportText;

	private BarButtonItem bbiExportPdf;

	private BarButtonItem bbiExportPrint;

	private BarManager barManager1;

	private BarDockControl barDockControlTop;

	private BarDockControl barDockControlBottom;

	private BarDockControl barDockControlLeft;

	private BarDockControl barDockControlRight;

	public IEnumerable<LigneDeclarationTejView> SelectedRetenueSource { get; set; }

	public GridLayoutCutomizationMenu LayoutMenu { get; set; }

	public GridView View => gv;

	public GridViewMenu ViewMenu { get; }

	public Guid GridGuid => _gridGuid;

	public int? AppliedLayoutNo { get; set; }

	public string GridName => "La liste des modèles [Liste des lignes de déclaration de retenue à la source]";

	private FrmSelectLigneDeclarationRetenuTej()
	{
		InitializeComponent();
		btnActualiser.Click += Actualiser;
		btnValider.Click += Valider;
		btnAnnuler.Click += Annuler;
		bbiExportCsv.ItemClick += ExportToCSV;
		bbiExportExcel.ItemClick += ExportToEXCEL;
		bbiExportText.ItemClick += ExportToTEXT;
		bbiExportPdf.ItemClick += ExportToPDF;
		bbiExportPrint.ItemClick += Preview;
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
		gv.FocusedRowChanged += Gv_FocusedRowChanged;
	}

	public FrmSelectLigneDeclarationRetenuTej(ListeRetenueFournisseurController controller, IFormFactory formFactory, LayoutController layoutController)
		: this()
	{
		_controller = controller ?? throw new ArgumentNullException("controller");
		_formFactory = formFactory ?? throw new ArgumentNullException("formFactory");
		_layoutController = layoutController ?? throw new ArgumentNullException("layoutController");
		Initialize();
		SocieteDevise defaultDeviseSociete = _controller.GetDefaultDeviseSociete();
		this.SetDecimalFormat(defaultDeviseSociete.Format);
	}

	protected override void OnLoad(EventArgs e)
	{
		LayoutMenu = new GridLayoutCutomizationMenu(_layoutController, _formFactory, this);
		_gridGuid = (Guid)gv.Tag;
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

	public void SetDeclaration(DeclarationRetenuSourceView view)
	{
		_currentView = view ?? throw new ArgumentNullException("view");
	}

	private void EnterEvent(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Return)
		{
			Actualiser();
		}
	}

	protected override void OnShown(EventArgs e)
	{
		Actualiser();
		base.OnShown(e);
	}

	private async void Valider(object sender = null, EventArgs e = null)
	{
		SelectedRetenueSource = await gv.GetSelectedObjectsAsync<LigneDeclarationTejView>();
		base.DialogResult = DialogResult.OK;
	}

	private void Annuler(object sender = null, EventArgs e = null)
	{
		base.DialogResult = DialogResult.Cancel;
	}

	private void Actualiser(object sender = null, EventArgs e = null)
	{
		gc.DataSource = null;
		try
		{
			handleValider = SplashScreenManager.ShowOverlayForm(this);
			List<LigneDeclarationTejView> allRetenueToDeclarationTej = _controller.GetAllRetenueToDeclarationTej(_currentView);
			gc.DataSource = allRetenueToDeclarationTej;
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

	private void Gv_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
	{
		gv.ClearColumnErrors();
		if (gv.GetFocusedRow() is LigneDeclarationTejView ligneDeclarationTejView)
		{
			GridColumn column = gv.Columns["FournisseurNumero"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurNumero))
			{
				gv.SetColumnError(column, "Le fournisseur est obligatoire.");
			}
			GridColumn column2 = gv.Columns["FournisseurIdentifiant"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurIdentifiant))
			{
				gv.SetColumnError(column2, "L'identifiant est obligatoire.");
			}
			GridColumn column3 = gv.Columns["FournisseurActivite"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurActivite))
			{
				gv.SetColumnError(column3, "L'activité est obligatoire.");
			}
			GridColumn column4 = gv.Columns["FournisseurEmail"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurEmail))
			{
				gv.SetColumnError(column4, "L'email est obligatoire.");
			}
			GridColumn column5 = gv.Columns["FournisseurTelephone"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurTelephone))
			{
				gv.SetColumnError(column5, "Le numéro de téléphone est obligatoire.");
			}
			GridColumn column6 = gv.Columns["FournisseurDateNaissance"];
			if (ligneDeclarationTejView.FournisseurDateNaissance.IsNotLogique() && ligneDeclarationTejView.NatureFournisseur == NatureFournisseur.PersonnePhysique)
			{
				gv.SetColumnError(column6, "La date de naissance est obligatoire.");
			}
			GridColumn column7 = gv.Columns["FournisseurAdresse"];
			if (string.IsNullOrEmpty(ligneDeclarationTejView.FournisseurAdresse))
			{
				gv.SetColumnError(column7, "L'adresse est obligatoire.");
			}
			gv.UpdateCurrentRow();
		}
	}

	private void ExportToCSV(object sender, EventArgs e)
	{
		gv.ExportToCSV();
	}

	private void ExportToEXCEL(object sender, EventArgs e)
	{
		gv.ExportToEXCEL();
	}

	private void ExportToTEXT(object sender, EventArgs e)
	{
		gv.ExportToTEXT();
	}

	private void ExportToPDF(object sender, EventArgs e)
	{
		gv.ExportToPDF();
	}

	private void Preview(object sender, EventArgs e)
	{
		gv.ShowRibbonPrintPreview();
	}

	public void ApplyModel(GridLayout layout)
	{
		if (layout != null)
		{
			MemoryStream stream = new MemoryStream(layout.Layout);
			gv.RestoreLayoutFromStream(stream, GridLayoutOptionCustomization.LayoutVisualOption);
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
		this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
		this.dropDownButton1 = new DevExpress.XtraEditors.DropDownButton();
		this.popupMenuExport = new DevExpress.XtraBars.PopupMenu(this.components);
		this.bbiExportExcel = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportCsv = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportText = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportPdf = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportPrint = new DevExpress.XtraBars.BarButtonItem();
		this.barManager1 = new DevExpress.XtraBars.BarManager(this.components);
		this.barDockControlTop = new DevExpress.XtraBars.BarDockControl();
		this.barDockControlBottom = new DevExpress.XtraBars.BarDockControl();
		this.barDockControlLeft = new DevExpress.XtraBars.BarDockControl();
		this.barDockControlRight = new DevExpress.XtraBars.BarDockControl();
		this.btnValider = new DevExpress.XtraEditors.SimpleButton();
		this.btnAnnuler = new DevExpress.XtraEditors.SimpleButton();
		this.btnActualiser = new DevExpress.XtraEditors.SimpleButton();
		this.gc = new DevExpress.XtraGrid.GridControl();
		this.gv = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem4 = new DevExpress.XtraLayout.LayoutControlItem();
		this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem2 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem3 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem5 = new DevExpress.XtraLayout.LayoutControlItem();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.popupMenuExport).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.barManager1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gc).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gv).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.Root).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).BeginInit();
		base.SuspendLayout();
		this.layoutControl1.Controls.Add(this.dropDownButton1);
		this.layoutControl1.Controls.Add(this.btnValider);
		this.layoutControl1.Controls.Add(this.btnAnnuler);
		this.layoutControl1.Controls.Add(this.btnActualiser);
		this.layoutControl1.Controls.Add(this.gc);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 0);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.OptionsCustomizationForm.DesignTimeCustomizationFormPositionAndSize = new System.Drawing.Rectangle(1217, 284, 650, 400);
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(992, 747);
		this.layoutControl1.TabIndex = 1;
		this.layoutControl1.Text = "layoutControl1";
		this.dropDownButton1.DropDownArrowStyle = DevExpress.XtraEditors.DropDownArrowStyle.Show;
		this.dropDownButton1.DropDownControl = this.popupMenuExport;
		this.dropDownButton1.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_export_32;
		this.dropDownButton1.Location = new System.Drawing.Point(514, 2);
		this.dropDownButton1.Name = "dropDownButton1";
		this.dropDownButton1.Size = new System.Drawing.Size(116, 36);
		this.dropDownButton1.StyleController = this.layoutControl1;
		this.dropDownButton1.TabIndex = 14;
		this.dropDownButton1.Text = "Exporter en";
		this.popupMenuExport.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[5]
		{
			new DevExpress.XtraBars.LinkPersistInfo(this.bbiExportExcel),
			new DevExpress.XtraBars.LinkPersistInfo(this.bbiExportCsv),
			new DevExpress.XtraBars.LinkPersistInfo(this.bbiExportText),
			new DevExpress.XtraBars.LinkPersistInfo(this.bbiExportPdf),
			new DevExpress.XtraBars.LinkPersistInfo(this.bbiExportPrint)
		});
		this.popupMenuExport.Manager = this.barManager1;
		this.popupMenuExport.Name = "popupMenuExport";
		this.bbiExportExcel.Caption = "Excel";
		this.bbiExportExcel.Id = 0;
		this.bbiExportExcel.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttoxlsx_16x16;
		this.bbiExportExcel.Name = "bbiExportExcel";
		this.bbiExportCsv.Caption = "CSV";
		this.bbiExportCsv.Id = 2;
		this.bbiExportCsv.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttocsv_16x16;
		this.bbiExportCsv.Name = "bbiExportCsv";
		this.bbiExportText.Caption = "Text";
		this.bbiExportText.Id = 3;
		this.bbiExportText.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttotxt_16x16;
		this.bbiExportText.Name = "bbiExportText";
		this.bbiExportPdf.Caption = "PDF";
		this.bbiExportPdf.Id = 1;
		this.bbiExportPdf.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttopdf_16x16;
		this.bbiExportPdf.Name = "bbiExportPdf";
		this.bbiExportPrint.Caption = "Aperçu";
		this.bbiExportPrint.Id = 4;
		this.bbiExportPrint.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_look_16;
		this.bbiExportPrint.Name = "bbiExportPrint";
		this.barManager1.DockControls.Add(this.barDockControlTop);
		this.barManager1.DockControls.Add(this.barDockControlBottom);
		this.barManager1.DockControls.Add(this.barDockControlLeft);
		this.barManager1.DockControls.Add(this.barDockControlRight);
		this.barManager1.Form = this;
		this.barManager1.Items.AddRange(new DevExpress.XtraBars.BarItem[5] { this.bbiExportExcel, this.bbiExportPdf, this.bbiExportCsv, this.bbiExportText, this.bbiExportPrint });
		this.barManager1.MaxItemId = 5;
		this.barDockControlTop.CausesValidation = false;
		this.barDockControlTop.Dock = System.Windows.Forms.DockStyle.Top;
		this.barDockControlTop.Location = new System.Drawing.Point(0, 0);
		this.barDockControlTop.Manager = this.barManager1;
		this.barDockControlTop.Size = new System.Drawing.Size(992, 0);
		this.barDockControlBottom.CausesValidation = false;
		this.barDockControlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.barDockControlBottom.Location = new System.Drawing.Point(0, 747);
		this.barDockControlBottom.Manager = this.barManager1;
		this.barDockControlBottom.Size = new System.Drawing.Size(992, 0);
		this.barDockControlLeft.CausesValidation = false;
		this.barDockControlLeft.Dock = System.Windows.Forms.DockStyle.Left;
		this.barDockControlLeft.Location = new System.Drawing.Point(0, 0);
		this.barDockControlLeft.Manager = this.barManager1;
		this.barDockControlLeft.Size = new System.Drawing.Size(0, 747);
		this.barDockControlRight.CausesValidation = false;
		this.barDockControlRight.Dock = System.Windows.Forms.DockStyle.Right;
		this.barDockControlRight.Location = new System.Drawing.Point(992, 0);
		this.barDockControlRight.Manager = this.barManager1;
		this.barDockControlRight.Size = new System.Drawing.Size(0, 747);
		this.btnValider.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_valid_32;
		this.btnValider.Location = new System.Drawing.Point(754, 2);
		this.btnValider.Name = "btnValider";
		this.btnValider.Size = new System.Drawing.Size(117, 36);
		this.btnValider.StyleController = this.layoutControl1;
		this.btnValider.TabIndex = 13;
		this.btnValider.Text = "Valider";
		this.btnAnnuler.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_delete_32;
		this.btnAnnuler.Location = new System.Drawing.Point(875, 2);
		this.btnAnnuler.Name = "btnAnnuler";
		this.btnAnnuler.Size = new System.Drawing.Size(115, 36);
		this.btnAnnuler.StyleController = this.layoutControl1;
		this.btnAnnuler.TabIndex = 12;
		this.btnAnnuler.Text = "Annuler";
		this.btnActualiser.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_refresh4_32;
		this.btnActualiser.Location = new System.Drawing.Point(634, 2);
		this.btnActualiser.Name = "btnActualiser";
		this.btnActualiser.Size = new System.Drawing.Size(116, 36);
		this.btnActualiser.StyleController = this.layoutControl1;
		this.btnActualiser.TabIndex = 11;
		this.btnActualiser.Text = "Actualiser";
		this.gc.Location = new System.Drawing.Point(2, 42);
		this.gc.MainView = this.gv;
		this.gc.Name = "gc";
		this.gc.Size = new System.Drawing.Size(988, 703);
		this.gc.TabIndex = 7;
		this.gc.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gv });
		this.gv.GridControl = this.gc;
		this.gv.Name = "gv";
		this.gv.OptionsSelection.MultiSelect = true;
		this.gv.OptionsSelection.MultiSelectMode = DevExpress.XtraGrid.Views.Grid.GridMultiSelectMode.CheckBoxRowSelect;
		this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
		this.Root.GroupBordersVisible = false;
		this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[6] { this.layoutControlItem4, this.emptySpaceItem1, this.layoutControlItem1, this.layoutControlItem5, this.layoutControlItem3, this.layoutControlItem2 });
		this.Root.Name = "Root";
		this.Root.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.Root.Size = new System.Drawing.Size(992, 747);
		this.Root.TextVisible = false;
		this.layoutControlItem4.Control = this.gc;
		this.layoutControlItem4.Location = new System.Drawing.Point(0, 40);
		this.layoutControlItem4.Name = "layoutControlItem4";
		this.layoutControlItem4.Size = new System.Drawing.Size(992, 707);
		this.layoutControlItem4.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem4.TextVisible = false;
		this.emptySpaceItem1.AllowHotTrack = false;
		this.emptySpaceItem1.Location = new System.Drawing.Point(0, 0);
		this.emptySpaceItem1.Name = "emptySpaceItem1";
		this.emptySpaceItem1.Size = new System.Drawing.Size(512, 40);
		this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem1.Control = this.btnActualiser;
		this.layoutControlItem1.Location = new System.Drawing.Point(632, 0);
		this.layoutControlItem1.MaxSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem1.MinSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem1.Name = "layoutControlItem1";
		this.layoutControlItem1.Size = new System.Drawing.Size(120, 40);
		this.layoutControlItem1.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem1.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem1.TextVisible = false;
		this.layoutControlItem2.Control = this.btnAnnuler;
		this.layoutControlItem2.Location = new System.Drawing.Point(873, 0);
		this.layoutControlItem2.MaxSize = new System.Drawing.Size(119, 40);
		this.layoutControlItem2.MinSize = new System.Drawing.Size(119, 40);
		this.layoutControlItem2.Name = "layoutControlItem2";
		this.layoutControlItem2.Size = new System.Drawing.Size(119, 40);
		this.layoutControlItem2.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem2.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem2.TextVisible = false;
		this.layoutControlItem3.Control = this.btnValider;
		this.layoutControlItem3.Location = new System.Drawing.Point(752, 0);
		this.layoutControlItem3.MaxSize = new System.Drawing.Size(121, 40);
		this.layoutControlItem3.MinSize = new System.Drawing.Size(121, 40);
		this.layoutControlItem3.Name = "layoutControlItem3";
		this.layoutControlItem3.Size = new System.Drawing.Size(121, 40);
		this.layoutControlItem3.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem3.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem3.TextVisible = false;
		this.layoutControlItem5.Control = this.dropDownButton1;
		this.layoutControlItem5.Location = new System.Drawing.Point(512, 0);
		this.layoutControlItem5.MaxSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem5.MinSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem5.Name = "layoutControlItem5";
		this.layoutControlItem5.Size = new System.Drawing.Size(120, 40);
		this.layoutControlItem5.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem5.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem5.TextVisible = false;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(992, 747);
		base.Controls.Add(this.layoutControl1);
		base.Controls.Add(this.barDockControlLeft);
		base.Controls.Add(this.barDockControlRight);
		base.Controls.Add(this.barDockControlBottom);
		base.Controls.Add(this.barDockControlTop);
		base.IconOptions.ShowIcon = false;
		base.Name = "FrmSelectLigneDeclarationRetenuTej";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Retenue à la source";
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.popupMenuExport).EndInit();
		((System.ComponentModel.ISupportInitialize)this.barManager1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gc).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gv).EndInit();
		((System.ComponentModel.ISupportInitialize)this.Root).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}

	private void Initialize()
	{
		InitGrid();
	}

	private void InitGrid()
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
		_repoCaisse = new RepositoryItemGridLookUpEdit();
		_repoCaisse.View.Columns.AddVisible("Code", "Code");
		_repoCaisse.View.Columns.AddVisible("Designation", "Intitulé");
		_repoCaisse.ValueMember = "No";
		_repoCaisse.DisplayMember = "Designation";
		_repoCaisse.NullText = string.Empty;
		_repoCaisse.DataSource = _controller.GetAllCaisse();
		new RepositoryItemGridLookUpEdit
		{
			DataSource = _controller.GetAllDesignationDocument(),
			ValueMember = "No",
			DisplayMember = "Intitule",
			NullText = "",
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
					},
					new GridColumn
					{
						Caption = "Nature",
						FieldName = "NatureOperation",
						Visible = true
					}
				}
			}
		};
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
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Récupéré", true, 0));
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Non récupéré", false));
		repositoryItemImageComboBox2.SmallImages = new ImageCollection(allowModifyImages: false)
		{
			Images = { (Image)Resources.recuperer_16 }
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
		RepositoryItemImageComboBox repositoryItemImageComboBox3 = new RepositoryItemImageComboBox();
		repositoryItemImageComboBox3.Items.Add(new ImageComboBoxItem("Erreur", true, 0));
		repositoryItemImageComboBox3.SmallImages = new ImageCollection(allowModifyImages: false)
		{
			Images = { (Image)Resources.delete }
		};
		repositoryItemImageComboBox3.GlyphAlignment = HorzAlignment.Far;
		repositoryItemImageComboBox3.ShowToolTipForTrimmedText = DefaultBoolean.True;
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
			Visible = true
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
			Visible = true
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
			ColumnEdit = repositoryItemImageComboBox2,
			Caption = "R ",
			FieldName = "IsRecuperer",
			Visible = true,
			MinWidth = 20,
			MaxWidth = 20,
			ToolTip = "Récupéré"
		};
		GridColumn gridColumn29 = new GridColumn
		{
			Caption = "Date récupération",
			FieldName = "DateRecuperation",
			Visible = true
		};
		GridColumn gridColumn30 = new GridColumn
		{
			Caption = "E",
			FieldName = "HasError",
			Visible = true,
			ToolTip = "Erreur",
			ColumnEdit = repositoryItemImageComboBox3,
			MinWidth = 20,
			MaxWidth = 20
		};
		gv.Columns.AddRange(new GridColumn[30]
		{
			gridColumn, gridColumn2, gridColumn3, gridColumn4, gridColumn5, gridColumn6, gridColumn7, gridColumn8, gridColumn9, gridColumn10,
			gridColumn11, gridColumn12, gridColumn13, gridColumn14, gridColumn15, gridColumn16, gridColumn17, gridColumn18, gridColumn19, gridColumn20,
			gridColumn21, gridColumn22, gridColumn23, gridColumn24, gridColumn25, gridColumn26, gridColumn27, gridColumn28, gridColumn29, gridColumn30
		});
		gv.Init();
		gv.Tag = new Guid("{35EBBAFF-CD52-4B58-9F86-0DAA5ED9628E}");
		gv.BestFitColumns();
		gv.OptionsFilter.DefaultFilterEditorView = FilterEditorViewMode.VisualAndText;
		gv.OptionsView.ShowFilterPanelMode = ShowFilterPanelMode.Default;
		gv.OptionsSelection.MultiSelect = true;
		gv.OptionsSelection.MultiSelectMode = GridMultiSelectMode.CheckBoxRowSelect;
		gv.OptionsSelection.CheckBoxSelectorColumnWidth = 30;
		gv.Appearance.HideSelectionRow.Assign(gv.Appearance.FocusedRow);
		gv.Columns.Cast<GridColumn>().ToList().ForEach(delegate(GridColumn x)
		{
			x.OptionsColumn.AllowEdit = false;
		});
		gv.OptionsView.ShowFooter = true;
	}
}
