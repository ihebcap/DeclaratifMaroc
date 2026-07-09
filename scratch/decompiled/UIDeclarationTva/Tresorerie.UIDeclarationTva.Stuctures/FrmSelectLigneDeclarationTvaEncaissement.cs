using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DevExpress.Data;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using DevExpress.XtraLayout.Utils;
using DevExpress.XtraSplashScreen;
using Tresorerie.Core.Enum;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.UICommun.Components;
using Tresorerie.UIDeclarationTva.Properties;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class FrmSelectLigneDeclarationTvaEncaissement : XtraForm
{
	private readonly DeclarationTvaController _controller;

	private readonly OverlayTextPainter overlayLabel;

	private readonly OverlayImagePainter overlayButton;

	private CancellationTokenSource tokenSource;

	private IOverlaySplashScreenHandle handleValider;

	private DeclarationTvaEncaissementView _currentView;

	private DeclarationTvaSelectedEntityFiltreView _filtreView;

	private IContainer components;

	private LayoutControl layoutControl1;

	private LayoutControlGroup Root;

	private GridControl gc;

	private GridView gv;

	private LayoutControlItem layoutControlItem4;

	private GridControl gcDeclaration;

	private GridView gvDeclaration;

	private SplitterItem splitterItem1;

	private LayoutControlItem layoutControlItem2;

	private SimpleButton btnIntegrer;

	private SimpleButton btnActualiser;

	private EmptySpaceItem emptySpaceItem1;

	private LayoutControlItem layoutControlItem1;

	private LayoutControlItem layoutControlItem3;

	private ImageComboBoxEdit txtSelectedEntity;

	private LayoutControlItem layoutControlItem6;

	private EmptySpaceItem emptySpaceItem2;

	private FrmSelectLigneDeclarationTvaEncaissement()
	{
		InitializeComponent();
		txtSelectedEntity.KeyDown += EnterEvent;
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
		gv.FocusedRowChanged += Gv_FocusedRowChanged;
	}

	public FrmSelectLigneDeclarationTvaEncaissement(DeclarationTvaController controller)
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

	public void SetDeclaration(DeclarationTvaEncaissementView view)
	{
		_currentView = view ?? throw new ArgumentNullException("view");
		_filtreView = new DeclarationTvaSelectedEntityFiltreView
		{
			SelectedEntity = SelectedEntityFiltre.Decaissement
		};
		BindingFiltre();
	}

	private void BindingFiltre()
	{
		txtSelectedEntity.DataBindings.Clear();
		txtSelectedEntity.DataBindings.Add("EditValue", _filtreView, "SelectedEntity", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
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

	private void Actualiser(object sender = null, EventArgs e = null)
	{
		gc.DataSource = null;
		gcDeclaration.DataSource = null;
		try
		{
			handleValider = SplashScreenManager.ShowOverlayForm(this);
			List<DeclarationTvaView> declaration = _controller.GetDeclaration(_currentView, _filtreView);
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

	private void Gv_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
	{
		gv.ClearColumnErrors();
		if (!(gv.GetFocusedRow() is DeclarationTvaView declarationTvaView))
		{
			return;
		}
		GridColumn column = gv.Columns["DocumentNumero"];
		if (string.IsNullOrEmpty(declarationTvaView.DocumentNumero))
		{
			MouvementDomaine mouvementDomaine = declarationTvaView.MouvementDomaine;
			if (mouvementDomaine != MouvementDomaine.ReglementClient && mouvementDomaine == MouvementDomaine.ReglementFournisseur)
			{
				gv.SetColumnError(column, "Le numéro du document est obligatoire.");
			}
		}
		GridColumn column2 = gv.Columns["DesignationDocument"];
		if (string.IsNullOrEmpty(declarationTvaView.DesignationDocument))
		{
			gv.SetColumnError(column2, "La désignation du document est obligatoire.");
		}
		GridColumn column3 = gv.Columns["TiersCode"];
		if (string.IsNullOrEmpty(declarationTvaView.TiersCode))
		{
			MouvementDomaine mouvementDomaine = declarationTvaView.MouvementDomaine;
			if (mouvementDomaine != MouvementDomaine.ReglementClient && mouvementDomaine == MouvementDomaine.ReglementFournisseur)
			{
				gv.SetColumnError(column3, "Le tiers est obligatoire.");
			}
		}
		GridColumn column4 = gv.Columns["TiersIdentifiant"];
		if (string.IsNullOrEmpty(declarationTvaView.TiersIdentifiant))
		{
			gv.SetColumnError(column4, "L'identifiant est obligatoire.");
		}
		GridColumn column5 = gv.Columns["TiersIce"];
		if (string.IsNullOrEmpty(declarationTvaView.TiersIce))
		{
			gv.SetColumnError(column5, "L'Ice du tiers est obligatoire.");
		}
		if (declarationTvaView.Legislation == Legislation.Maroc)
		{
			if (declarationTvaView.TiersIdentifiant.Length != 8)
			{
				gv.SetColumnError(column4, "Longueur invalide. 8 caractères requis.");
			}
			if (declarationTvaView.TiersIce.Length != 15)
			{
				gv.SetColumnError(column5, "Longueur invalide. 15 caractères requis.");
			}
			if (declarationTvaView.TiersIdentifiant.Contains(" "))
			{
				gv.SetColumnError(column4, "Les espaces ne sont pas autorisés.");
			}
			if (declarationTvaView.TiersIce.Contains(" "))
			{
				gv.SetColumnError(column5, "Les espaces ne sont pas autorisés.");
			}
		}
		gv.UpdateCurrentRow();
	}

	private async void Integrer(object sener, EventArgs e)
	{
		IEnumerable<DeclarationTvaView> enumerable = await gv.GetSelectedObjectsAsync<DeclarationTvaView>();
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
		this.btnIntegrer = new DevExpress.XtraEditors.SimpleButton();
		this.btnActualiser = new DevExpress.XtraEditors.SimpleButton();
		this.gcDeclaration = new DevExpress.XtraGrid.GridControl();
		this.gvDeclaration = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.gc = new DevExpress.XtraGrid.GridControl();
		this.gv = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtSelectedEntity = new DevExpress.XtraEditors.ImageComboBoxEdit();
		this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
		this.layoutControlItem4 = new DevExpress.XtraLayout.LayoutControlItem();
		this.splitterItem1 = new DevExpress.XtraLayout.SplitterItem();
		this.layoutControlItem2 = new DevExpress.XtraLayout.LayoutControlItem();
		this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem3 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem6 = new DevExpress.XtraLayout.LayoutControlItem();
		this.emptySpaceItem2 = new DevExpress.XtraLayout.EmptySpaceItem();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.gcDeclaration).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gvDeclaration).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gc).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gv).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtSelectedEntity.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.Root).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.splitterItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem6).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).BeginInit();
		base.SuspendLayout();
		this.layoutControl1.Controls.Add(this.btnIntegrer);
		this.layoutControl1.Controls.Add(this.btnActualiser);
		this.layoutControl1.Controls.Add(this.gcDeclaration);
		this.layoutControl1.Controls.Add(this.gc);
		this.layoutControl1.Controls.Add(this.txtSelectedEntity);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 0);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.OptionsCustomizationForm.DesignTimeCustomizationFormPositionAndSize = new System.Drawing.Rectangle(1217, 284, 650, 400);
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(992, 747);
		this.layoutControl1.TabIndex = 1;
		this.layoutControl1.Text = "layoutControl1";
		this.btnIntegrer.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_download_2_32;
		this.btnIntegrer.Location = new System.Drawing.Point(754, 2);
		this.btnIntegrer.Name = "btnIntegrer";
		this.btnIntegrer.Size = new System.Drawing.Size(116, 36);
		this.btnIntegrer.StyleController = this.layoutControl1;
		this.btnIntegrer.TabIndex = 12;
		this.btnIntegrer.Text = "Intégrer";
		this.btnActualiser.ImageOptions.Image = Tresorerie.UIDeclarationTva.Properties.Resources.new_refresh4_32;
		this.btnActualiser.Location = new System.Drawing.Point(874, 2);
		this.btnActualiser.Name = "btnActualiser";
		this.btnActualiser.Size = new System.Drawing.Size(116, 36);
		this.btnActualiser.StyleController = this.layoutControl1;
		this.btnActualiser.TabIndex = 11;
		this.btnActualiser.Text = "Actualiser";
		this.gcDeclaration.Location = new System.Drawing.Point(2, 578);
		this.gcDeclaration.MainView = this.gvDeclaration;
		this.gcDeclaration.Name = "gcDeclaration";
		this.gcDeclaration.Size = new System.Drawing.Size(988, 167);
		this.gcDeclaration.TabIndex = 10;
		this.gcDeclaration.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gvDeclaration });
		this.gvDeclaration.GridControl = this.gcDeclaration;
		this.gvDeclaration.Name = "gvDeclaration";
		this.gc.Location = new System.Drawing.Point(2, 42);
		this.gc.MainView = this.gv;
		this.gc.Name = "gc";
		this.gc.Size = new System.Drawing.Size(988, 522);
		this.gc.TabIndex = 7;
		this.gc.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[1] { this.gv });
		this.gv.GridControl = this.gc;
		this.gv.Name = "gv";
		this.txtSelectedEntity.Location = new System.Drawing.Point(82, 2);
		this.txtSelectedEntity.Name = "txtSelectedEntity";
		this.txtSelectedEntity.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtSelectedEntity.Size = new System.Drawing.Size(158, 20);
		this.txtSelectedEntity.StyleController = this.layoutControl1;
		this.txtSelectedEntity.TabIndex = 14;
		this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
		this.Root.GroupBordersVisible = false;
		this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[8] { this.layoutControlItem4, this.splitterItem1, this.layoutControlItem2, this.emptySpaceItem1, this.layoutControlItem1, this.layoutControlItem3, this.layoutControlItem6, this.emptySpaceItem2 });
		this.Root.Name = "Root";
		this.Root.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.Root.Size = new System.Drawing.Size(992, 747);
		this.Root.TextVisible = false;
		this.layoutControlItem4.Control = this.gc;
		this.layoutControlItem4.Location = new System.Drawing.Point(0, 40);
		this.layoutControlItem4.Name = "layoutControlItem4";
		this.layoutControlItem4.Size = new System.Drawing.Size(992, 526);
		this.layoutControlItem4.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem4.TextVisible = false;
		this.splitterItem1.AllowHotTrack = true;
		this.splitterItem1.Inverted = true;
		this.splitterItem1.IsCollapsible = DevExpress.Utils.DefaultBoolean.True;
		this.splitterItem1.Location = new System.Drawing.Point(0, 566);
		this.splitterItem1.Name = "splitterItem1";
		this.splitterItem1.Size = new System.Drawing.Size(992, 10);
		this.layoutControlItem2.Control = this.gcDeclaration;
		this.layoutControlItem2.Location = new System.Drawing.Point(0, 576);
		this.layoutControlItem2.Name = "layoutControlItem2";
		this.layoutControlItem2.Size = new System.Drawing.Size(992, 171);
		this.layoutControlItem2.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem2.TextVisible = false;
		this.emptySpaceItem1.AllowHotTrack = false;
		this.emptySpaceItem1.Location = new System.Drawing.Point(242, 0);
		this.emptySpaceItem1.Name = "emptySpaceItem1";
		this.emptySpaceItem1.Size = new System.Drawing.Size(510, 40);
		this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem1.Control = this.btnActualiser;
		this.layoutControlItem1.Location = new System.Drawing.Point(872, 0);
		this.layoutControlItem1.MaxSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem1.MinSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem1.Name = "layoutControlItem1";
		this.layoutControlItem1.Size = new System.Drawing.Size(120, 40);
		this.layoutControlItem1.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem1.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem1.TextVisible = false;
		this.layoutControlItem3.Control = this.btnIntegrer;
		this.layoutControlItem3.Location = new System.Drawing.Point(752, 0);
		this.layoutControlItem3.MaxSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem3.MinSize = new System.Drawing.Size(120, 40);
		this.layoutControlItem3.Name = "layoutControlItem3";
		this.layoutControlItem3.Size = new System.Drawing.Size(120, 40);
		this.layoutControlItem3.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem3.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem3.TextVisible = false;
		this.layoutControlItem6.Control = this.txtSelectedEntity;
		this.layoutControlItem6.Location = new System.Drawing.Point(0, 0);
		this.layoutControlItem6.Name = "layoutControlItem6";
		this.layoutControlItem6.Size = new System.Drawing.Size(242, 24);
		this.layoutControlItem6.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem6.Text = "Domaine";
		this.layoutControlItem6.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem6.TextSize = new System.Drawing.Size(75, 13);
		this.layoutControlItem6.TextToControlDistance = 0;
		this.emptySpaceItem2.AllowHotTrack = false;
		this.emptySpaceItem2.Location = new System.Drawing.Point(0, 24);
		this.emptySpaceItem2.Name = "emptySpaceItem2";
		this.emptySpaceItem2.Size = new System.Drawing.Size(242, 16);
		this.emptySpaceItem2.TextSize = new System.Drawing.Size(0, 0);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(992, 747);
		base.Controls.Add(this.layoutControl1);
		base.IconOptions.ShowIcon = false;
		base.Name = "FrmSelectLigneDeclarationTvaEncaissement";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Déclaration";
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.gcDeclaration).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gvDeclaration).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gc).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gv).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtSelectedEntity.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.Root).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).EndInit();
		((System.ComponentModel.ISupportInitialize)this.splitterItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem6).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).EndInit();
		base.ResumeLayout(false);
	}

	private void Initialize()
	{
		InitFiltre();
		InitGrid();
		InitGridRegroupement();
		InitExercice();
	}

	private void InitFiltre()
	{
		txtSelectedEntity.Properties.AddEnum<SelectedEntityFiltre>();
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
			Visible = true
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
		repositoryItemImageComboBox.Items.Add(new ImageComboBoxItem("Erreur", true, 0));
		repositoryItemImageComboBox.SmallImages = new ImageCollection(allowModifyImages: false)
		{
			Images = { (Image)Resources.delete }
		};
		repositoryItemImageComboBox.GlyphAlignment = HorzAlignment.Far;
		repositoryItemImageComboBox.ShowToolTipForTrimmedText = DefaultBoolean.True;
		RepositoryItemImageComboBox repositoryItemImageComboBox2 = new RepositoryItemImageComboBox();
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Encaissement", LigneDeclarationTvaEncaissementDomaine.Encaissement));
		repositoryItemImageComboBox2.Items.Add(new ImageComboBoxItem("Decaissement", LigneDeclarationTvaEncaissementDomaine.Decaissement));
		repositoryItemImageComboBox2.GlyphAlignment = HorzAlignment.Far;
		repositoryItemImageComboBox2.ShowToolTipForTrimmedText = DefaultBoolean.True;
		RepositoryItemImageComboBox repositoryItemImageComboBox3 = new RepositoryItemImageComboBox();
		repositoryItemImageComboBox3.Items.Add(new ImageComboBoxItem("Règlement client", MouvementDomaine.ReglementClient));
		repositoryItemImageComboBox3.Items.Add(new ImageComboBoxItem("Règlement fournisseur", MouvementDomaine.ReglementFournisseur));
		repositoryItemImageComboBox3.Items.Add(new ImageComboBoxItem("Depense", MouvementDomaine.Depense));
		repositoryItemImageComboBox3.Items.Add(new ImageComboBoxItem("Opération bancaire", MouvementDomaine.OperationBancaire));
		repositoryItemImageComboBox3.GlyphAlignment = HorzAlignment.Far;
		repositoryItemImageComboBox3.ShowToolTipForTrimmedText = DefaultBoolean.True;
		int nombreDecimalDefaultDevise = _controller.GetNombreDecimalDefaultDevise();
		GridColumn gridColumn = new GridColumn
		{
			ColumnEdit = repositoryItemImageComboBox2,
			Caption = "Type",
			FieldName = "DomaineDeclarationTva",
			Visible = true,
			MinWidth = 20
		};
		GridColumn gridColumn2 = new GridColumn
		{
			ColumnEdit = repositoryItemImageComboBox3,
			Caption = "Domaine",
			FieldName = "MouvementDomaine",
			Visible = true,
			MinWidth = 20
		};
		GridColumn gridColumn3 = new GridColumn
		{
			Caption = "Tiers",
			FieldName = "TiersCode",
			Visible = true,
			MinWidth = 20
		};
		GridColumn gridColumn4 = new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "TiersIntitule",
			Visible = true,
			MinWidth = 20
		};
		GridColumn gridColumn5 = new GridColumn
		{
			Caption = "Identifiant",
			FieldName = "TiersIdentifiant",
			Visible = true,
			MinWidth = 20
		};
		GridColumn gridColumn6 = new GridColumn
		{
			Caption = "Ice",
			FieldName = "TiersIce",
			Visible = true,
			MinWidth = 20
		};
		GridColumn gridColumn7 = new GridColumn
		{
			Caption = "N° règlement",
			FieldName = "MouvementNumero",
			Visible = true,
			Width = 35
		};
		GridColumn gridColumn8 = new GridColumn
		{
			ColumnEdit = repositoryGridLookUpEdit,
			Caption = "Mode",
			FieldName = "MouvementModeNo",
			Visible = false,
			MinWidth = 50
		};
		GridColumn gridColumn9 = new GridColumn
		{
			Caption = "Bordereau",
			FieldName = "MouvementEnteteBordereauNumero",
			Visible = false,
			MinWidth = 50
		};
		GridColumn gridColumn10 = new GridColumn
		{
			Caption = "Banque",
			FieldName = "MouvementBanqueAbregee",
			Visible = false,
			MinWidth = 50
		};
		GridColumn gridColumn11 = new GridColumn
		{
			Caption = "Date règlement",
			FieldName = "MouvementDate",
			Visible = false,
			Width = 35
		};
		GridColumn gridColumn12 = new GridColumn
		{
			Caption = "Echéance règlement",
			FieldName = "MouvementEcheance",
			Visible = false,
			Width = 35
		};
		GridColumn gridColumn13 = new GridColumn
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
		GridColumn gridColumn14 = new GridColumn
		{
			Caption = "N° document",
			FieldName = "DocumentNumero",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn15 = new GridColumn
		{
			Caption = "Date document",
			FieldName = "DocumentDate",
			Visible = false,
			Width = 50
		};
		GridColumn gridColumn16 = new GridColumn
		{
			Caption = "Echéance document",
			FieldName = "DocumentEcheance",
			Visible = false,
			Width = 60
		};
		GridColumn gridColumn17 = new GridColumn
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
		GridColumn gridColumn18 = new GridColumn
		{
			Caption = "Date affectation",
			FieldName = "AffectationDate",
			Visible = false,
			Width = 60
		};
		GridColumn gridColumn19 = new GridColumn
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
		GridColumn gridColumn20 = new GridColumn
		{
			Caption = "Date rapprochement",
			FieldName = "DateRapprochementComptable",
			Visible = false,
			Width = 60
		};
		GridColumn gridColumn21 = new GridColumn
		{
			Caption = "Pièce trésorerie",
			FieldName = "PieceTresorerieComptable",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn22 = new GridColumn
		{
			Caption = "D",
			ColumnEdit = GetRepoDeclare(),
			FieldName = "IsDeclare",
			Visible = false,
			Width = 10,
			MaxWidth = 10
		};
		GridColumn gridColumn23 = new GridColumn
		{
			Caption = "Date déclaration",
			FieldName = "DateDeclaration",
			Visible = false,
			Width = 60
		};
		GridColumn gridColumn24 = new GridColumn
		{
			Caption = "Taxe",
			FieldName = "ErpTaxeCode",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn25 = new GridColumn
		{
			Caption = "Code activité",
			FieldName = "CodeActivite",
			Visible = true,
			Width = 60
		};
		GridColumn gridColumn26 = new GridColumn
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
		GridColumn gridColumn27 = new GridColumn
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
		GridColumn gridColumn28 = new GridColumn
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
		GridColumn gridColumn29 = new GridColumn
		{
			ColumnEdit = GetRepositoryTypeMode(),
			Caption = "T",
			FieldName = "MouvementTypeMode",
			Visible = false,
			MinWidth = 20,
			MaxWidth = 20,
			ToolTip = "Type"
		};
		GridColumn gridColumn30 = new GridColumn
		{
			Caption = "E",
			FieldName = "HasError",
			Visible = true,
			ToolTip = "Erreur",
			ColumnEdit = repositoryItemImageComboBox,
			MinWidth = 20,
			MaxWidth = 20
		};
		gv.Columns.AddRange(new GridColumn[30]
		{
			gridColumn, gridColumn2, gridColumn3, gridColumn4, gridColumn5, gridColumn6, gridColumn7, gridColumn11, gridColumn8, gridColumn29,
			gridColumn9, gridColumn12, gridColumn13, gridColumn14, gridColumn15, gridColumn16, gridColumn17, gridColumn10, gridColumn21, gridColumn20,
			gridColumn19, gridColumn18, gridColumn22, gridColumn24, gridColumn25, gridColumn26, gridColumn27, gridColumn28, gridColumn23, gridColumn30
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
		gv.GroupSummary.Add(new GridGroupSummaryItem(SummaryItemType.Sum, "AssietteDeclaration", gridColumn26, "{0:n" + nombreDecimalDefaultDevise + "}"));
		gv.GroupSummary.Add(new GridGroupSummaryItem(SummaryItemType.Sum, "MontantDeclaration", gridColumn28, "{0:n" + nombreDecimalDefaultDevise + "}"));
		gv.OptionsSelection.MultiSelect = true;
		gv.OptionsSelection.MultiSelectMode = GridMultiSelectMode.CheckBoxRowSelect;
		gv.OptionsSelection.CheckBoxSelectorColumnWidth = 30;
		GridColumnSummaryItem item = new GridColumnSummaryItem(SummaryItemType.Sum, "AssietteDeclaration", "{0:n" + nombreDecimalDefaultDevise + "}");
		GridColumnSummaryItem item2 = new GridColumnSummaryItem(SummaryItemType.Sum, "MontantDeclaration", "{0:n" + nombreDecimalDefaultDevise + "}");
		gridColumn26.Summary.Add(item);
		gridColumn28.Summary.Add(item2);
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
