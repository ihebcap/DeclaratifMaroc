using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.Utils;
using DevExpress.Utils.Menu;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Mask;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Menu;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;
using DevExpress.XtraLayout;
using DevExpress.XtraLayout.Utils;
using Tresorerie.UICommun.Components;
using Tresorerie.UIDeclarationTva.Properties;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.DeclarationDelaisPaiement;

public class FrmControleLigneDelaisPaiement : RibbonForm, IGridViewForm
{
	private readonly LigneControleDelaisPaiementController _controller;

	private LigneControleDelaisPaiementFiltreView _filtreView;

	private IContainer components;

	private RibbonControl ribbon;

	private RibbonPage ribbonPage1;

	private RibbonPageGroup ribbonPageGroup1;

	private LayoutControl layoutControl1;

	private GridControl gc;

	private GridView gv;

	private DateEdit txtDateDebut;

	private DateEdit txtDateFin;

	private LayoutControlGroup Root;

	private LayoutControlItem layoutControlItem1;

	private LayoutControlItem layoutControlItem2;

	private LayoutControlItem layoutControlItem3;

	private EmptySpaceItem emptySpaceItem1;

	private BarSubItem barSubItem1;

	private BarButtonItem bbiExportExcel;

	private BarButtonItem bbiExportCSV;

	private BarButtonItem bbiExportText;

	private BarButtonItem bbiExportPdf;

	private BarButtonItem bbiExportPrint;

	private BarButtonItem btnFilter;

	private BarCheckItem btnAppliquer;

	private BarButtonItem btnReinitialiser;

	private BarCheckItem btnLigneFilter;

	private BarSubItem bbiOption;

	private BarCheckItem bbiZoneGroupement;

	private BarCheckItem bbiZoneRecherche;

	public GridView View => gv;

	private FrmControleLigneDelaisPaiement()
	{
		InitializeComponent();
		btnFilter.ItemClick += AfficherFilter;
		btnLigneFilter.CheckedChanged += LigneFilter;
		btnAppliquer.CheckedChanged += AppliquerFilter;
		btnReinitialiser.ItemClick += ReinitialiserFilter;
		bbiZoneGroupement.CheckedChanged += Grouper;
		bbiZoneRecherche.CheckedChanged += Chercher;
		bbiExportCSV.ItemClick += ExportToCsv;
		bbiExportExcel.ItemClick += ExportToExcel;
		bbiExportText.ItemClick += ExportToText;
		bbiExportPdf.ItemClick += ExportToPdf;
		bbiExportPrint.ItemClick += Preview;
		txtDateDebut.KeyDown += EnterEvent;
		txtDateFin.KeyDown += EnterEvent;
	}

	public FrmControleLigneDelaisPaiement(LigneControleDelaisPaiementController controller)
		: this()
	{
		_controller = controller ?? throw new ArgumentNullException("controller");
		Initialize();
		string defaultDeviseFormat = _controller.GetDefaultDeviseFormat();
		this.SetDecimalFormat(defaultDeviseFormat);
		Binding();
	}

