using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.Data;
using DevExpress.Data.PLinq;
using DevExpress.Utils;
using DevExpress.Utils.Menu;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Menu;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;
using DevExpress.XtraLayout;
using DevExpress.XtraLayout.Utils;
using Tresorerie.Core.Models;
using Tresorerie.UICommun.Components;
using Tresorerie.UIDeclarationTva.Infrastructures;
using Tresorerie.UIDeclarationTva.Properties;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class FrmListeDeclarationTvaEncaissement : RibbonForm, IGridViewForm, IDeclarationTvaEncaissementNotify
{
	private readonly ListDeclarationTvaController _controller;

	private readonly PLinqInstantFeedbackSource _pLinqEntet;

	private List<DeclarationTvaEncaissementView> _declarationCollection;

	private IContainer components;

	private RibbonControl ribbon;

	private RibbonPage ribbonPage1;

	private RibbonPageGroup ribbonPageGroup1;

	private GridControl gc;

	private GridView gv;

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

	private LayoutControl layoutControl1;

	private SimpleButton btnAjouter;

	private LayoutControlGroup Root;

	private EmptySpaceItem emptySpaceItem2;

	private LayoutControlItem layoutControlItem1;

	private LayoutControlItem layoutControlItem2;

	private SimpleButton btnSupprimer;

	private LayoutControlItem layoutControlItem3;

	public GridView View => gv;

	private void Initialize()
	{
		PersistentRepository persistentRepository = RepositoryItemHelper.Get();
		gc.ExternalRepository = persistentRepository;
		RepositoryItem columnEdit = persistentRepository.Items[0];
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
			Caption = "Exercice",
			FieldName = "Exercice",
			Visible = true
		};
		GridColumn gridColumn4 = new GridColumn
		{
			Caption = "Période début",
			FieldName = "DateDebut",
			Visible = true
		};
		GridColumn gridColumn5 = new GridColumn
		{
			Caption = "Période fin",
			FieldName = "DateFin",
			Visible = true
		};
		GridColumn gridColumn6 = new GridColumn
		{
			Caption = "Type",
			FieldName = "TypeDeclaration",
			Visible = true
		};
		GridColumn gridColumn7 = new GridColumn
		{
			Caption = "Libellé",
			FieldName = "Libelle",
			Visible = true
		};
		GridColumn gridColumn8 = new GridColumn
		{
			Caption = "Statut",
			FieldName = "Statut",
			Visible = true
		};
		GridColumn gridColumn9 = new GridColumn
		{
			Caption = "G",
			FieldName = "IsFichierGenerer",
			Visible = true,
			MinWidth = 20,
			MaxWidth = 20,
			ToolTip = "Générée"
		};
		GridColumn gridColumn10 = new GridColumn
		{
			Caption = "D",
			FieldName = "IsDepose",
			Visible = true,
			MinWidth = 20,
			MaxWidth = 20,
			ToolTip = "Déposée"
		};
		new GridColumn
		{
			ColumnEdit = columnEdit,
			Caption = "C",
			FieldName = "IsComptabilise",
			Visible = true,
			MinWidth = 20,
			MaxWidth = 20,
			ToolTip = "Comptabilisé"
		};
		gv.Columns.AddRange(new GridColumn[10] { gridColumn, gridColumn2, gridColumn3, gridColumn4, gridColumn5, gridColumn6, gridColumn7, gridColumn8, gridColumn9, gridColumn10 });
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
		gc.DataSource = _pLinqEntet;
		gv.Tag = new Guid("{51629582-EFA3-45E0-8114-BBE755BEA4D0}");
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

	private FrmListeDeclarationTvaEncaissement()
	{
		InitializeComponent();
		_pLinqEntet = new PLinqInstantFeedbackSource(GetDeclarationCollection);
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
		btnAjouter.Click += AjouterDeclaration;
		btnSupprimer.Click += DeleteDeclaration;
		gv.RowClick += OpenDeclaration;
	}

	public FrmListeDeclarationTvaEncaissement(ListDeclarationTvaController controller)
		: this()
	{
		_controller = controller ?? throw new ArgumentNullException("controller");
		Initialize();
	}

	private void GetDeclarationCollection(object sender, GetEnumerableEventArgs e)
	{
		if (_declarationCollection == null)
		{
			_declarationCollection = _controller.GetAllDeclaration();
		}
		e.Source = _declarationCollection;
	}

	private void AjouterDeclaration(object sender, EventArgs e)
	{
		_controller.HasRestrictionAjout();
		DeclarationTvaEncaissementView view = _controller.InitView();
		((IParentFormDeclaration)base.MdiParent).OpenDeclarationTvaEncaissement(view);
	}

	private void DeleteDeclaration(object sender, EventArgs e)
	{
		if (!(gv.GetFocusedRow() is DeclarationTvaEncaissementView view))
		{
			return;
		}
		if (_controller.IsLocked(view))
		{
			XtraMessageBox.Show("La déclaration est en cours d'utilisation.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		else if (XtraMessageBox.Show("Voulez vous supprimer la déclaration?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
		{
			try
			{
				_controller.DeleteDeclaration(view);
				gc.RefreshDataSource();
			}
			catch (Exception ex)
			{
				XtraMessageBox.Show(ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}
	}

	private void GridviewSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		_ = gv.SelectedRowsCount;
	}

	private void OpenDeclaration(object sender, RowClickEventArgs e)
	{
		_controller.HasRestrictionConsultation();
		if (e.Button == MouseButtons.Left && e.Clicks == 2 && gv.GetFocusedRow() is DeclarationTvaEncaissementView view && base.MdiParent is IParentFormDeclaration parentFormDeclaration)
		{
			parentFormDeclaration.OpenDeclarationTvaEncaissement(view);
		}
	}

	public void DeclarationTvaEncaissementChanged(DeclarationTvaEncaissementChangedArgs e)
	{
		if (_declarationCollection == null || !base.IsHandleCreated)
		{
			return;
		}
		switch (e.Action)
		{
		case TypeAction.Ajout:
		{
			Action method3 = delegate
			{
				NotifyDeclarationTvaEncaissementChangedAjout(e);
			};
			BeginInvoke(method3);
			break;
		}
		case TypeAction.Suppression:
		{
			Action method2 = delegate
			{
				NotifyDeclarationTvaEncaissementChangedSupp(e);
			};
			BeginInvoke(method2);
			break;
		}
		case TypeAction.Modification:
		{
			Action method = delegate
			{
				NotifyDeclarationTvaEncaissementChangedModify(e);
			};
			BeginInvoke(method);
			break;
		}
		}
	}

	private void NotifyDeclarationTvaEncaissementChangedAjout(DeclarationTvaEncaissementChangedArgs e)
	{
		DeclarationTvaEncaissementView declarationTvaEncaissementView = _controller.Get(e.EntityNo);
		if (declarationTvaEncaissementView != null)
		{
			_declarationCollection.Add(declarationTvaEncaissementView);
			GridviewSelectionChanged(null, null);
			_pLinqEntet.Refresh();
		}
	}

	private void NotifyDeclarationTvaEncaissementChangedModify(DeclarationTvaEncaissementChangedArgs e)
	{
		DeclarationTvaEncaissementView declarationTvaEncaissementView = _declarationCollection.SingleOrDefault((DeclarationTvaEncaissementView x) => x.No == e.EntityNo);
		if (declarationTvaEncaissementView == null)
		{
			return;
		}
		DeclarationTvaEncaissementView declarationTvaEncaissementView2 = _controller.Get(e.EntityNo);
		if (declarationTvaEncaissementView2 == null)
		{
			return;
		}
		int num = _declarationCollection.IndexOf(declarationTvaEncaissementView);
		if (num < 0)
		{
			return;
		}
		int[] selectedRows = View.GetSelectedRows();
		try
		{
			_declarationCollection[num] = declarationTvaEncaissementView2;
			GridviewSelectionChanged(null, null);
			_pLinqEntet.Refresh();
		}
		catch (Exception ex)
		{
			XtraMessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		finally
		{
			if (selectedRows.Any())
			{
				View.SelectRow(selectedRows.First());
			}
		}
	}

	private void NotifyDeclarationTvaEncaissementChangedSupp(DeclarationTvaEncaissementChangedArgs e)
	{
		DeclarationTvaEncaissementView declarationTvaEncaissementView = _declarationCollection.SingleOrDefault((DeclarationTvaEncaissementView x) => x.No == e.EntityNo);
		if (declarationTvaEncaissementView != null)
		{
			_declarationCollection.Remove(declarationTvaEncaissementView);
			GridviewSelectionChanged(null, null);
			_pLinqEntet.Refresh();
		}
	}

	public int GetRowsCount()
	{
		return gv.RowCount;
	}

	public int GetSelectedRowsCount()
	{
		return gv.SelectedRowsCount;
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
		this.gc = new DevExpress.XtraGrid.GridControl();
		this.gv = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
		this.btnSupprimer = new DevExpress.XtraEditors.SimpleButton();
		this.btnAjouter = new DevExpress.XtraEditors.SimpleButton();
		this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
		this.emptySpaceItem2 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem2 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem3 = new DevExpress.XtraLayout.LayoutControlItem();
		((System.ComponentModel.ISupportInitialize)this.ribbon).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gc).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gv).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.Root).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).BeginInit();
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
		this.ribbon.Size = new System.Drawing.Size(993, 162);
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
		this.btnAppliquer.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_valid_16;
		this.btnAppliquer.Name = "btnAppliquer";
		this.btnAppliquer.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonItemStyles.SmallWithText;
		this.btnReinitialiser.Caption = "Réinitialiser";
		this.btnReinitialiser.Id = 9;
		this.btnReinitialiser.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_cancel_16;
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
		this.gc.Location = new System.Drawing.Point(2, 42);
		this.gc.MainView = this.gv;
		this.gc.MenuManager = this.ribbon;
		this.gc.Name = "gc";
		this.gc.Size = new System.Drawing.Size(989, 421);
		this.gc.TabIndex = 1;
		this.gc.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gv });
		this.gv.GridControl = this.gc;
		this.gv.Name = "gv";
		this.layoutControl1.Controls.Add(this.btnSupprimer);
		this.layoutControl1.Controls.Add(this.btnAjouter);
		this.layoutControl1.Controls.Add(this.gc);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 162);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(993, 465);
		this.layoutControl1.TabIndex = 3;
		this.layoutControl1.Text = "layoutControl1";
		this.btnSupprimer.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_delete_32;
		this.btnSupprimer.Location = new System.Drawing.Point(875, 2);
		this.btnSupprimer.Name = "btnSupprimer";
		this.btnSupprimer.Size = new System.Drawing.Size(116, 36);
		this.btnSupprimer.StyleController = this.layoutControl1;
		this.btnSupprimer.TabIndex = 5;
		this.btnSupprimer.Text = "Supprimer";
		this.btnAjouter.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.add_32x32;
		this.btnAjouter.Location = new System.Drawing.Point(755, 2);
		this.btnAjouter.Name = "btnAjouter";
		this.btnAjouter.Size = new System.Drawing.Size(116, 36);
		this.btnAjouter.StyleController = this.layoutControl1;
		this.btnAjouter.TabIndex = 4;
		this.btnAjouter.Text = "Ajouter";
		this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
		this.Root.GroupBordersVisible = false;
		this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[4] { this.emptySpaceItem2, this.layoutControlItem1, this.layoutControlItem2, this.layoutControlItem3 });
		this.Root.Name = "Root";
		this.Root.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.Root.Size = new System.Drawing.Size(993, 465);
		this.Root.TextVisible = false;
		this.emptySpaceItem2.AllowHotTrack = false;
		this.emptySpaceItem2.Location = new System.Drawing.Point(0, 0);
		this.emptySpaceItem2.Name = "emptySpaceItem2";
		this.emptySpaceItem2.Size = new System.Drawing.Size(753, 40);
		this.emptySpaceItem2.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem1.Control = this.gc;
		this.layoutControlItem1.Location = new System.Drawing.Point(0, 40);
		this.layoutControlItem1.Name = "layoutControlItem1";
		this.layoutControlItem1.Size = new System.Drawing.Size(993, 425);
		this.layoutControlItem1.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem1.TextVisible = false;
		this.layoutControlItem2.Control = this.btnAjouter;
		this.layoutControlItem2.Location = new System.Drawing.Point(753, 0);
		this.layoutControlItem2.MaxSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem2.MinSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem2.Name = "layoutControlItem2";
		this.layoutControlItem2.Size = new System.Drawing.Size(120, 40);
		this.layoutControlItem2.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem2.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem2.TextVisible = false;
		this.layoutControlItem3.Control = this.btnSupprimer;
		this.layoutControlItem3.Location = new System.Drawing.Point(873, 0);
		this.layoutControlItem3.MaxSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem3.MinSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem3.Name = "layoutControlItem3";
		this.layoutControlItem3.Size = new System.Drawing.Size(120, 40);
		this.layoutControlItem3.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem3.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem3.TextVisible = false;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(993, 627);
		base.Controls.Add(this.layoutControl1);
		base.Controls.Add(this.ribbon);
		base.Name = "FrmListeDeclarationTvaEncaissement";
		this.Ribbon = this.ribbon;
		this.Text = "Déclarations";
		((System.ComponentModel.ISupportInitialize)this.ribbon).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gc).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gv).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.Root).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
