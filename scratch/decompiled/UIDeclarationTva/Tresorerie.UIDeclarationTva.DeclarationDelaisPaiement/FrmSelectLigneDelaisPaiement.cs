using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using DevExpress.XtraLayout.Utils;
using DevExpress.XtraSplashScreen;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.UIDeclarationTva.Properties;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.DeclarationDelaisPaiement;

public class FrmSelectLigneDelaisPaiement : XtraForm
{
	private readonly LigneControleDelaisPaiementController _controller;

	private readonly OverlayTextPainter overlayLabel;

	private readonly OverlayImagePainter overlayButton;

	private CancellationTokenSource tokenSource;

	private IOverlaySplashScreenHandle handleValider;

	private DeclarationDelaisPaiementView _currentView;

	private LigneControleDelaisPaiementFiltreView _filtreView;

	private IContainer components;

	private LayoutControl layoutControl1;

	private LayoutControlGroup Root;

	private GridControl gc;

	private GridView gv;

	private SimpleButton btnIntegrer;

	private SimpleButton btnActualiser;

	private EmptySpaceItem emptySpaceItem1;

	private LayoutControlItem layoutControlItem1;

	private LayoutControlItem layoutControlItem2;

	private LayoutControlItem layoutControlItem3;

	private FrmSelectLigneDelaisPaiement()
	{
		InitializeComponent();
		btnActualiser.Click += Actualiser;
		btnIntegrer.Click += Integrer;
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

	public FrmSelectLigneDelaisPaiement(LigneControleDelaisPaiementController controller)
		: this()
	{
		_controller = controller ?? throw new ArgumentNullException("controller");
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

	public void SetDeclaration(DeclarationDelaisPaiementView view)
	{
		_currentView = view ?? throw new ArgumentNullException("view");
		_filtreView = new LigneControleDelaisPaiementFiltreView
		{
			DateDu = view.DateDebut,
			DateAu = view.DateFin
		};
	}

	protected override void OnShown(EventArgs e)
	{
		Actualiser();
		base.OnShown(e);
	}

	private void Actualiser(object sender = null, EventArgs e = null)
	{
		gc.DataSource = null;
		try
		{
			handleValider = SplashScreenManager.ShowOverlayForm(this);
			List<LigneControleDelaisPaiementView> all = _controller.GetAll(_filtreView);
			gc.DataSource = all;
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

	private async void Integrer(object sener, EventArgs e)
	{
		IEnumerable<LigneControleDelaisPaiementView> enumerable = await gv.GetSelectedObjectsAsync<LigneControleDelaisPaiementView>();
		try
		{
			if (enumerable.Count() == 0)
			{
				throw new InvalidOperationException("Aucune ligne séléctionnées.");
			}
			_controller.IntergerLigne(_currentView, enumerable);
			Close();
		}
		catch (Exception ex)
		{
			XtraMessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
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
		this.layoutControl1 = new DevExpress.XtraLayout.LayoutControl();
		this.gc = new DevExpress.XtraGrid.GridControl();
		this.gv = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.btnIntegrer = new DevExpress.XtraEditors.SimpleButton();
		this.btnActualiser = new DevExpress.XtraEditors.SimpleButton();
		this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
		this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem2 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem3 = new DevExpress.XtraLayout.LayoutControlItem();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.gc).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gv).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.Root).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).BeginInit();
		base.SuspendLayout();
		this.layoutControl1.Controls.Add(this.gc);
		this.layoutControl1.Controls.Add(this.btnIntegrer);
		this.layoutControl1.Controls.Add(this.btnActualiser);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 0);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.OptionsCustomizationForm.DesignTimeCustomizationFormPositionAndSize = new System.Drawing.Rectangle(2186, 291, 650, 400);
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(1202, 704);
		this.layoutControl1.TabIndex = 0;
		this.layoutControl1.Text = "layoutControl1";
		this.gc.Location = new System.Drawing.Point(2, 42);
		this.gc.MainView = this.gv;
		this.gc.Name = "gc";
		this.gc.Size = new System.Drawing.Size(1198, 660);
		this.gc.TabIndex = 6;
		this.gc.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gv });
		this.gv.GridControl = this.gc;
		this.gv.Name = "gv";
		this.btnIntegrer.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.export_32x32;
		this.btnIntegrer.Location = new System.Drawing.Point(964, 2);
		this.btnIntegrer.Name = "btnIntegrer";
		this.btnIntegrer.Size = new System.Drawing.Size(116, 36);
		this.btnIntegrer.StyleController = this.layoutControl1;
		this.btnIntegrer.TabIndex = 5;
		this.btnIntegrer.Text = "Intégrer";
		this.btnActualiser.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_refresh4_32;
		this.btnActualiser.Location = new System.Drawing.Point(1084, 2);
		this.btnActualiser.Name = "btnActualiser";
		this.btnActualiser.Size = new System.Drawing.Size(116, 36);
		this.btnActualiser.StyleController = this.layoutControl1;
		this.btnActualiser.TabIndex = 4;
		this.btnActualiser.Text = "Actualiser";
		this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
		this.Root.GroupBordersVisible = false;
		this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[4] { this.emptySpaceItem1, this.layoutControlItem1, this.layoutControlItem2, this.layoutControlItem3 });
		this.Root.Name = "Root";
		this.Root.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.Root.Size = new System.Drawing.Size(1202, 704);
		this.Root.TextVisible = false;
		this.emptySpaceItem1.AllowHotTrack = false;
		this.emptySpaceItem1.Location = new System.Drawing.Point(0, 0);
		this.emptySpaceItem1.Name = "emptySpaceItem1";
		this.emptySpaceItem1.Size = new System.Drawing.Size(962, 40);
		this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem1.Control = this.btnActualiser;
		this.layoutControlItem1.Location = new System.Drawing.Point(1082, 0);
		this.layoutControlItem1.MaxSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem1.MinSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem1.Name = "layoutControlItem1";
		this.layoutControlItem1.Size = new System.Drawing.Size(120, 40);
		this.layoutControlItem1.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem1.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem1.TextVisible = false;
		this.layoutControlItem2.Control = this.btnIntegrer;
		this.layoutControlItem2.Location = new System.Drawing.Point(962, 0);
		this.layoutControlItem2.MaxSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem2.MinSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem2.Name = "layoutControlItem2";
		this.layoutControlItem2.Size = new System.Drawing.Size(120, 40);
		this.layoutControlItem2.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem2.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem2.TextVisible = false;
		this.layoutControlItem3.Control = this.gc;
		this.layoutControlItem3.Location = new System.Drawing.Point(0, 40);
		this.layoutControlItem3.MinSize = new System.Drawing.Size(104, 24);
		this.layoutControlItem3.Name = "layoutControlItem3";
		this.layoutControlItem3.Size = new System.Drawing.Size(1202, 664);
		this.layoutControlItem3.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem3.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem3.TextVisible = false;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(1202, 704);
		base.Controls.Add(this.layoutControl1);
		base.IconOptions.ShowIcon = false;
		base.MinimizeBox = false;
		base.Name = "FrmSelectLigneDelaisPaiement";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Déclaration";
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.gc).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gv).EndInit();
		((System.ComponentModel.ISupportInitialize)this.Root).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).EndInit();
		base.ResumeLayout(false);
	}

	private void Initialize()
	{
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
			Caption = "Devise",
			FieldName = "EcheanceDeviseCode",
			Visible = true
		};
		GridColumn gridColumn8 = new GridColumn
		{
			Caption = "Cours échéance",
			FieldName = "EcheanceCours",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn9 = new GridColumn
		{
			Caption = "N° règlement",
			FieldName = "ReglementNumero",
			Visible = true
		};
		GridColumn gridColumn10 = new GridColumn
		{
			Caption = "Date règlement",
			FieldName = "ReglementDate",
			Visible = true
		};
		GridColumn gridColumn11 = new GridColumn
		{
			Caption = "Echéance règlement",
			FieldName = "ReglementEcheance",
			Visible = true
		};
		GridColumn gridColumn12 = new GridColumn
		{
			Caption = "Montant règlement",
			FieldName = "ReglementMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn13 = new GridColumn
		{
			Caption = "Solde règlement",
			FieldName = "ReglementSolde",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn14 = new GridColumn
		{
			Caption = "Cours règlement",
			FieldName = "ReglementCours",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn15 = new GridColumn
		{
			Caption = "Tiers",
			FieldName = "TiersNumero",
			Visible = true
		};
		GridColumn gridColumn16 = new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "TiersIntitule",
			Visible = true
		};
		GridColumn gridColumn17 = new GridColumn
		{
			Caption = "Montant affectation",
			FieldName = "AffectationMontant",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		};
		GridColumn gridColumn18 = new GridColumn
		{
			Caption = "Dépassement(j)",
			FieldName = "Depassement",
			Visible = true
		};
		GridColumn gridColumn19 = new GridColumn
		{
			Caption = "P",
			FieldName = "IsReglementPoint",
			Visible = true,
			MinWidth = 20,
			MaxWidth = 20
		};
		GridColumn gridColumn20 = new GridColumn
		{
			Caption = "Date pointage",
			FieldName = "ReglementDatePointage",
			Visible = true
		};
		gv.Columns.AddRange(new GridColumn[20]
		{
			gridColumn15, gridColumn16, gridColumn, gridColumn2, gridColumn3, gridColumn4, gridColumn5, gridColumn6, gridColumn7, gridColumn8,
			gridColumn9, gridColumn10, gridColumn11, gridColumn12, gridColumn13, gridColumn19, gridColumn20, gridColumn14, gridColumn17, gridColumn18
		});
		gv.Init();
		gv.OptionsView.ShowAutoFilterRow = true;
		gv.OptionsDetail.EnableMasterViewMode = false;
		gv.OptionsFilter.DefaultFilterEditorView = FilterEditorViewMode.VisualAndText;
		gv.OptionsView.ShowFilterPanelMode = ShowFilterPanelMode.Default;
		gv.BestFitColumns();
		gv.OptionsView.ShowFooter = true;
		gv.Columns.Cast<GridColumn>().ToList().ForEach(delegate(GridColumn x)
		{
			x.OptionsColumn.AllowEdit = false;
		});
		gv.OptionsSelection.MultiSelect = true;
		gv.OptionsSelection.MultiSelectMode = GridMultiSelectMode.CheckBoxRowSelect;
		gv.OptionsSelection.CheckBoxSelectorColumnWidth = 30;
	}
}
