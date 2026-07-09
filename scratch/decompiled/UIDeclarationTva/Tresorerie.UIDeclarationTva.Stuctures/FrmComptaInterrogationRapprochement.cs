using System;
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
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.UICommun.Components;
using Tresorerie.UIDeclarationTva.Infrastructures;
using Tresorerie.UIDeclarationTva.Properties;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class FrmComptaInterrogationRapprochement : RibbonForm, IGridViewForm
{
	private readonly InterrogationRapprochementController _controller;

	private readonly ListDeclarationTvaController _declarationController;

	private readonly OverlayTextPainter overlayLabel;

	private readonly OverlayImagePainter overlayButton;

	private CancellationTokenSource tokenSource;

	private IOverlaySplashScreenHandle handleValider;

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

	private GridLookUpEdit txtExercice;

	private GridView gridLookUpEdit1View;

	private EmptySpaceItem emptySpaceItem2;

	private LayoutControlItem layoutControlItem3;

	private GridControl gc;

	private GridView gv;

	private LayoutControlItem layoutControlItem4;

	private DateEdit txtDateDu;

	private DateEdit txtDateAu;

	private LayoutControlItem layoutControlItem1;

	private LayoutControlItem layoutControlItem5;

	private BarButtonItem btnActualiser;

	private RibbonPageGroup ribbonPageGroup2;

	public GridView View => gv;

	private FrmComptaInterrogationRapprochement()
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

	public FrmComptaInterrogationRapprochement(InterrogationRapprochementController controller, ListDeclarationTvaController declarationController)
		: this()
	{
		_controller = controller ?? throw new ArgumentNullException("controller");
		_declarationController = declarationController ?? throw new ArgumentNullException("declarationController");
		Initialize();
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

	private void Actualiser(object sender = null, EventArgs e = null)
	{
		gc.DataSource = null;
		if (!CheckFiltre())
		{
			return;
		}
		try
		{
			handleValider = SplashScreenManager.ShowOverlayForm(this);
			gc.DataSource = _controller.GetEcritureRapproche(txtDateDu.DateTime.Date, txtDateAu.DateTime.Date);
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

	private void OpenDeclaration(object sender, OpenLinkEventArgs e)
	{
		if (gv.GetFocusedRow() is InterrogationRapprochementView { DeclarationNo: var declarationNo } && declarationNo.HasValue)
		{
			DeclarationTvaEncaissementView declarationTvaEncaissementView = _declarationController.Get(declarationNo.Value);
			if (declarationTvaEncaissementView != null && base.MdiParent is IParentFormDeclaration parentFormDeclaration)
			{
				parentFormDeclaration.OpenDeclarationTvaEncaissement(declarationTvaEncaissementView);
			}
		}
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
		this.gc = new DevExpress.XtraGrid.GridControl();
		this.gv = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtExercice = new DevExpress.XtraEditors.GridLookUpEdit();
		this.gridLookUpEdit1View = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtDateDu = new DevExpress.XtraEditors.DateEdit();
		this.txtDateAu = new DevExpress.XtraEditors.DateEdit();
		this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
		this.emptySpaceItem2 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.layoutControlItem3 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem4 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem5 = new DevExpress.XtraLayout.LayoutControlItem();
		((System.ComponentModel.ISupportInitialize)this.ribbon).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.gc).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gv).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtExercice.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gridLookUpEdit1View).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDu.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDu.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateAu.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateAu.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.Root).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).BeginInit();
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
		this.layoutControl1.Controls.Add(this.gc);
		this.layoutControl1.Controls.Add(this.txtExercice);
		this.layoutControl1.Controls.Add(this.txtDateDu);
		this.layoutControl1.Controls.Add(this.txtDateAu);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 162);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.OptionsCustomizationForm.DesignTimeCustomizationFormPositionAndSize = new System.Drawing.Rectangle(1217, 284, 650, 400);
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(992, 585);
		this.layoutControl1.TabIndex = 1;
		this.layoutControl1.Text = "layoutControl1";
		this.gc.Location = new System.Drawing.Point(2, 74);
		this.gc.MainView = this.gv;
		this.gc.MenuManager = this.ribbon;
		this.gc.Name = "gc";
		this.gc.Size = new System.Drawing.Size(988, 509);
		this.gc.TabIndex = 7;
		this.gc.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gv });
		this.gv.GridControl = this.gc;
		this.gv.Name = "gv";
		this.txtExercice.Location = new System.Drawing.Point(87, 2);
		this.txtExercice.MenuManager = this.ribbon;
		this.txtExercice.Name = "txtExercice";
		this.txtExercice.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtExercice.Properties.NullText = "";
		this.txtExercice.Properties.PopupView = this.gridLookUpEdit1View;
		this.txtExercice.Size = new System.Drawing.Size(194, 20);
		this.txtExercice.StyleController = this.layoutControl1;
		this.txtExercice.TabIndex = 6;
		this.gridLookUpEdit1View.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
		this.gridLookUpEdit1View.Name = "gridLookUpEdit1View";
		this.gridLookUpEdit1View.OptionsSelection.EnableAppearanceFocusedCell = false;
		this.gridLookUpEdit1View.OptionsView.ShowGroupPanel = false;
		this.txtDateDu.EditValue = null;
		this.txtDateDu.Location = new System.Drawing.Point(87, 26);
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
		this.txtDateDu.Size = new System.Drawing.Size(194, 20);
		this.txtDateDu.StyleController = this.layoutControl1;
		this.txtDateDu.TabIndex = 8;
		this.txtDateAu.EditValue = null;
		this.txtDateAu.Location = new System.Drawing.Point(87, 50);
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
		this.txtDateAu.Size = new System.Drawing.Size(194, 20);
		this.txtDateAu.StyleController = this.layoutControl1;
		this.txtDateAu.TabIndex = 9;
		this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
		this.Root.GroupBordersVisible = false;
		this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[5] { this.emptySpaceItem2, this.layoutControlItem3, this.layoutControlItem4, this.layoutControlItem1, this.layoutControlItem5 });
		this.Root.Name = "Root";
		this.Root.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.Root.Size = new System.Drawing.Size(992, 585);
		this.Root.TextVisible = false;
		this.emptySpaceItem2.AllowHotTrack = false;
		this.emptySpaceItem2.Location = new System.Drawing.Point(283, 0);
		this.emptySpaceItem2.Name = "emptySpaceItem2";
		this.emptySpaceItem2.Size = new System.Drawing.Size(709, 72);
		this.emptySpaceItem2.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem3.Control = this.txtExercice;
		this.layoutControlItem3.Location = new System.Drawing.Point(0, 0);
		this.layoutControlItem3.MaxSize = new System.Drawing.Size(283, 24);
		this.layoutControlItem3.MinSize = new System.Drawing.Size(283, 24);
		this.layoutControlItem3.Name = "layoutControlItem3";
		this.layoutControlItem3.Size = new System.Drawing.Size(283, 24);
		this.layoutControlItem3.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem3.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem3.Text = "Exercice";
		this.layoutControlItem3.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem3.TextSize = new System.Drawing.Size(80, 20);
		this.layoutControlItem3.TextToControlDistance = 0;
		this.layoutControlItem4.Control = this.gc;
		this.layoutControlItem4.Location = new System.Drawing.Point(0, 72);
		this.layoutControlItem4.Name = "layoutControlItem4";
		this.layoutControlItem4.Size = new System.Drawing.Size(992, 513);
		this.layoutControlItem4.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem4.TextVisible = false;
		this.layoutControlItem1.Control = this.txtDateDu;
		this.layoutControlItem1.Location = new System.Drawing.Point(0, 24);
		this.layoutControlItem1.MaxSize = new System.Drawing.Size(283, 24);
		this.layoutControlItem1.MinSize = new System.Drawing.Size(283, 24);
		this.layoutControlItem1.Name = "layoutControlItem1";
		this.layoutControlItem1.Size = new System.Drawing.Size(283, 24);
		this.layoutControlItem1.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem1.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem1.Text = "Date du";
		this.layoutControlItem1.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem1.TextSize = new System.Drawing.Size(80, 20);
		this.layoutControlItem1.TextToControlDistance = 0;
		this.layoutControlItem5.Control = this.txtDateAu;
		this.layoutControlItem5.Location = new System.Drawing.Point(0, 48);
		this.layoutControlItem5.MaxSize = new System.Drawing.Size(283, 24);
		this.layoutControlItem5.MinSize = new System.Drawing.Size(283, 24);
		this.layoutControlItem5.Name = "layoutControlItem5";
		this.layoutControlItem5.Size = new System.Drawing.Size(283, 24);
		this.layoutControlItem5.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem5.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem5.Text = "Au";
		this.layoutControlItem5.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem5.TextSize = new System.Drawing.Size(80, 20);
		this.layoutControlItem5.TextToControlDistance = 0;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(992, 747);
		base.Controls.Add(this.layoutControl1);
		base.Controls.Add(this.ribbon);
		base.Name = "FrmComptaInterrogationRapprochement";
		this.Ribbon = this.ribbon;
		this.Text = "Intérrogation rapprochement";
		((System.ComponentModel.ISupportInitialize)this.ribbon).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.gc).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gv).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtExercice.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gridLookUpEdit1View).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDu.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateDu.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateAu.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateAu.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.Root).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}

	private void Initialize()
	{
		InitGrid();
		InitExercice();
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
		int nombreDecimalDefaultDevise = _controller.GetNombreDecimalDefaultDevise();
		RepositoryItemHyperLinkEdit repositoryItemHyperLinkEdit = new RepositoryItemHyperLinkEdit
		{
			SingleClick = true
		};
		repositoryItemHyperLinkEdit.OpenLink += OpenDeclaration;
		GridColumn gridColumn = new GridColumn();
		gridColumn.Caption = "Journal";
		gridColumn.FieldName = "CodeJournal";
		gridColumn.Visible = true;
		gridColumn.OptionsColumn.AllowShowHide = false;
		gridColumn.Width = 35;
		GridColumn gridColumn2 = gridColumn;
		GridColumn gridColumn3 = new GridColumn();
		gridColumn3.Caption = "N° pièce";
		gridColumn3.FieldName = "NumeroPiece";
		gridColumn3.Visible = true;
		gridColumn3.OptionsColumn.AllowShowHide = false;
		gridColumn3.Width = 35;
		GridColumn gridColumn4 = gridColumn3;
		GridColumn gridColumn5 = new GridColumn();
		gridColumn5.Caption = "N° compte général";
		gridColumn5.FieldName = "CompteGeneral";
		gridColumn5.Visible = true;
		gridColumn5.OptionsColumn.AllowShowHide = false;
		gridColumn5.Width = 80;
		GridColumn gridColumn6 = gridColumn5;
		GridColumn gridColumn7 = new GridColumn
		{
			Caption = "Contrepartie générale",
			FieldName = "ContrePartieCompteG",
			Visible = false,
			Width = 60
		};
		GridColumn gridColumn8 = new GridColumn();
		gridColumn8.Caption = "N° facture";
		gridColumn8.FieldName = "NumeroDocument";
		gridColumn8.Visible = true;
		gridColumn8.OptionsColumn.AllowShowHide = false;
		gridColumn8.Width = 50;
		GridColumn gridColumn9 = gridColumn8;
		GridColumn gridColumn10 = new GridColumn
		{
			Caption = "Référence",
			FieldName = "Reference",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn11 = new GridColumn
		{
			Caption = "Date écriture",
			FieldName = "Date",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn12 = new GridColumn
		{
			Caption = "Date saisie",
			FieldName = "DateCreation",
			Visible = false,
			Width = 60
		};
		GridColumn gridColumn13 = new GridColumn
		{
			Caption = "N° compte tiers",
			FieldName = "TiersNumero",
			Visible = true,
			Width = 80
		};
		GridColumn gridColumn14 = new GridColumn
		{
			Caption = "Contrepartie tiers",
			FieldName = "ContrePartieTiers",
			Visible = false,
			Width = 80
		};
		GridColumn gridColumn15 = new GridColumn
		{
			Caption = "Libellé écriture",
			FieldName = "Libelle",
			Visible = true,
			Width = 200
		};
		GridColumn gridColumn16 = new GridColumn
		{
			Caption = "Date échéance",
			FieldName = "Echeance",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn17 = new GridColumn();
		gridColumn17.Caption = "Débit";
		gridColumn17.FieldName = "MontantDebitDevise";
		gridColumn17.Tag = new
		{
			Default = true
		};
		gridColumn17.Visible = true;
		gridColumn17.OptionsColumn.AllowShowHide = false;
		gridColumn17.Width = 60;
		GridColumn gridColumn18 = gridColumn17;
		GridColumn gridColumn19 = new GridColumn();
		gridColumn19.Caption = "Crédit";
		gridColumn19.FieldName = "MontantCreditDevise";
		gridColumn19.Tag = new
		{
			Default = true
		};
		gridColumn19.Visible = true;
		gridColumn19.OptionsColumn.AllowShowHide = false;
		gridColumn19.Width = 60;
		GridColumn gridColumn20 = gridColumn19;
		GridColumn gridColumn21 = new GridColumn
		{
			Caption = "L",
			ColumnEdit = GetRepoLettrage(),
			FieldName = "IsLettre",
			Visible = true,
			Width = 10,
			MaxWidth = 10
		};
		GridColumn gridColumn22 = new GridColumn
		{
			Caption = "Lettre",
			FieldName = "Lettrage",
			Visible = true,
			Width = 20
		};
		GridColumn gridColumn23 = new GridColumn
		{
			Caption = "P",
			ColumnEdit = GetRepoPointage(),
			FieldName = "IsPointe",
			Visible = true,
			Width = 10,
			MaxWidth = 10
		};
		GridColumn gridColumn24 = new GridColumn
		{
			Caption = "L.Pointage",
			FieldName = "Pointage",
			Visible = true,
			Width = 20
		};
		GridColumn gridColumn25 = new GridColumn
		{
			Caption = "R",
			ColumnEdit = GetRepoRapproche(),
			FieldName = "IsRapproche",
			Visible = true,
			Width = 10,
			MaxWidth = 10
		};
		GridColumn gridColumn26 = new GridColumn
		{
			Caption = "Date rapprochement",
			FieldName = "DateRapprochement",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn27 = new GridColumn
		{
			Caption = "Pièce trésorerie",
			FieldName = "PieceTresorerie",
			Visible = true,
			Width = 20
		};
		GridColumn gridColumn28 = new GridColumn
		{
			Caption = "D",
			FieldName = "IsDeclare",
			Visible = true,
			Width = 20
		};
		GridColumn gridColumn29 = new GridColumn
		{
			Caption = "N° déclaration",
			FieldName = "DeclarationNumero",
			Visible = true,
			ColumnEdit = repositoryItemHyperLinkEdit
		};
		gv.Columns.AddRange(new GridColumn[23]
		{
			gridColumn2, gridColumn4, gridColumn6, gridColumn7, gridColumn11, gridColumn9, gridColumn10, gridColumn13, gridColumn14, gridColumn15,
			gridColumn16, gridColumn21, gridColumn22, gridColumn23, gridColumn24, gridColumn18, gridColumn20, gridColumn12, gridColumn25, gridColumn26,
			gridColumn27, gridColumn28, gridColumn29
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
		gv.OptionsBehavior.Editable = true;
		gv.OptionsBehavior.ReadOnly = true;
		gv.OptionsMenu.ShowGroupSummaryEditorItem = true;
		gv.OptionsView.GroupFooterShowMode = GroupFooterShowMode.VisibleAlways;
		gv.GroupSummary.Add(new GridGroupSummaryItem(SummaryItemType.Sum, "MontantCreditDevise", gridColumn20, "{0:n" + nombreDecimalDefaultDevise + "}"));
		gv.GroupSummary.Add(new GridGroupSummaryItem(SummaryItemType.Sum, "MontantDebitDevise", gridColumn18, "{0:n" + nombreDecimalDefaultDevise + "}"));
		GridColumnSummaryItem item = new GridColumnSummaryItem(SummaryItemType.Sum, "MontantCreditDevise", "{0:n" + nombreDecimalDefaultDevise + "}");
		GridColumnSummaryItem item2 = new GridColumnSummaryItem(SummaryItemType.Sum, "MontantDebitDevise", "{0:n" + nombreDecimalDefaultDevise + "}");
		gridColumn20.Summary.Add(item);
		gridColumn18.Summary.Add(item2);
		StyleFormatCondition styleFormatCondition = new StyleFormatCondition
		{
			ApplyToRow = true,
			Condition = FormatConditionEnum.Expression,
			Expression = "[IsTresoEntity] = false"
		};
		styleFormatCondition.Appearance.ForeColor = Color.OrangeRed;
		styleFormatCondition.Appearance.Options.UseForeColor = true;
		gv.FormatConditions.AddRange(new StyleFormatCondition[1] { styleFormatCondition });
		gv.Columns.Cast<GridColumn>().ToList().ForEach(delegate(GridColumn x)
		{
			x.OptionsColumn.AllowEdit = false;
		});
		gridColumn29.OptionsColumn.AllowEdit = true;
	}

	private RepositoryItemImageComboBox GetRepoLettrage()
	{
		return new RepositoryItemImageComboBox
		{
			Items = 
			{
				new ImageComboBoxItem("Non lettré", false),
				new ImageComboBoxItem("Lettré", true, 0)
			},
			SmallImages = new ImageCollection(allowModifyImages: false)
			{
				Images = { (Image)Resources.lettree_16 }
			},
			GlyphAlignment = HorzAlignment.Far,
			ShowToolTipForTrimmedText = DefaultBoolean.True
		};
	}

	private RepositoryItemImageComboBox GetRepoPointage()
	{
		return new RepositoryItemImageComboBox
		{
			Items = 
			{
				new ImageComboBoxItem("Non pointé", false),
				new ImageComboBoxItem("Pointé", true, 0)
			},
			SmallImages = new ImageCollection(allowModifyImages: false)
			{
				Images = { (Image)Resources.new_valid_16 }
			},
			GlyphAlignment = HorzAlignment.Far,
			ShowToolTipForTrimmedText = DefaultBoolean.True
		};
	}

	private RepositoryItemImageComboBox GetRepoRapproche()
	{
		return new RepositoryItemImageComboBox
		{
			Items = 
			{
				new ImageComboBoxItem("Non rapproché", false),
				new ImageComboBoxItem("Rapproché", true, 0)
			},
			SmallImages = new ImageCollection(allowModifyImages: false)
			{
				Images = { (Image)Resources.new_valid_16 }
			},
			GlyphAlignment = HorzAlignment.Far,
			ShowToolTipForTrimmedText = DefaultBoolean.True
		};
	}

	private RepositoryItemImageComboBox GetRepoCloture()
	{
		return new RepositoryItemImageComboBox
		{
			Items = 
			{
				new ImageComboBoxItem("Non clôturée", false),
				new ImageComboBoxItem("Clôturée", true, 0)
			},
			SmallImages = new ImageCollection(allowModifyImages: false)
			{
				Images = { (Image)Resources.lock_16 }
			},
			GlyphAlignment = HorzAlignment.Far,
			ShowToolTipForTrimmedText = DefaultBoolean.True
		};
	}
}
