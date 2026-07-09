using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DevExpress.Data;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Mask;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using DevExpress.XtraLayout.Utils;
using DevExpress.XtraSplashScreen;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.UICommun.Components;
using Tresorerie.UIDeclarationTva.Properties;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class FrmMouvementNonDeclareDeclarationTvaEncaissement : RibbonForm, IGridViewForm
{
	private readonly DeclarationTvaController _controller;

	private readonly OverlayTextPainter overlayLabel;

	private readonly OverlayImagePainter overlayButton;

	private CancellationTokenSource tokenSource;

	private IOverlaySplashScreenHandle handleValider;

	private DeclarationTvaEncaissementView _currentView;

	private IContainer components;

	private RibbonControl ribbon;

	private RibbonPage ribbonPage1;

	private RibbonPageGroup ribbonPageGroup1;

	private LayoutControl layoutControl1;

	private LayoutControlGroup Root;

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

	private GridControl gc;

	private GridView gv;

	private LayoutControlItem layoutControlItem4;

	private BarButtonItem btnActualiser;

	private RibbonPageGroup ribbonPageGroup2;

	private GridControl gcDeclaration;

	private GridView gvDeclaration;

	private SplitterItem splitterItem1;

	private LayoutControlItem layoutControlItem2;

	private DateEdit txtDateDu;

	private DateEdit txtDateAu;

	private LayoutControlItem layoutControlItem1;

	private LayoutControlItem layoutControlItem3;

	private LayoutControlItem layoutControlItem5;

	private EmptySpaceItem emptySpaceItem1;

	private GridLookUpEdit txtExercice;

	private GridView gridLookUpEdit1View;

	public GridView View => gv;

	private FrmMouvementNonDeclareDeclarationTvaEncaissement()
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
		btnActualiser.ItemClick += Actualiser;
		txtExercice.KeyDown += EnterEvent;
		txtExercice.EditValueChanged += ExerciceChanged;
		txtDateDu.KeyDown += EnterEvent;
		txtDateAu.KeyDown += EnterEvent;
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

	public FrmMouvementNonDeclareDeclarationTvaEncaissement(DeclarationTvaController controller)
		: this()
	{
		_controller = controller ?? throw new ArgumentNullException("controller");
		Initialize();
		_currentView = new DeclarationTvaEncaissementView();
		string defaultDeviseFormat = _controller.GetDefaultDeviseFormat();
		this.SetDecimalFormat(defaultDeviseFormat);
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

	private void Actualiser(object sender = null, EventArgs e = null)
	{
		gc.DataSource = null;
		gcDeclaration.DataSource = null;
		if (!CheckFiltre() || !(txtExercice.GetSelectedDataRow() is IErpExercice erpExercice))
		{
			return;
		}
		_currentView.Exercice = erpExercice.Annee;
		_currentView.DateDebut = txtDateDu.DateTime.Date;
		_currentView.DateFin = txtDateAu.DateTime.Date;
		try
		{
			handleValider = SplashScreenManager.ShowOverlayForm(this);
			DeclarationTvaSelectedEntityFiltreView filtreView = new DeclarationTvaSelectedEntityFiltreView
			{
				SelectedEntity = SelectedEntityFiltre.Tous
			};
			List<DeclarationTvaView> declaration = _controller.GetDeclaration(_currentView, filtreView);
			List<DeclarationTvaRegroupementView> totalDeclaration = _controller.GetTotalDeclaration(declaration);
			gc.DataSource = declaration;
			gcDeclaration.DataSource = totalDeclaration;
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

	private bool CheckFiltre()
	{
		txtExercice.ErrorText = "";
		txtDateDu.ErrorText = "";
		txtDateAu.ErrorText = "";
		if (!(txtExercice.GetSelectedDataRow() is IErpExercice erpExercice))
		{
			txtExercice.ErrorText = "Exercice obligatoire.";
			return false;
		}
		if (txtDateDu.DateTime.Date < erpExercice.Debut.Date || txtDateDu.DateTime.Date > erpExercice.Fin.Date)
		{
			txtDateDu.ErrorText = "La date début doit être inclut dans la pérode de l'exercice.";
			return false;
		}
		if (txtDateAu.DateTime.Date < erpExercice.Debut.Date || txtDateAu.DateTime.Date > erpExercice.Fin.Date)
		{
			txtDateAu.ErrorText = "La date fin doit être inclut dans la pérode de l'exercice.";
			return false;
		}
		if (txtDateAu.DateTime.Date < txtDateDu.DateTime.Date)
		{
			txtDateAu.ErrorText = "La date fin doit être supérieur ou égale à la date début.";
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
		txtDateDu.EditValue = erpExercice.Debut;
		txtDateAu.EditValue = erpExercice.Fin;
	}

	private void EnterEvent(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Return)
		{
			Actualiser();
		}
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
		this.btnActualiser = new DevExpress.XtraBars.BarButtonItem();
		this.ribbonPage1 = new DevExpress.XtraBars.Ribbon.RibbonPage();
		this.ribbonPageGroup1 = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
		this.ribbonPageGroup2 = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
		this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
		this.gcDeclaration = new DevExpress.XtraGrid.GridControl();
		this.gvDeclaration = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.gc = new DevExpress.XtraGrid.GridControl();
		this.gv = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtDateDu = new DevExpress.XtraEditors.DateEdit();
		this.txtDateAu = new DevExpress.XtraEditors.DateEdit();
		this.txtExercice = new DevExpress.XtraEditors.GridLookUpEdit();
		this.gridLookUpEdit1View = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem4 = new DevExpress.XtraLayout.LayoutControlItem();
		this.splitterItem1 = new DevExpress.XtraLayout.SplitterItem();
		this.layoutControlItem2 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem3 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem5 = new DevExpress.XtraLayout.LayoutControlItem();
		this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
		((System.ComponentModel.ISupportInitialize)this.ribbon).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.gcDeclaration).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gvDeclaration).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gc).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gv).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDu.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDu.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateAu.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateAu.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtExercice.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gridLookUpEdit1View).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.Root).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.splitterItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).BeginInit();
		base.SuspendLayout();
		this.ribbon.ExpandCollapseItem.Id = 0;
		this.ribbon.Items.AddRange(new DevExpress.XtraBars.BarItem[16]
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
			this.btnFilter,
			this.btnActualiser
		});
		this.ribbon.Location = new System.Drawing.Point(0, 0);
		this.ribbon.MaxItemId = 15;
		this.ribbon.Name = "ribbon";
		this.ribbon.Pages.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPage[1] { this.ribbonPage1 });
		this.ribbon.Size = new System.Drawing.Size(992, 162);
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
		this.btnActualiser.Caption = "Actualiser";
		this.btnActualiser.Id = 14;
		this.btnActualiser.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_refresh4_32;
		this.btnActualiser.Name = "btnActualiser";
		this.btnActualiser.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.All;
		this.ribbonPage1.Groups.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPageGroup[2] { this.ribbonPageGroup1, this.ribbonPageGroup2 });
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
		this.ribbonPageGroup2.ItemLinks.Add(this.btnActualiser);
		this.ribbonPageGroup2.Name = "ribbonPageGroup2";
		this.layoutControl1.Controls.Add(this.gcDeclaration);
		this.layoutControl1.Controls.Add(this.gc);
		this.layoutControl1.Controls.Add(this.txtDateDu);
		this.layoutControl1.Controls.Add(this.txtDateAu);
		this.layoutControl1.Controls.Add(this.txtExercice);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 162);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.OptionsCustomizationForm.DesignTimeCustomizationFormPositionAndSize = new System.Drawing.Rectangle(1217, 284, 650, 400);
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(992, 585);
		this.layoutControl1.TabIndex = 1;
		this.layoutControl1.Text = "layoutControl1";
		this.gcDeclaration.Location = new System.Drawing.Point(2, 449);
		this.gcDeclaration.MainView = this.gvDeclaration;
		this.gcDeclaration.MenuManager = this.ribbon;
		this.gcDeclaration.Name = "gcDeclaration";
		this.gcDeclaration.Size = new System.Drawing.Size(988, 134);
		this.gcDeclaration.TabIndex = 10;
		this.gcDeclaration.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gvDeclaration });
		this.gvDeclaration.GridControl = this.gcDeclaration;
		this.gvDeclaration.Name = "gvDeclaration";
		this.gc.Location = new System.Drawing.Point(2, 74);
		this.gc.MainView = this.gv;
		this.gc.MenuManager = this.ribbon;
		this.gc.Name = "gc";
		this.gc.Size = new System.Drawing.Size(988, 361);
		this.gc.TabIndex = 7;
		this.gc.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gv });
		this.gv.GridControl = this.gc;
		this.gv.Name = "gv";
		this.txtDateDu.EditValue = null;
		this.txtDateDu.Location = new System.Drawing.Point(77, 26);
		this.txtDateDu.MenuManager = this.ribbon;
		this.txtDateDu.Name = "txtDateDu";
		this.txtDateDu.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDateDu.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDateDu.Properties.Mask.EditMask = "ddMMyy";
		this.txtDateDu.Properties.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.DateTimeAdvancingCaret;
		this.txtDateDu.Size = new System.Drawing.Size(248, 20);
		this.txtDateDu.StyleController = this.layoutControl1;
		this.txtDateDu.TabIndex = 12;
		this.txtDateAu.EditValue = null;
		this.txtDateAu.Location = new System.Drawing.Point(77, 50);
		this.txtDateAu.MenuManager = this.ribbon;
		this.txtDateAu.Name = "txtDateAu";
		this.txtDateAu.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDateAu.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDateAu.Properties.Mask.EditMask = "ddMMyy";
		this.txtDateAu.Properties.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.DateTimeAdvancingCaret;
		this.txtDateAu.Size = new System.Drawing.Size(248, 20);
		this.txtDateAu.StyleController = this.layoutControl1;
		this.txtDateAu.TabIndex = 13;
		this.txtExercice.Location = new System.Drawing.Point(77, 2);
		this.txtExercice.MenuManager = this.ribbon;
		this.txtExercice.Name = "txtExercice";
		this.txtExercice.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtExercice.Properties.NullText = "";
		this.txtExercice.Properties.PopupView = this.gridLookUpEdit1View;
		this.txtExercice.Size = new System.Drawing.Size(248, 20);
		this.txtExercice.StyleController = this.layoutControl1;
		this.txtExercice.TabIndex = 11;
		this.gridLookUpEdit1View.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
		this.gridLookUpEdit1View.Name = "gridLookUpEdit1View";
		this.gridLookUpEdit1View.OptionsSelection.EnableAppearanceFocusedCell = false;
		this.gridLookUpEdit1View.OptionsView.ShowGroupPanel = false;
		this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
		this.Root.GroupBordersVisible = false;
		this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[7] { this.layoutControlItem4, this.splitterItem1, this.layoutControlItem2, this.layoutControlItem1, this.layoutControlItem3, this.layoutControlItem5, this.emptySpaceItem1 });
		this.Root.Name = "Root";
		this.Root.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.Root.Size = new System.Drawing.Size(992, 585);
		this.Root.TextVisible = false;
		this.layoutControlItem4.Control = this.gc;
		this.layoutControlItem4.Location = new System.Drawing.Point(0, 72);
		this.layoutControlItem4.Name = "layoutControlItem4";
		this.layoutControlItem4.Size = new System.Drawing.Size(992, 365);
		this.layoutControlItem4.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem4.TextVisible = false;
		this.splitterItem1.AllowHotTrack = true;
		this.splitterItem1.Inverted = true;
		this.splitterItem1.IsCollapsible = DevExpress.Utils.DefaultBoolean.True;
		this.splitterItem1.Location = new System.Drawing.Point(0, 437);
		this.splitterItem1.Name = "splitterItem1";
		this.splitterItem1.Size = new System.Drawing.Size(992, 10);
		this.layoutControlItem2.Control = this.gcDeclaration;
		this.layoutControlItem2.Location = new System.Drawing.Point(0, 447);
		this.layoutControlItem2.Name = "layoutControlItem2";
		this.layoutControlItem2.Size = new System.Drawing.Size(992, 138);
		this.layoutControlItem2.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem2.TextVisible = false;
		this.layoutControlItem1.Control = this.txtExercice;
		this.layoutControlItem1.Location = new System.Drawing.Point(0, 0);
		this.layoutControlItem1.MaxSize = new System.Drawing.Size(327, 24);
		this.layoutControlItem1.MinSize = new System.Drawing.Size(327, 24);
		this.layoutControlItem1.Name = "layoutControlItem1";
		this.layoutControlItem1.Size = new System.Drawing.Size(327, 24);
		this.layoutControlItem1.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem1.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem1.Text = "Exercice";
		this.layoutControlItem1.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem1.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem1.TextToControlDistance = 0;
		this.layoutControlItem3.Control = this.txtDateDu;
		this.layoutControlItem3.Location = new System.Drawing.Point(0, 24);
		this.layoutControlItem3.MaxSize = new System.Drawing.Size(327, 24);
		this.layoutControlItem3.MinSize = new System.Drawing.Size(327, 24);
		this.layoutControlItem3.Name = "layoutControlItem3";
		this.layoutControlItem3.Size = new System.Drawing.Size(327, 24);
		this.layoutControlItem3.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem3.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem3.Text = "Date du";
		this.layoutControlItem3.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem3.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem3.TextToControlDistance = 0;
		this.layoutControlItem5.Control = this.txtDateAu;
		this.layoutControlItem5.Location = new System.Drawing.Point(0, 48);
		this.layoutControlItem5.MaxSize = new System.Drawing.Size(327, 24);
		this.layoutControlItem5.MinSize = new System.Drawing.Size(327, 24);
		this.layoutControlItem5.Name = "layoutControlItem5";
		this.layoutControlItem5.Size = new System.Drawing.Size(327, 24);
		this.layoutControlItem5.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem5.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem5.Text = "Date au";
		this.layoutControlItem5.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem5.TextSize = new System.Drawing.Size(70, 20);
		this.layoutControlItem5.TextToControlDistance = 0;
		this.emptySpaceItem1.AllowHotTrack = false;
		this.emptySpaceItem1.Location = new System.Drawing.Point(327, 0);
		this.emptySpaceItem1.Name = "emptySpaceItem1";
		this.emptySpaceItem1.Size = new System.Drawing.Size(665, 72);
		this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(992, 747);
		base.Controls.Add(this.layoutControl1);
		base.Controls.Add(this.ribbon);
		base.Name = "FrmMouvementNonDeclareDeclarationTvaEncaissement";
		this.Ribbon = this.ribbon;
		this.Text = "Mouvements non déclarés";
		((System.ComponentModel.ISupportInitialize)this.ribbon).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.gcDeclaration).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gvDeclaration).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gc).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gv).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDu.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDu.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateAu.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateAu.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtExercice.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gridLookUpEdit1View).EndInit();
		((System.ComponentModel.ISupportInitialize)this.Root).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).EndInit();
		((System.ComponentModel.ISupportInitialize)this.splitterItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}

	private void Initialize()
	{
		InitGrid();
		InitGridRegroupement();
		InitExercice();
	}

	private void InitGridRegroupement()
	{
		int nombreDecimalDefaultDevise = _controller.GetNombreDecimalDefaultDevise();
		RepositoryItemImageComboBox repositoryItemImageComboBox = new RepositoryItemImageComboBox();
		repositoryItemImageComboBox.Items.Add(new ImageComboBoxItem("Encaissement", LigneDeclarationTvaEncaissementDomaine.Encaissement));
		repositoryItemImageComboBox.Items.Add(new ImageComboBoxItem("Decaissement", LigneDeclarationTvaEncaissementDomaine.Decaissement));
		repositoryItemImageComboBox.GlyphAlignment = HorzAlignment.Far;
		repositoryItemImageComboBox.ShowToolTipForTrimmedText = DefaultBoolean.True;
		GridColumn gridColumn = new GridColumn
		{
			ColumnEdit = repositoryItemImageComboBox,
			Caption = "Domaine",
			FieldName = "Domaine",
			Visible = true,
			MinWidth = 20
		};
		GridColumn gridColumn2 = new GridColumn
		{
			Caption = "Taxe",
			FieldName = "ErpTaxeCode",
			Visible = true,
			ColumnEdit = GetRepoTaxe()
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
			Caption = "Assiette",
			FieldName = "TotalAssiette",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn5 = new GridColumn
		{
			Caption = "Tva",
			FieldName = "TotalTva",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		gvDeclaration.Columns.AddRange(new GridColumn[5] { gridColumn, gridColumn2, gridColumn3, gridColumn4, gridColumn5 });
		gvDeclaration.BestFitColumns();
		gvDeclaration.FocusRectStyle = DrawFocusRectStyle.RowFullFocus;
		gvDeclaration.OptionsSelection.EnableAppearanceFocusedCell = false;
		gvDeclaration.Appearance.SelectedRow.Options.UseBackColor = true;
		gvDeclaration.Appearance.FocusedRow.BackColor = Color.Transparent;
		gvDeclaration.OptionsFilter.DefaultFilterEditorView = FilterEditorViewMode.VisualAndText;
		gvDeclaration.OptionsView.ShowFilterPanelMode = ShowFilterPanelMode.Default;
		gvDeclaration.OptionsFind.AlwaysVisible = false;
		gvDeclaration.OptionsView.ShowGroupPanel = true;
		gvDeclaration.OptionsView.ShowAutoFilterRow = true;
		gvDeclaration.OptionsView.ShowFooter = true;
		gvDeclaration.Appearance.HideSelectionRow.Assign(gv.Appearance.FocusedRow);
		gvDeclaration.OptionsView.ShowIndicator = false;
		gvDeclaration.OptionsBehavior.Editable = false;
		gvDeclaration.OptionsMenu.ShowGroupSummaryEditorItem = true;
		gvDeclaration.OptionsView.GroupFooterShowMode = GroupFooterShowMode.VisibleAlways;
		gvDeclaration.GroupSummary.Add(new GridGroupSummaryItem(SummaryItemType.Sum, "TotalAssiette", gridColumn4, "{0:n" + nombreDecimalDefaultDevise + "}"));
		gvDeclaration.GroupSummary.Add(new GridGroupSummaryItem(SummaryItemType.Sum, "TotalTva", gridColumn5, "{0:n" + nombreDecimalDefaultDevise + "}"));
		GridColumnSummaryItem item = new GridColumnSummaryItem(SummaryItemType.Sum, "TotalAssiette", "{0:n" + nombreDecimalDefaultDevise + "}");
		GridColumnSummaryItem item2 = new GridColumnSummaryItem(SummaryItemType.Sum, "TotalTva", "{0:n" + nombreDecimalDefaultDevise + "}");
		gridColumn4.Summary.Add(item);
		gridColumn5.Summary.Add(item2);
	}

	private void InitExercice()
	{
		GridColumn gridColumn = new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "Intitule",
			Visible = true
		};
		txtExercice.Properties.View.Columns.AddRange(new GridColumn[1] { gridColumn });
		txtExercice.Properties.DataSource = _controller.GetAllExercice();
		txtExercice.Properties.DisplayMember = "Intitule";
		txtExercice.Properties.ValueMember = "Intitule";
	}

	private void InitGrid()
	{
		RepositoryItemGridLookUpEdit repositoryGridLookUpEdit = RepositoryItemHelper.GetRepositoryGridLookUpEdit(_controller.GetAllModeReglement().ToList(), "Designation", "No");
		repositoryGridLookUpEdit.View.Columns.Add(new GridColumn
		{
			Caption = "Code",
			FieldName = "Code",
			Visible = true
		});
		repositoryGridLookUpEdit.View.Columns.Add(new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "Designation",
			Visible = true
		});
		RepositoryItemImageComboBox repositoryItemImageComboBox = new RepositoryItemImageComboBox();
		repositoryItemImageComboBox.Items.Add(new ImageComboBoxItem("Encaissement", LigneDeclarationTvaEncaissementDomaine.Encaissement));
		repositoryItemImageComboBox.Items.Add(new ImageComboBoxItem("Decaissement", LigneDeclarationTvaEncaissementDomaine.Decaissement));
		repositoryItemImageComboBox.GlyphAlignment = HorzAlignment.Far;
		repositoryItemImageComboBox.ShowToolTipForTrimmedText = DefaultBoolean.True;
		RepositoryItemImageComboBox repositoryItemImageComboBox2 = new RepositoryItemImageComboBox();
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Règlement client", MouvementDomaine.ReglementClient));
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Règlement fournisseur", MouvementDomaine.ReglementFournisseur));
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Depense", MouvementDomaine.Depense));
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Opération bancaire", MouvementDomaine.OperationBancaire));
		repositoryItemImageComboBox2.GlyphAlignment = HorzAlignment.Far;
		repositoryItemImageComboBox2.ShowToolTipForTrimmedText = DefaultBoolean.True;
		int nombreDecimalDefaultDevise = _controller.GetNombreDecimalDefaultDevise();
		GridColumn gridColumn = new GridColumn
		{
			ColumnEdit = repositoryItemImageComboBox,
			Caption = "Type",
			FieldName = "DomaineDeclarationTva",
			Visible = true,
			MinWidth = 20
		};
		GridColumn gridColumn2 = new GridColumn
		{
			ColumnEdit = repositoryItemImageComboBox2,
			Caption = "Domaine",
			FieldName = "MouvementDomaine",
			Visible = true,
			MinWidth = 20
		};
		GridColumn gridColumn3 = new GridColumn
		{
			Caption = "N° règlement",
			FieldName = "MouvementNumero",
			Visible = true,
			Width = 35
		};
		GridColumn gridColumn4 = new GridColumn
		{
			ColumnEdit = repositoryGridLookUpEdit,
			Caption = "Mode",
			FieldName = "MouvementModeNo",
			Visible = true,
			MinWidth = 50
		};
		GridColumn gridColumn5 = new GridColumn
		{
			Caption = "Bordereau",
			FieldName = "MouvementEnteteBordereauNumero",
			Visible = true,
			MinWidth = 50
		};
		GridColumn gridColumn6 = new GridColumn
		{
			Caption = "Banque",
			FieldName = "MouvementBanqueAbregee",
			Visible = true,
			MinWidth = 50
		};
		GridColumn gridColumn7 = new GridColumn
		{
			Caption = "Date règlement",
			FieldName = "MouvementDate",
			Visible = true,
			Width = 35
		};
		GridColumn gridColumn8 = new GridColumn
		{
			Caption = "Echéance règlement",
			FieldName = "MouvementEcheance",
			Visible = true,
			Width = 35
		};
		GridColumn gridColumn9 = new GridColumn
		{
			Caption = "Montant règlement",
			FieldName = "MouvementMontantDeviseSociete",
			Visible = true,
			Width = 80,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn10 = new GridColumn
		{
			Caption = "N° document",
			FieldName = "DocumentNumero",
			Visible = false,
			Width = 60
		};
		GridColumn gridColumn11 = new GridColumn
		{
			Caption = "Date document",
			FieldName = "DocumentDate",
			Visible = true,
			Width = 50
		};
		GridColumn gridColumn12 = new GridColumn
		{
			Caption = "Echéance document",
			FieldName = "DocumentEcheance",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn13 = new GridColumn
		{
			Caption = "Montant document",
			FieldName = "DocumentMontantDeviseSociete",
			Visible = true,
			Tag = new
			{
				Default = true
			},
			Width = 60
		};
		GridColumn gridColumn14 = new GridColumn
		{
			Caption = "Date affectation",
			FieldName = "AffectationDate",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn15 = new GridColumn
		{
			Caption = "Montant affectation",
			FieldName = "AffectationMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			},
			Width = 80
		};
		GridColumn gridColumn16 = new GridColumn
		{
			Caption = "Date rapprochement",
			FieldName = "DateRapprochementComptable",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn17 = new GridColumn
		{
			Caption = "Pièce trésorerie",
			FieldName = "PieceTresorerieComptable",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn18 = new GridColumn
		{
			Caption = "D",
			ColumnEdit = GetRepoDeclare(),
			FieldName = "IsDeclare",
			Visible = true,
			Width = 10,
			MaxWidth = 10
		};
		GridColumn gridColumn19 = new GridColumn
		{
			Caption = "Date déclaration",
			FieldName = "DateDeclaration",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn20 = new GridColumn
		{
			Caption = "Assiette",
			FieldName = "AssietteDeclaration",
			Visible = true,
			Tag = new
			{
				Default = true
			},
			Width = 60
		};
		GridColumn gridColumn21 = new GridColumn
		{
			Caption = "Taxe",
			FieldName = "ErpTaxeCode",
			Visible = true,
			ColumnEdit = GetRepoTaxe()
		};
		GridColumn gridColumn22 = new GridColumn
		{
			Caption = "Taux",
			FieldName = "TauxTva",
			Visible = true,
			Tag = new
			{
				Default = true
			},
			Width = 60
		};
		GridColumn gridColumn23 = new GridColumn
		{
			Caption = "TVA",
			FieldName = "MontantDeclaration",
			Visible = true,
			Tag = new
			{
				Default = true
			},
			Width = 60
		};
		GridColumn gridColumn24 = new GridColumn
		{
			ColumnEdit = GetRepositoryTypeMode(),
			Caption = "T",
			FieldName = "MouvementTypeMode",
			Visible = true,
			MinWidth = 20,
			MaxWidth = 20,
			ToolTip = "Type"
		};
		gv.Columns.AddRange(new GridColumn[24]
		{
			gridColumn, gridColumn2, gridColumn3, gridColumn7, gridColumn4, gridColumn24, gridColumn5, gridColumn8, gridColumn9, gridColumn10,
			gridColumn11, gridColumn12, gridColumn13, gridColumn6, gridColumn17, gridColumn16, gridColumn15, gridColumn14, gridColumn18, gridColumn20,
			gridColumn21, gridColumn22, gridColumn23, gridColumn19
		});
		gv.BestFitColumns();
		gv.FocusRectStyle = DrawFocusRectStyle.RowFullFocus;
		gv.OptionsSelection.EnableAppearanceFocusedCell = false;
		gv.Appearance.SelectedRow.Options.UseBackColor = true;
		gv.Appearance.FocusedRow.BackColor = Color.Transparent;
		gv.OptionsFilter.DefaultFilterEditorView = FilterEditorViewMode.VisualAndText;
		gv.OptionsView.ShowFilterPanelMode = ShowFilterPanelMode.Default;
		gv.OptionsFind.AlwaysVisible = false;
		gv.OptionsView.ShowGroupPanel = true;
		gv.OptionsView.ShowAutoFilterRow = true;
		gv.OptionsView.ShowFooter = true;
		gv.Appearance.HideSelectionRow.Assign(gv.Appearance.FocusedRow);
		gv.OptionsView.ShowIndicator = false;
		gv.OptionsBehavior.Editable = false;
		gv.OptionsMenu.ShowGroupSummaryEditorItem = true;
		gv.OptionsView.GroupFooterShowMode = GroupFooterShowMode.VisibleAlways;
		gv.GroupSummary.Add(new GridGroupSummaryItem(SummaryItemType.Sum, "AssietteDeclaration", gridColumn20, "{0:n" + nombreDecimalDefaultDevise + "}"));
		gv.GroupSummary.Add(new GridGroupSummaryItem(SummaryItemType.Sum, "MontantDeclaration", gridColumn23, "{0:n" + nombreDecimalDefaultDevise + "}"));
		GridColumnSummaryItem item = new GridColumnSummaryItem(SummaryItemType.Sum, "AssietteDeclaration", "{0:n" + nombreDecimalDefaultDevise + "}");
		GridColumnSummaryItem item2 = new GridColumnSummaryItem(SummaryItemType.Sum, "MontantDeclaration", "{0:n" + nombreDecimalDefaultDevise + "}");
		gridColumn20.Summary.Add(item);
		gridColumn23.Summary.Add(item2);
		StyleFormatCondition styleFormatCondition = new StyleFormatCondition
		{
			ApplyToRow = true,
			Condition = FormatConditionEnum.Expression,
			Expression = "[IsDeclare] == true"
		};
		styleFormatCondition.Appearance.BackColor = Color.FromArgb(234, 242, 248);
		styleFormatCondition.Appearance.Options.UseBackColor = true;
		gv.FormatConditions.Add(styleFormatCondition);
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

	private RepositoryItemImageComboBox GetRepoDeclare()
	{
		return new RepositoryItemImageComboBox
		{
			Items = 
			{
				new ImageComboBoxItem("Non déclaré", false),
				new ImageComboBoxItem("Déclaré", true, 0)
			},
			SmallImages = new ImageCollection(allowModifyImages: false)
			{
				Images = { (Image)Resources.new_valid_16 }
			},
			GlyphAlignment = HorzAlignment.Far,
			ShowToolTipForTrimmedText = DefaultBoolean.True
		};
	}

	public RepositoryItemImageComboBox GetRepositoryTypeMode()
	{
		return new RepositoryItemImageComboBox
		{
			Items = 
			{
				new ImageComboBoxItem("Espèce", ReglementType.Espece, 0),
				new ImageComboBoxItem("Chèque", ReglementType.Cheque, 1),
				new ImageComboBoxItem("Traite", ReglementType.Traite, 2),
				new ImageComboBoxItem("Virement", ReglementType.Virement, 3)
			},
			SmallImages = new ImageCollection(allowModifyImages: false)
			{
				Images = 
				{
					(Image)Resources.espece,
					(Image)Resources.cheque,
					(Image)Resources.traite,
					(Image)Resources.virement
				}
			},
			GlyphAlignment = HorzAlignment.Far
		};
	}
}