	private void Binding()
	{
		_filtreView = _filtreView ?? new LigneControleDelaisPaiementFiltreView();
		txtDateDebut.DataBindings.Clear();
		txtDateDebut.DataBindings.Add("EditValue", _filtreView, "DateDu", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtDateFin.DataBindings.Clear();
		txtDateFin.DataBindings.Add("EditValue", _filtreView, "DateAu", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
	}

	private void EnterEvent(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Return)
		{
			Actualiser();
		}
	}

	private void Actualiser(object sender = null, EventArgs e = null)
	{
		gc.DataSource = null;
		gc.DataSource = _controller.GetAll(_filtreView);
	}

	public void AfficherFilter(object sender, ItemClickEventArgs e)
	{
		gv.ShowFilterEditor(gv.Columns[0]);
	}

	public void LigneFilter(object sender, ItemClickEventArgs e)
	{
		gv.OptionsView.ShowAutoFilterRow = btnLigneFilter.Checked;
	}

	private void AppliquerFilter(object sender, EventArgs e)
	{
		gv.ActiveFilterEnabled = btnAppliquer.Checked;
	}

	private void ReinitialiserFilter(object sender, EventArgs e)
	{
		gv.ActiveFilter.Clear();
	}

	private void Grouper(object sender, EventArgs e)
	{
		gv.OptionsView.ShowGroupPanel = bbiZoneGroupement.Checked;
	}

	private void Chercher(object sender, EventArgs e)
	{
		gv.OptionsFind.AlwaysVisible = bbiZoneRecherche.Checked;
	}

	private void ExportToCsv(object sender, EventArgs e)
	{
		gv.ExportToCSV(Text);
	}

	private void ExportToExcel(object sender, EventArgs e)
	{
		gv.ExportToEXCEL(Text);
	}

	private void ExportToText(object sender, EventArgs e)
	{
		gv.ExportToTEXT(Text);
	}

	private void ExportToPdf(object sender, EventArgs e)
	{
		gv.ExportToPDF(Text);
	}

	private void Preview(object sender, EventArgs e)
	{
		gv.ShowRibbonPrintPreview();
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
		this.ribbon = new DevExpress.XtraBars.Ribbon.RibbonControl();
		this.barSubItem1 = new DevExpress.XtraBars.BarSubItem();
		this.bbiExportExcel = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportCSV = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportText = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportPdf = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportPrint = new DevExpress.XtraBars.BarButtonItem();
		this.btnFilter = new DevExpress.XtraBars.BarButtonItem();
		this.btnAppliquer = new DevExpress.XtraBars.BarCheckItem();
		this.btnReinitialiser = new DevExpress.XtraBars.BarButtonItem();
		this.btnLigneFilter = new DevExpress.XtraBars.BarCheckItem();
		this.bbiOption = new DevExpress.XtraBars.BarSubItem();
		this.bbiZoneGroupement = new DevExpress.XtraBars.BarCheckItem();
		this.bbiZoneRecherche = new DevExpress.XtraBars.BarCheckItem();
		this.ribbonPage1 = new DevExpress.XtraBars.Ribbon.RibbonPage();
		this.ribbonPageGroup1 = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
		this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
		this.gc = new DevExpress.XtraGrid.GridControl();
		this.gv = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtDateDebut = new DevExpress.XtraEditors.DateEdit();
		this.txtDateFin = new DevExpress.XtraEditors.DateEdit();
		this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem2 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem3 = new DevExpress.XtraLayout.LayoutControlItem();
		this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
		((System.ComponentModel.ISupportInitialize)this.ribbon).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.gc).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gv).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDebut.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDebut.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateFin.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateFin.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.Root).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).BeginInit();
		base.SuspendLayout();
		this.ribbon.ExpandCollapseItem.Id = 0;
		this.ribbon.Items.AddRange(new DevExpress.XtraBars.BarItem[15]
		{
			this.ribbon.ExpandCollapseItem,
			this.ribbon.SearchEditItem,
			this.barSubItem1,
			this.bbiExportExcel,
			this.bbiExportCSV,
			this.bbiExportText,
			this.bbiExportPdf,
			this.bbiExportPrint,
			this.btnFilter,
			this.btnAppliquer,
			this.btnReinitialiser,
			this.btnLigneFilter,
			this.bbiOption,
			this.bbiZoneGroupement,
			this.bbiZoneRecherche
		});
		this.ribbon.Location = new System.Drawing.Point(0, 0);
		this.ribbon.MaxItemId = 14;
		this.ribbon.Name = "ribbon";
		this.ribbon.Pages.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPage[1] { this.ribbonPage1 });
		this.ribbon.Size = new System.Drawing.Size(1063, 162);
		this.barSubItem1.Caption = "Exporter en";
		this.barSubItem1.Id = 1;
		this.barSubItem1.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_export_32;
		this.barSubItem1.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[5]
		{
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiExportExcel, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiExportCSV, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiExportText, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiExportPdf, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiExportPrint, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph)
		});
		this.barSubItem1.Name = "barSubItem1";
		this.barSubItem1.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.All;
		this.bbiExportExcel.Caption = "Excel";
		this.bbiExportExcel.Id = 2;
		this.bbiExportExcel.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttoxlsx_16x16;
		this.bbiExportExcel.Name = "bbiExportExcel";
		this.bbiExportCSV.Caption = "CSV";
		this.bbiExportCSV.Id = 3;
		this.bbiExportCSV.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttocsv_16x16;
		this.bbiExportCSV.Name = "bbiExportCSV";
		this.bbiExportText.Caption = "Text";
		this.bbiExportText.Id = 4;
		this.bbiExportText.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttotxt_16x16;
		this.bbiExportText.Name = "bbiExportText";
		this.bbiExportPdf.Caption = "PDF";
		this.bbiExportPdf.Id = 5;
		this.bbiExportPdf.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttopdf_16x16;
		this.bbiExportPdf.Name = "bbiExportPdf";
		this.bbiExportPrint.Caption = "Aperçu";
		this.bbiExportPrint.Id = 6;
		this.bbiExportPrint.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_look_16;
		this.bbiExportPrint.Name = "bbiExportPrint";
		this.btnFilter.Caption = "Filtre";
		this.btnFilter.Id = 7;
		this.btnFilter.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_filtre_32;
		this.btnFilter.Name = "btnFilter";
		this.btnFilter.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.All;
		this.btnAppliquer.Caption = "Appliquer";
		this.btnAppliquer.Id = 8;
		this.btnAppliquer.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.apply_16x16;
		this.btnAppliquer.Name = "btnAppliquer";
		this.btnAppliquer.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.SmallWithText;
		this.btnReinitialiser.Caption = "Réinitialiser";
		this.btnReinitialiser.Id = 9;
		this.btnReinitialiser.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.delete_16x16;
		this.btnReinitialiser.Name = "btnReinitialiser";
		this.btnReinitialiser.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.SmallWithText;
		this.btnLigneFilter.Caption = "Ligne filtre";
		this.btnLigneFilter.Id = 10;
		this.btnLigneFilter.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.chartsshowlegend_16x16;
		this.btnLigneFilter.Name = "btnLigneFilter";
		this.btnLigneFilter.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.SmallWithText;
		this.bbiOption.Caption = "Option";
		this.bbiOption.Id = 11;
		this.bbiOption.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_Option_32;
		this.bbiOption.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[2]
		{
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiZoneGroupement, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiZoneRecherche, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph)
		});
		this.bbiOption.Name = "bbiOption";
		this.bbiOption.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.All;
		this.bbiZoneGroupement.Caption = "Zone de groupement";
		this.bbiZoneGroupement.Id = 12;
		this.bbiZoneGroupement.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.New_Group_16x16;
		this.bbiZoneGroupement.Name = "bbiZoneGroupement";
		this.bbiZoneRecherche.Caption = "Zone de recherche";
		this.bbiZoneRecherche.Id = 13;
		this.bbiZoneRecherche.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_look_16;
		this.bbiZoneRecherche.Name = "bbiZoneRecherche";
		this.ribbonPage1.Groups.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPageGroup[1] { this.ribbonPageGroup1 });
		this.ribbonPage1.Name = "ribbonPage1";
		this.ribbonPage1.Text = "Déclaratif";
		this.ribbonPageGroup1.ItemLinks.Add(this.barSubItem1);
		this.ribbonPageGroup1.ItemLinks.Add(this.btnFilter);
		this.ribbonPageGroup1.ItemLinks.Add(this.btnAppliquer);
		this.ribbonPageGroup1.ItemLinks.Add(this.btnReinitialiser);
		this.ribbonPageGroup1.ItemLinks.Add(this.btnLigneFilter);
		this.ribbonPageGroup1.ItemLinks.Add(this.bbiOption);
		this.ribbonPageGroup1.Name = "ribbonPageGroup1";
		this.ribbonPageGroup1.Text = "Export";
		this.layoutControl1.Controls.Add(this.gc);
		this.layoutControl1.Controls.Add(this.txtDateDebut);
		this.layoutControl1.Controls.Add(this.txtDateFin);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 162);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(1063, 476);
		this.layoutControl1.TabIndex = 1;
		this.layoutControl1.Text = "layoutControl1";
		this.gc.Location = new System.Drawing.Point(2, 50);
		this.gc.MainView = this.gv;
		this.gc.MenuManager = this.ribbon;
		this.gc.Name = "gc";
		this.gc.Size = new System.Drawing.Size(1059, 424);
		this.gc.TabIndex = 4;
		this.gc.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gv });
		this.gv.GridControl = this.gc;
		this.gv.Name = "gv";
		this.txtDateDebut.EditValue = null;
		this.txtDateDebut.Location = new System.Drawing.Point(82, 2);
		this.txtDateDebut.MenuManager = this.ribbon;
		this.txtDateDebut.Name = "txtDateDebut";
		this.txtDateDebut.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDateDebut.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDateDebut.Properties.Mask.EditMask = "ddMMyy";
		this.txtDateDebut.Properties.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.DateTimeAdvancingCaret;
		this.txtDateDebut.Size = new System.Drawing.Size(166, 20);
		this.txtDateDebut.StyleController = this.layoutControl1;
		this.txtDateDebut.TabIndex = 5;
		this.txtDateFin.EditValue = null;
		this.txtDateFin.Location = new System.Drawing.Point(82, 26);
		this.txtDateFin.MenuManager = this.ribbon;
		this.txtDateFin.Name = "txtDateFin";
		this.txtDateFin.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDateFin.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDateFin.Properties.Mask.EditMask = "ddMMyy";
		this.txtDateFin.Properties.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.DateTimeAdvancingCaret;
		this.txtDateFin.Size = new System.Drawing.Size(166, 20);
		this.txtDateFin.StyleController = this.layoutControl1;
		this.txtDateFin.TabIndex = 6;
		this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
		this.Root.GroupBordersVisible = false;
		this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[4] { this.layoutControlItem1, this.layoutControlItem2, this.layoutControlItem3, this.emptySpaceItem1 });
		this.Root.Name = "Root";
		this.Root.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.Root.Size = new System.Drawing.Size(1063, 476);
		this.Root.TextVisible = false;
		this.layoutControlItem1.Control = this.gc;
		this.layoutControlItem1.Location = new System.Drawing.Point(0, 48);
		this.layoutControlItem1.Name = "layoutControlItem1";
		this.layoutControlItem1.Size = new System.Drawing.Size(1063, 428);
		this.layoutControlItem1.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem1.TextVisible = false;
		this.layoutControlItem2.Control = this.txtDateDebut;
		this.layoutControlItem2.Location = new System.Drawing.Point(0, 0);
		this.layoutControlItem2.MaxSize = new System.Drawing.Size(250, 24);
		this.layoutControlItem2.MinSize = new System.Drawing.Size(250, 24);
		this.layoutControlItem2.Name = "layoutControlItem2";
		this.layoutControlItem2.Size = new System.Drawing.Size(250, 24);
		this.layoutControlItem2.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem2.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem2.Text = "Date du";
		this.layoutControlItem2.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem2.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem2.TextToControlDistance = 0;
		this.layoutControlItem3.Control = this.txtDateFin;
		this.layoutControlItem3.Location = new System.Drawing.Point(0, 24);
		this.layoutControlItem3.MaxSize = new System.Drawing.Size(250, 24);
		this.layoutControlItem3.MinSize = new System.Drawing.Size(250, 24);
		this.layoutControlItem3.Name = "layoutControlItem3";
		this.layoutControlItem3.Size = new System.Drawing.Size(250, 24);
		this.layoutControlItem3.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem3.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem3.Text = "Date au";
		this.layoutControlItem3.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem3.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem3.TextToControlDistance = 0;
		this.emptySpaceItem1.AllowHotTrack = false;
		this.emptySpaceItem1.Location = new System.Drawing.Point(250, 0);
		this.emptySpaceItem1.Name = "emptySpaceItem1";
		this.emptySpaceItem1.Size = new System.Drawing.Size(813, 48);
		this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(1063, 638);
		base.Controls.Add(this.layoutControl1);
		base.Controls.Add(this.ribbon);
		base.Name = "FrmControleLigneDelaisPaiement";
		this.Ribbon = this.ribbon;
		this.Text = "Calculs Dépassements";
		((System.ComponentModel.ISupportInitialize)this.ribbon).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.gc).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gv).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDebut.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDebut.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateFin.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateFin.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.Root).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}

	private void Initialize()
	{
		RepositoryItemGridLookUpEdit repositoryItemGridLookUpEdit = new RepositoryItemGridLookUpEdit();
		repositoryItemGridLookUpEdit.View.Columns.AddVisible("Code", "Code");
		repositoryItemGridLookUpEdit.View.Columns.AddVisible("Designation", "Intitulé");
		repositoryItemGridLookUpEdit.ValueMember = "No";
		repositoryItemGridLookUpEdit.DisplayMember = "Designation";
		repositoryItemGridLookUpEdit.DataSource = _controller.GetAllMode();
		repositoryItemGridLookUpEdit.NullText = string.Empty;
		GridColumn gridColumn = new GridColumn
		{
			Caption = "N° échéance",
			FieldName = "EcheanceNumero",
			Visible = true
		};
		GridColumn gridColumn2 = new GridColumn
		{
			Caption = "Date",
			FieldName = "EcheanceDateDocument",
			Visible = true
		};
		GridColumn gridColumn3 = new GridColumn
		{
			Caption = "Echéance prévue",
			FieldName = "EcheanceDatePrevue",
			Visible = true
		};
		GridColumn gridColumn4 = new GridColumn
		{
			Caption = "Echéance légale",
			FieldName = "EcheanceLegale",
			Visible = true
		};
		GridColumn gridColumn5 = new GridColumn
		{
			Caption = "Montant échéance",
			FieldName = "EcheanceMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn6 = new GridColumn
		{
			Caption = "Solde échéance",
			FieldName = "EcheanceSolde",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn7 = new GridColumn
		{
			Caption = "Doc. info 1",
			FieldName = "DocumentInfoLibre1",
			Visible = false
		};
		GridColumn gridColumn8 = new GridColumn
		{
			Caption = "Doc. info 2",
			FieldName = "DocumentInfoLibre2",
			Visible = false
		};
		GridColumn gridColumn9 = new GridColumn
		{
			Caption = "Doc. info 3",
			FieldName = "DocumentInfoLibre3",
			Visible = false
		};
		GridColumn gridColumn10 = new GridColumn
		{
			Caption = "Doc. info 4",
			FieldName = "DocumentInfoLibre4",
			Visible = false
		};
		GridColumn gridColumn11 = new GridColumn
		{
			Caption = "Devise",
			FieldName = "EcheanceDeviseCode",
			Visible = true
		};
		GridColumn gridColumn12 = new GridColumn
		{
			Caption = "Cours échéance",
			FieldName = "EcheanceCours",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn13 = new GridColumn
		{
			Caption = "N° règlement",
			FieldName = "ReglementNumero",
			Visible = true
		};
		GridColumn gridColumn14 = new GridColumn
		{
			Caption = "Date règlement",
			FieldName = "ReglementDate",
			Visible = true
		};
		GridColumn gridColumn15 = new GridColumn
		{
			Caption = "Echéance règlement",
			FieldName = "ReglementEcheance",
			Visible = true
		};
		GridColumn gridColumn16 = new GridColumn
		{
			Caption = "Montant règlement",
			FieldName = "ReglementMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn17 = new GridColumn
		{
			Caption = "Solde règlement",
			FieldName = "ReglementSolde",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn18 = new GridColumn
		{
			Caption = "Mode",
			FieldName = "ReglementModeNo",
			Visible = true,
			ColumnEdit = repositoryItemGridLookUpEdit
		};
		GridColumn gridColumn19 = new GridColumn
		{
			Caption = "N° pièce",
			FieldName = "ReglementPieceNumero",
			Visible = false
		};
		GridColumn gridColumn20 = new GridColumn
		{
			Caption = "Règ. info 1",
			FieldName = "ReglementInfoLibre1",
			Visible = false
		};
		GridColumn gridColumn21 = new GridColumn
		{
			Caption = "Règ. info 2",
			FieldName = "ReglementInfoLibre2",
			Visible = false
		};
		GridColumn gridColumn22 = new GridColumn
		{
			Caption = "Règ. info 3",
			FieldName = "ReglementInfoLibre3",
			Visible = false
		};
		GridColumn gridColumn23 = new GridColumn
		{
			Caption = "Règ. info 4",
			FieldName = "ReglementInfoLibre4",
			Visible = false
		};
		GridColumn gridColumn24 = new GridColumn
		{
			Caption = "Cours règlement",
			FieldName = "ReglementCours",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn25 = new GridColumn
		{
			Caption = "Tiers",
			FieldName = "TiersNumero",
			Visible = true
		};
		GridColumn gridColumn26 = new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "TiersIntitule",
			Visible = true
		};
		GridColumn gridColumn27 = new GridColumn
		{
			Caption = "Montant affectation",
			FieldName = "AffectationMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn28 = new GridColumn
		{
			Caption = "Dépassement(j)",
			FieldName = "Depassement",
			Visible = true
		};
		GridColumn gridColumn29 = new GridColumn
		{
			Caption = "P",
			FieldName = "IsReglementPoint",
			Visible = true,
			MinWidth = 20,
			MaxWidth = 20
		};
		GridColumn gridColumn30 = new GridColumn
		{
			Caption = "Date pointage",
			FieldName = "ReglementDatePointage",
			Visible = true
		};
		gv.Columns.AddRange(new GridColumn[30]
		{
			gridColumn25, gridColumn26, gridColumn, gridColumn2, gridColumn3, gridColumn4, gridColumn5, gridColumn6, gridColumn11, gridColumn12,
			gridColumn7, gridColumn8, gridColumn9, gridColumn10, gridColumn13, gridColumn14, gridColumn18, gridColumn19, gridColumn15, gridColumn16,
			gridColumn17, gridColumn29, gridColumn30, gridColumn20, gridColumn21, gridColumn22, gridColumn23, gridColumn24, gridColumn27, gridColumn28
		});
		gv.Init();
		gv.OptionsView.ShowAutoFilterRow = true;
		gv.OptionsSelection.MultiSelect = true;
		gv.OptionsDetail.EnableMasterViewMode = false;
		gv.OptionsFilter.DefaultFilterEditorView = FilterEditorViewMode.VisualAndText;
		gv.OptionsView.ShowFilterPanelMode = ShowFilterPanelMode.Default;
		gv.BestFitColumns();
		gv.OptionsView.ShowFooter = true;
		gv.Columns.Cast<GridColumn>().ToList().ForEach(delegate(GridColumn x)
		{
			x.OptionsColumn.AllowEdit = false;
		});
		gv.PopupMenuShowing += OnGridViewPopupMenuShowing;
	}

	private void ShowHideGridFooter(object sender, EventArgs e)
	{
		gv.OptionsView.ShowFooter = !gv.OptionsView.ShowFooter;
	}

	private void OnGridViewPopupMenuShowing(object sender, DevExpress.XtraGrid.Views.Grid.PopupMenuShowingEventArgs e)
	{
		GridViewMenu menu = e.Menu;
		GridHitInfo hitInfo = e.HitInfo;
		if (hitInfo.InColumnPanel)
		{
			string caption = string.Format("{0} le Pied de la Grille", gv.OptionsView.ShowFooter ? "Cacher" : "Afficher");
			menu?.Items.Add(new DXMenuItem(caption, ShowHideGridFooter));
		}
		else if (hitInfo.HitTest == GridHitTest.RowCell && hitInfo.InRow && hitInfo.InRowCell)
		{
			switch (hitInfo.RowHandle)
			{
			case -2147483646:
				break;
			case int.MinValue:
				break;
			case -2147483647:
				break;
			}
		}
	}
}
