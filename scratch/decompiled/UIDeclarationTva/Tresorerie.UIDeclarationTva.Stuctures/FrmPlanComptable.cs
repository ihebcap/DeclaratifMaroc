using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using DevExpress.XtraLayout.Utils;
using Tresorerie.UICommun.Components;
using Tresorerie.UIDeclarationTva.Properties;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class FrmPlanComptable : RibbonForm, IGridViewForm
{
	private readonly PlanComptableController _controller;

	private IContainer components;

	private RibbonControl ribbon;

	private RibbonPage ribbonPage1;

	private RibbonPageGroup ribbonPageGroup1;

	private LayoutControl layoutControl1;

	private GridControl gc;

	private GridView gv;

	private LayoutControlGroup Root;

	private LayoutControlItem layoutControlItem1;

	private BarSubItem bbiExport;

	private BarCheckItem btnAppliquer;

	private BarButtonItem btnReinitialiser;

	private BarCheckItem btnLigneFilter;

	private BarSubItem bbiOption;

	private BarButtonItem bbiExportExcel;

	private BarButtonItem bbiExportCSV;

	private BarButtonItem bbiExportText;

	private BarButtonItem bbiExportPdf;

	private BarButtonItem bbiExportPrint;

	private BarCheckItem bbiZoneGroupement;

	private BarCheckItem bbiZoneRecherche;

	private BarButtonItem btnFilter;

	private SimpleButton btnActualiser;

	private EmptySpaceItem emptySpaceItem1;

	private LayoutControlItem layoutControlItem2;

	public GridView View => gv;

	private FrmPlanComptable()
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
		btnActualiser.Click += Actualiser;
	}

	public FrmPlanComptable(PlanComptableController controller)
		: this()
	{
		_controller = controller ?? throw new ArgumentNullException("controller");
		InitGrid();
		Actualiser();
	}

	private void Actualiser(object sender = null, EventArgs e = null)
	{
		gc.DataSource = _controller.GetAll();
	}

	public void AfficherFilter(object sender, ItemClickEventArgs e)
	{
		if (_controller.IsLicenceGratuit())
		{
			gv.ShowFilterEditor(gv.Columns[0]);
		}
	}

	public void LigneFilter(object sender, ItemClickEventArgs e)
	{
		gv.OptionsView.ShowAutoFilterRow = btnLigneFilter.Checked;
	}

	private void AppliquerFilter(object sender, EventArgs e)
	{
		if (_controller.IsLicenceGratuit())
		{
			gv.ActiveFilterEnabled = btnAppliquer.Checked;
		}
	}

	private void ReinitialiserFilter(object sender, EventArgs e)
	{
		if (_controller.IsLicenceGratuit())
		{
			gv.ActiveFilter.Clear();
		}
	}

	private void Grouper(object sender, EventArgs e)
	{
		if (_controller.IsLicenceGratuit())
		{
			gv.OptionsView.ShowGroupPanel = bbiZoneGroupement.Checked;
		}
	}

	private void Chercher(object sender, EventArgs e)
	{
		gv.OptionsFind.AlwaysVisible = bbiZoneRecherche.Checked;
	}

	private void ExportToCsv(object sender, EventArgs e)
	{
		_controller.AuthorisationExport();
		gv.ExportToCSV(Text);
	}

	private void ExportToExcel(object sender, EventArgs e)
	{
		_controller.AuthorisationExport();
		gv.ExportToEXCEL(Text);
	}

	private void ExportToText(object sender, EventArgs e)
	{
		_controller.AuthorisationExport();
		gv.ExportToTEXT(Text);
	}

	private void ExportToPdf(object sender, EventArgs e)
	{
		_controller.AuthorisationExport();
		gv.ExportToPDF(Text);
	}

	private void Preview(object sender, EventArgs e)
	{
		if (_controller.IsLicenceGratuit())
		{
			_controller.AuthorisationExport();
			gv.ShowRibbonPrintPreview();
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
		this.ribbon = new DevExpress.XtraBars.Ribbon.RibbonControl();
		this.bbiExport = new DevExpress.XtraBars.BarSubItem();
		this.bbiExportExcel = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportCSV = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportText = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportPdf = new DevExpress.XtraBars.BarButtonItem();
		this.bbiExportPrint = new DevExpress.XtraBars.BarButtonItem();
		this.btnAppliquer = new DevExpress.XtraBars.BarCheckItem();
		this.btnReinitialiser = new DevExpress.XtraBars.BarButtonItem();
		this.btnLigneFilter = new DevExpress.XtraBars.BarCheckItem();
		this.bbiOption = new DevExpress.XtraBars.BarSubItem();
		this.bbiZoneGroupement = new DevExpress.XtraBars.BarCheckItem();
		this.bbiZoneRecherche = new DevExpress.XtraBars.BarCheckItem();
		this.btnFilter = new DevExpress.XtraBars.BarButtonItem();
		this.ribbonPage1 = new DevExpress.XtraBars.Ribbon.RibbonPage();
		this.ribbonPageGroup1 = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
		this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
		this.btnActualiser = new DevExpress.XtraEditors.SimpleButton();
		this.gc = new DevExpress.XtraGrid.GridControl();
		this.gv = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
		this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.layoutControlItem2 = new DevExpress.XtraLayout.LayoutControlItem();
		((System.ComponentModel.ISupportInitialize)this.ribbon).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.gc).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gv).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.Root).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).BeginInit();
		base.SuspendLayout();
		this.ribbon.ExpandCollapseItem.Id = 0;
		this.ribbon.Items.AddRange(new DevExpress.XtraBars.BarItem[15]
		{
			this.ribbon.ExpandCollapseItem,
			this.ribbon.SearchEditItem,
			this.bbiExport,
			this.btnAppliquer,
			this.btnReinitialiser,
			this.btnLigneFilter,
			this.bbiOption,
			this.bbiExportExcel,
			this.bbiExportCSV,
			this.bbiExportText,
			this.bbiExportPdf,
			this.bbiExportPrint,
			this.bbiZoneGroupement,
			this.bbiZoneRecherche,
			this.btnFilter
		});
		this.ribbon.Location = new System.Drawing.Point(0, 0);
		this.ribbon.MaxItemId = 14;
		this.ribbon.Name = "ribbon";
		this.ribbon.Pages.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPage[1] { this.ribbonPage1 });
		this.ribbon.Size = new System.Drawing.Size(890, 162);
		this.bbiExport.Caption = "Exporter en";
		this.bbiExport.Id = 1;
		this.bbiExport.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_export_32;
		this.bbiExport.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[5]
		{
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiExportExcel, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiExportCSV, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiExportText, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiExportPdf, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiExportPrint, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph)
		});
		this.bbiExport.Name = "bbiExport";
		this.bbiExport.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.All;
		this.bbiExportExcel.Caption = "Excel";
		this.bbiExportExcel.Id = 6;
		this.bbiExportExcel.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttoxlsx_16x16;
		this.bbiExportExcel.Name = "bbiExportExcel";
		this.bbiExportCSV.Caption = "CSV";
		this.bbiExportCSV.Id = 7;
		this.bbiExportCSV.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttocsv_16x16;
		this.bbiExportCSV.Name = "bbiExportCSV";
		this.bbiExportText.Caption = "Text";
		this.bbiExportText.Id = 8;
		this.bbiExportText.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttotxt_16x16;
		this.bbiExportText.Name = "bbiExportText";
		this.bbiExportPdf.Caption = "PDF";
		this.bbiExportPdf.Id = 9;
		this.bbiExportPdf.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.exporttopdf_16x16;
		this.bbiExportPdf.Name = "bbiExportPdf";
		this.bbiExportPrint.Caption = "Aperçu";
		this.bbiExportPrint.Id = 10;
		this.bbiExportPrint.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_look_16;
		this.bbiExportPrint.Name = "bbiExportPrint";
		this.btnAppliquer.Caption = "Appliquer";
		this.btnAppliquer.Id = 2;
		this.btnAppliquer.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.apply_16x16;
		this.btnAppliquer.Name = "btnAppliquer";
		this.btnAppliquer.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.SmallWithText;
		this.btnReinitialiser.Caption = "Réinitialiser";
		this.btnReinitialiser.Id = 3;
		this.btnReinitialiser.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_cancel_16;
		this.btnReinitialiser.Name = "btnReinitialiser";
		this.btnReinitialiser.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.SmallWithText;
		this.btnLigneFilter.BindableChecked = true;
		this.btnLigneFilter.Caption = "Ligne filtre";
		this.btnLigneFilter.Checked = true;
		this.btnLigneFilter.Id = 4;
		this.btnLigneFilter.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.chartsshowlegend_16x16;
		this.btnLigneFilter.Name = "btnLigneFilter";
		this.btnLigneFilter.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.SmallWithText;
		this.bbiOption.Caption = "Option";
		this.bbiOption.Id = 5;
		this.bbiOption.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_Option_32;
		this.bbiOption.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[2]
		{
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiZoneGroupement, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph),
			new DevExpress.XtraBars.LinkPersistInfo(DevExpress.XtraBars.BarLinkUserDefines.PaintStyle, this.bbiZoneRecherche, DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph)
		});
		this.bbiOption.Name = "bbiOption";
		this.bbiOption.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.All;
		this.bbiZoneGroupement.BindableChecked = true;
		this.bbiZoneGroupement.Caption = "Zone de groupement";
		this.bbiZoneGroupement.Checked = true;
		this.bbiZoneGroupement.Id = 11;
		this.bbiZoneGroupement.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.New_Group_16x16;
		this.bbiZoneGroupement.Name = "bbiZoneGroupement";
		this.bbiZoneRecherche.BindableChecked = true;
		this.bbiZoneRecherche.Caption = "Zone de recherche";
		this.bbiZoneRecherche.Checked = true;
		this.bbiZoneRecherche.Id = 12;
		this.bbiZoneRecherche.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_look_16;
		this.bbiZoneRecherche.Name = "bbiZoneRecherche";
		this.btnFilter.Caption = "Filtre";
		this.btnFilter.Id = 13;
		this.btnFilter.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_filtre_32;
		this.btnFilter.Name = "btnFilter";
		this.btnFilter.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.All;
		this.ribbonPage1.Groups.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPageGroup[1] { this.ribbonPageGroup1 });
		this.ribbonPage1.Name = "ribbonPage1";
		this.ribbonPage1.Text = "Déclaratif";
		this.ribbonPageGroup1.ItemLinks.Add(this.bbiExport);
		this.ribbonPageGroup1.ItemLinks.Add(this.btnFilter);
		this.ribbonPageGroup1.ItemLinks.Add(this.btnAppliquer);
		this.ribbonPageGroup1.ItemLinks.Add(this.btnReinitialiser);
		this.ribbonPageGroup1.ItemLinks.Add(this.btnLigneFilter);
		this.ribbonPageGroup1.ItemLinks.Add(this.bbiOption);
		this.ribbonPageGroup1.Name = "ribbonPageGroup1";
		this.ribbonPageGroup1.Text = "Export";
		this.layoutControl1.Controls.Add(this.btnActualiser);
		this.layoutControl1.Controls.Add(this.gc);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 162);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(890, 414);
		this.layoutControl1.TabIndex = 1;
		this.layoutControl1.Text = "layoutControl1";
		this.btnActualiser.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_refresh4_32;
		this.btnActualiser.Location = new System.Drawing.Point(792, 2);
		this.btnActualiser.Name = "btnActualiser";
		this.btnActualiser.Size = new System.Drawing.Size(96, 38);
		this.btnActualiser.StyleController = this.layoutControl1;
		this.btnActualiser.TabIndex = 5;
		this.btnActualiser.Text = "Actualiser";
		this.gc.Location = new System.Drawing.Point(2, 44);
		this.gc.MainView = this.gv;
		this.gc.MenuManager = this.ribbon;
		this.gc.Name = "gc";
		this.gc.Size = new System.Drawing.Size(886, 368);
		this.gc.TabIndex = 4;
		this.gc.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gv });
		this.gv.GridControl = this.gc;
		this.gv.Name = "gv";
		this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
		this.Root.GroupBordersVisible = false;
		this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[3] { this.layoutControlItem1, this.emptySpaceItem1, this.layoutControlItem2 });
		this.Root.Name = "Root";
		this.Root.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.Root.Size = new System.Drawing.Size(890, 414);
		this.Root.TextVisible = false;
		this.layoutControlItem1.Control = this.gc;
		this.layoutControlItem1.Location = new System.Drawing.Point(0, 42);
		this.layoutControlItem1.Name = "layoutControlItem1";
		this.layoutControlItem1.Size = new System.Drawing.Size(890, 372);
		this.layoutControlItem1.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem1.TextVisible = false;
		this.emptySpaceItem1.AllowHotTrack = false;
		this.emptySpaceItem1.Location = new System.Drawing.Point(0, 0);
		this.emptySpaceItem1.Name = "emptySpaceItem1";
		this.emptySpaceItem1.Size = new System.Drawing.Size(790, 42);
		this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem2.Control = this.btnActualiser;
		this.layoutControlItem2.Location = new System.Drawing.Point(790, 0);
		this.layoutControlItem2.MaxSize = new System.Drawing.Size(100, 42);
		this.layoutControlItem2.MinSize = new System.Drawing.Size(100, 42);
		this.layoutControlItem2.Name = "layoutControlItem2";
		this.layoutControlItem2.Size = new System.Drawing.Size(100, 42);
		this.layoutControlItem2.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem2.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem2.TextVisible = false;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(890, 576);
		base.Controls.Add(this.layoutControl1);
		base.Controls.Add(this.ribbon);
		base.Name = "FrmPlanComptable";
		this.Ribbon = this.ribbon;
		this.Text = "Plan Comptable";
		((System.ComponentModel.ISupportInitialize)this.ribbon).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.gc).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gv).EndInit();
		((System.ComponentModel.ISupportInitialize)this.Root).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}

	private void InitGrid()
	{
		GridColumn gridColumn = new GridColumn
		{
			Caption = "Numéro",
			FieldName = "Numero",
			Visible = true
		};
		GridColumn gridColumn2 = new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "Intitule",
			Visible = true
		};
		GridColumn gridColumn3 = new GridColumn
		{
			Caption = "Type",
			FieldName = "TypeNo",
			Visible = true
		};
		GridColumn gridColumn4 = new GridColumn
		{
			Caption = "Nature",
			FieldName = "Nature",
			Visible = true
		};
		GridColumn gridColumn5 = new GridColumn
		{
			Caption = "Type report",
			FieldName = "Report",
			Visible = true
		};
		gv.Columns.AddRange(new GridColumn[5] { gridColumn, gridColumn2, gridColumn3, gridColumn4, gridColumn5 });
		gv.Init();
		gv.OptionsBehavior.Editable = false;
		gv.OptionsView.ShowFilterPanelMode = ShowFilterPanelMode.Default;
		gv.OptionsFilter.DefaultFilterEditorView = FilterEditorViewMode.VisualAndText;
		gv.OptionsFind.AlwaysVisible = true;
		gv.BestFitColumns();
		gv.OptionsView.ShowFooter = true;
		gv.Tag = new Guid("{9FB37FD9-1A94-5A8A-A477-E97D4B819EFC}");
	}
}
