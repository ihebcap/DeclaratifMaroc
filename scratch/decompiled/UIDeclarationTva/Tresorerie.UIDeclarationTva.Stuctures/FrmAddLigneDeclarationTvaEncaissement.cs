using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Mask;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using DevExpress.XtraLayout.Utils;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Win.Commun.Helper;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class FrmAddLigneDeclarationTvaEncaissement : XtraForm
{
	private LigneDeclarationTvaEncaissementView _current;

	private readonly DeclarationTvaController _controller;

	private DeclarationTvaEncaissementView _declarationView;

	private string _deviseFormat;

	private int _deviseNombreDecimals;

	private IContainer components;

	private LayoutControl layoutControl1;

	private LayoutControlGroup Root;

	private TextEdit txtNumeroPiece;

	private TextEdit txtMontant;

	private TextEdit txtTaux;

	private TextEdit txtAssiette;

	private TextEdit txtTiersIntitule;

	private TextEdit txtTiersIdentifiant;

	private EmptySpaceItem emptySpaceItem1;

	private LayoutControlItem layoutControlItem5;

	private LayoutControlItem layoutControlItem7;

	private LayoutControlItem layoutControlItem8;

	private LayoutControlItem layoutControlItem9;

	private LayoutControlItem layoutControlItem10;

	private LayoutControlItem layoutControlItem11;

	private LayoutControlItem layoutControlItem6;

	private LayoutControlItem layoutControlItem12;

	private LayoutControlItem layoutControlItem3;

	private LayoutControlItem layoutControlItem1;

	private LayoutControlItem layoutControlItem2;

	private SimpleButton btnEnregistrer;

	private SimpleButton btnAnnuler;

	private LayoutControlItem layoutControlItem4;

	private LayoutControlItem layoutControlItem13;

	private ImageComboBoxEdit txtDomaine;

	private ImageComboBoxEdit txtTypeLigne;

	private DateEdit txtDate;

	private ImageComboBoxEdit txtTypePayement;

	private SearchLookUpEdit txtTiersCode;

	private GridView searchLookUpEdit2View;

	private SearchLookUpEdit txtErpTaxeCode;

	private GridView searchLookUpEdit1View;

	private LayoutControlItem layoutControlItem14;

	private LayoutControlItem layoutControlItem15;

	private SearchLookUpEdit txtCodeActivite;

	private GridView gridView1;

	private TextEdit txtProrata;

	private LayoutControlItem layoutControlItem16;

	private EmptySpaceItem emptySpaceItem2;

	private TextEdit txtNumeroMouvement;

	private LayoutControlItem layoutControlItem17;

	private LayoutControlItem layoutControlItem18;

	private DateEdit txtDateMouvement;

	private TextEdit txtDesignationDocument;

	private LayoutControlItem layoutControlItem19;

	private FrmAddLigneDeclarationTvaEncaissement()
	{
		InitializeComponent();
		txtDomaine.KeyDown += EnterEvent;
		txtTypeLigne.KeyDown += EnterEvent;
		txtTiersCode.KeyDown += EnterEvent;
		txtTiersIdentifiant.KeyDown += EnterEvent;
		txtTiersIntitule.KeyDown += EnterEvent;
		txtDate.KeyDown += EnterEvent;
		txtNumeroPiece.KeyDown += EnterEvent;
		txtAssiette.KeyDown += EnterEvent;
		txtTaux.KeyDown += EnterEvent;
		txtMontant.KeyDown += EnterEvent;
		txtTypePayement.KeyDown += EnterEvent;
		txtErpTaxeCode.KeyDown += EnterEvent;
		txtCodeActivite.KeyDown += EnterEvent;
		txtProrata.KeyDown += EnterEvent;
		txtNumeroMouvement.KeyDown += EnterEvent;
		txtDateMouvement.KeyDown += EnterEvent;
		txtDesignationDocument.KeyDown += EnterEvent;
		txtTiersCode.EditValueChanged += TiersChanged;
		txtAssiette.EditValueChanged += AssietteTauxChanged;
		txtTaux.EditValueChanged += AssietteTauxChanged;
		txtErpTaxeCode.EditValueChanged += ErpTaxeChanged;
		btnEnregistrer.Click += Enregistrer;
		btnAnnuler.Click += Annuler;
	}

	public FrmAddLigneDeclarationTvaEncaissement(DeclarationTvaController controller)
		: this()
	{
		_controller = controller ?? throw new ArgumentNullException("controller");
		Initialize();
		_deviseFormat = "{0:" + $"# ### ##{_controller.GetDefaultDeviseFormat()}" + "}";
		_deviseNombreDecimals = _controller.GetNombreDecimalDefaultDevise();
		this.SetDecimalFormat(_deviseFormat);
	}

	public void SetDeclaration(DeclarationTvaEncaissementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_declarationView = view;
		_current = _controller.InitLigneDeclaration(view);
		Binding();
	}

	private void Binding()
	{
		if (_current == null)
		{
			_current = _controller.InitLigneDeclaration(_declarationView);
		}
		txtDomaine.DataBindings.Clear();
		txtDomaine.DataBindings.Add("EditValue", _current, "Domaine", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtTypeLigne.DataBindings.Clear();
		txtTypeLigne.DataBindings.Add("EditValue", _current, "Type", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtTiersCode.DataBindings.Clear();
		txtTiersCode.DataBindings.Add("EditValue", _current, "TiersCode", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtTiersIdentifiant.DataBindings.Clear();
		txtTiersIdentifiant.DataBindings.Add("EditValue", _current, "TiersIdentifiant", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtTiersIntitule.DataBindings.Clear();
		txtTiersIntitule.DataBindings.Add("EditValue", _current, "TiersIntitule", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtDate.DataBindings.Clear();
		txtDate.DataBindings.Add("EditValue", _current, "DateDocument", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtNumeroPiece.DataBindings.Clear();
		txtNumeroPiece.DataBindings.Add("EditValue", _current, "DocumentNumero", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtDesignationDocument.DataBindings.Clear();
		txtDesignationDocument.DataBindings.Add("EditValue", _current, "DesignationDocument", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtAssiette.DataBindings.Clear();
		txtAssiette.DataBindings.Add("EditValue", _current, "Assiette", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtErpTaxeCode.DataBindings.Clear();
		txtErpTaxeCode.DataBindings.Add("EditValue", _current, "ErpTaxeCode", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtTaux.DataBindings.Clear();
		txtTaux.DataBindings.Add("EditValue", _current, "Taux", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtMontant.DataBindings.Clear();
		txtMontant.DataBindings.Add("EditValue", _current, "Montant", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtCodeActivite.DataBindings.Clear();
		txtCodeActivite.DataBindings.Add("EditValue", _current, "CodeActivite", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtTypePayement.DataBindings.Clear();
		txtTypePayement.DataBindings.Add("EditValue", _current, "TypePayement", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtProrata.DataBindings.Clear();
		txtProrata.DataBindings.Add("EditValue", _current, "Prorata", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtNumeroMouvement.DataBindings.Clear();
		txtNumeroMouvement.DataBindings.Add("EditValue", _current, "MouvementNumero", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
		txtDateMouvement.DataBindings.Clear();
		txtDateMouvement.DataBindings.Add("EditValue", _current, "DateMouvement", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);
	}

	public void TiersChanged(object sender, EventArgs e)
	{
		if (txtTiersCode.GetSelectedDataRow() is IErpFournisseur erpFournisseur)
		{
			IErpTiersIce iceTiers = _controller.GetIceTiers(erpFournisseur.Numero, erpFournisseur.Type);
			_current.TiersIdentifiant = iceTiers?.TiersIdentifiant;
			_current.TiersIntitule = erpFournisseur.Intitule;
			_current.TiersIce = iceTiers?.TiersIce;
		}
	}

	public void ErpTaxeChanged(object sender, EventArgs e)
	{
		if (txtErpTaxeCode.GetSelectedDataRow() is IErpTaxe erpTaxe)
		{
			_current.CodeActivite = _controller.GetCodeActivite(erpTaxe.Code);
			_current.Taux = erpTaxe.Taux;
			_current.Montant = Math.Round(_current.Assiette * _current.Taux / 100m, _deviseNombreDecimals, MidpointRounding.AwayFromZero);
		}
	}

	public void AssietteTauxChanged(object sender, EventArgs e)
	{
		decimal.TryParse(txtAssiette.Text, out var result);
		decimal.TryParse(txtTaux.Text, out var result2);
		_current.Montant = Math.Round(result * result2 / 100m, _deviseNombreDecimals, MidpointRounding.AwayFromZero);
	}

	public void EnterEvent(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Return)
		{
			Enregistrer();
		}
	}

	public void Enregistrer(object sender = null, EventArgs e = null)
	{
		if (!CheckValue())
		{
			return;
		}
		try
		{
			_controller.AjouterLigneDeclaration(_current);
			Close();
		}
		catch (Exception ex)
		{
			XtraMessageBox.Show(ex.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	public void Annuler(object sender, EventArgs e)
	{
		Close();
	}

	private bool CheckValue()
	{
		txtAssiette.ErrorText = "";
		txtTaux.ErrorText = "";
		txtMontant.ErrorText = "";
		txtDate.ErrorText = "";
		txtNumeroPiece.ErrorText = "";
		txtNumeroMouvement.ErrorText = "";
		txtMontant.ErrorText = "";
		txtTiersCode.ErrorText = "";
		txtCodeActivite.ErrorText = "";
		txtProrata.ErrorText = "";
		txtDesignationDocument.ErrorText = "";
		if (string.IsNullOrEmpty(_current.TiersCode))
		{
			txtTiersCode.ErrorText = "Tiers invalide.";
			return false;
		}
		if (_current.DateMouvement < _declarationView.DateDebut || _current.DateMouvement > _declarationView.DateFin)
		{
			txtDate.ErrorText = "Date hors période de déclaration.";
			return false;
		}
		if (string.IsNullOrEmpty(_current.DocumentNumero))
		{
			txtNumeroPiece.ErrorText = "Le numéro de la pièce est obligatoire.";
			return false;
		}
		if (string.IsNullOrEmpty(_current.DesignationDocument))
		{
			txtDesignationDocument.ErrorText = "La désignation de la pièce est obligatoire.";
			return false;
		}
		if (string.IsNullOrEmpty(_current.MouvementNumero))
		{
			txtNumeroMouvement.ErrorText = "Le numéro mouvement est obligatoire.";
			return false;
		}
		if (_current.Assiette == 0m)
		{
			txtAssiette.ErrorText = "Assiette invalide.";
			return false;
		}
		if (_current.Taux < 0m || _current.Taux > 100m)
		{
			txtTaux.ErrorText = "Taux invalide.";
			return false;
		}
		if (_current.Montant == 0m)
		{
			txtMontant.ErrorText = "Montant invalide.";
			return false;
		}
		if (string.IsNullOrEmpty(_current.CodeActivite))
		{
			txtCodeActivite.ErrorText = "Code activité invalide.";
			return false;
		}
		if (_current.Prorata < 0m || _current.Prorata > 100m)
		{
			txtProrata.ErrorText = "Prorata invalide.";
			return false;
		}
		return true;
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
		this.txtNumeroMouvement = new DevExpress.XtraEditors.TextEdit();
		this.txtProrata = new DevExpress.XtraEditors.TextEdit();
		this.btnEnregistrer = new DevExpress.XtraEditors.SimpleButton();
		this.btnAnnuler = new DevExpress.XtraEditors.SimpleButton();
		this.txtNumeroPiece = new DevExpress.XtraEditors.TextEdit();
		this.txtMontant = new DevExpress.XtraEditors.TextEdit();
		this.txtTaux = new DevExpress.XtraEditors.TextEdit();
		this.txtAssiette = new DevExpress.XtraEditors.TextEdit();
		this.txtTiersIntitule = new DevExpress.XtraEditors.TextEdit();
		this.txtTiersIdentifiant = new DevExpress.XtraEditors.TextEdit();
		this.txtDomaine = new DevExpress.XtraEditors.ImageComboBoxEdit();
		this.txtTypeLigne = new DevExpress.XtraEditors.ImageComboBoxEdit();
		this.txtDate = new DevExpress.XtraEditors.DateEdit();
		this.txtTypePayement = new DevExpress.XtraEditors.ImageComboBoxEdit();
		this.txtTiersCode = new DevExpress.XtraEditors.SearchLookUpEdit();
		this.searchLookUpEdit2View = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtErpTaxeCode = new DevExpress.XtraEditors.SearchLookUpEdit();
		this.searchLookUpEdit1View = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtCodeActivite = new DevExpress.XtraEditors.SearchLookUpEdit();
		this.gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
		this.txtDateMouvement = new DevExpress.XtraEditors.DateEdit();
		this.Root = new DevExpress.XtraLayout.LayoutControlGroup();
		this.emptySpaceItem1 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.layoutControlItem5 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem9 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem6 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem3 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem2 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem4 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem13 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem12 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem14 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem10 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem11 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem8 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem15 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem16 = new DevExpress.XtraLayout.LayoutControlItem();
		this.emptySpaceItem2 = new DevExpress.XtraLayout.EmptySpaceItem();
		this.layoutControlItem17 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem18 = new DevExpress.XtraLayout.LayoutControlItem();
		this.layoutControlItem7 = new DevExpress.XtraLayout.LayoutControlItem();
		this.txtDesignationDocument = new DevExpress.XtraEditors.TextEdit();
		this.layoutControlItem19 = new DevExpress.XtraLayout.LayoutControlItem();
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).BeginInit();
		this.layoutControl1.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.txtNumeroMouvement.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtProrata.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtNumeroPiece.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtMontant.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtTaux.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtAssiette.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtTiersIntitule.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtTiersIdentifiant.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDomaine.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtTypeLigne.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtTypePayement.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtTiersCode.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.searchLookUpEdit2View).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtErpTaxeCode.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.searchLookUpEdit1View).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtCodeActivite.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.gridView1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateMouvement.Properties.CalendarTimeProperties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateMouvement.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.Root).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem9).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem6).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem13).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem12).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem14).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem10).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem11).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem8).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem15).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem16).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem17).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem18).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem7).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.txtDesignationDocument.Properties).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem19).BeginInit();
		base.SuspendLayout();
		this.layoutControl1.Controls.Add(this.txtDesignationDocument);
		this.layoutControl1.Controls.Add(this.txtNumeroMouvement);
		this.layoutControl1.Controls.Add(this.txtProrata);
		this.layoutControl1.Controls.Add(this.btnEnregistrer);
		this.layoutControl1.Controls.Add(this.btnAnnuler);
		this.layoutControl1.Controls.Add(this.txtNumeroPiece);
		this.layoutControl1.Controls.Add(this.txtMontant);
		this.layoutControl1.Controls.Add(this.txtTaux);
		this.layoutControl1.Controls.Add(this.txtAssiette);
		this.layoutControl1.Controls.Add(this.txtTiersIntitule);
		this.layoutControl1.Controls.Add(this.txtTiersIdentifiant);
		this.layoutControl1.Controls.Add(this.txtDomaine);
		this.layoutControl1.Controls.Add(this.txtTypeLigne);
		this.layoutControl1.Controls.Add(this.txtDate);
		this.layoutControl1.Controls.Add(this.txtTypePayement);
		this.layoutControl1.Controls.Add(this.txtTiersCode);
		this.layoutControl1.Controls.Add(this.txtErpTaxeCode);
		this.layoutControl1.Controls.Add(this.txtCodeActivite);
		this.layoutControl1.Controls.Add(this.txtDateMouvement);
		this.layoutControl1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.layoutControl1.Location = new System.Drawing.Point(0, 0);
		this.layoutControl1.Name = "layoutControl1";
		this.layoutControl1.Root = this.Root;
		this.layoutControl1.Size = new System.Drawing.Size(517, 266);
		this.layoutControl1.TabIndex = 0;
		this.layoutControl1.Text = "layoutControl1";
		this.txtNumeroMouvement.Location = new System.Drawing.Point(82, 122);
		this.txtNumeroMouvement.Name = "txtNumeroMouvement";
		this.txtNumeroMouvement.Size = new System.Drawing.Size(174, 20);
		this.txtNumeroMouvement.StyleController = this.layoutControl1;
		this.txtNumeroMouvement.TabIndex = 21;
		this.txtProrata.Location = new System.Drawing.Point(82, 218);
		this.txtProrata.Name = "txtProrata";
		this.txtProrata.Size = new System.Drawing.Size(174, 20);
		this.txtProrata.StyleController = this.layoutControl1;
		this.txtProrata.TabIndex = 20;
		this.btnEnregistrer.Location = new System.Drawing.Point(319, 242);
		this.btnEnregistrer.Name = "btnEnregistrer";
		this.btnEnregistrer.Size = new System.Drawing.Size(96, 22);
		this.btnEnregistrer.StyleController = this.layoutControl1;
		this.btnEnregistrer.TabIndex = 17;
		this.btnEnregistrer.Text = "Enregistrer";
		this.btnAnnuler.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.btnAnnuler.Location = new System.Drawing.Point(419, 242);
		this.btnAnnuler.Name = "btnAnnuler";
		this.btnAnnuler.Size = new System.Drawing.Size(96, 22);
		this.btnAnnuler.StyleController = this.layoutControl1;
		this.btnAnnuler.TabIndex = 16;
		this.btnAnnuler.Text = "Annuler";
		this.txtNumeroPiece.Location = new System.Drawing.Point(82, 74);
		this.txtNumeroPiece.Name = "txtNumeroPiece";
		this.txtNumeroPiece.Size = new System.Drawing.Size(174, 20);
		this.txtNumeroPiece.StyleController = this.layoutControl1;
		this.txtNumeroPiece.TabIndex = 15;
		this.txtMontant.Location = new System.Drawing.Point(340, 170);
		this.txtMontant.Name = "txtMontant";
		this.txtMontant.Size = new System.Drawing.Size(175, 20);
		this.txtMontant.StyleController = this.layoutControl1;
		this.txtMontant.TabIndex = 14;
		this.txtTaux.Location = new System.Drawing.Point(340, 146);
		this.txtTaux.Name = "txtTaux";
		this.txtTaux.Size = new System.Drawing.Size(175, 20);
		this.txtTaux.StyleController = this.layoutControl1;
		this.txtTaux.TabIndex = 13;
		this.txtAssiette.Location = new System.Drawing.Point(82, 170);
		this.txtAssiette.Name = "txtAssiette";
		this.txtAssiette.Size = new System.Drawing.Size(174, 20);
		this.txtAssiette.StyleController = this.layoutControl1;
		this.txtAssiette.TabIndex = 12;
		this.txtTiersIntitule.Location = new System.Drawing.Point(82, 50);
		this.txtTiersIntitule.Name = "txtTiersIntitule";
		this.txtTiersIntitule.Properties.ReadOnly = true;
		this.txtTiersIntitule.Size = new System.Drawing.Size(433, 20);
		this.txtTiersIntitule.StyleController = this.layoutControl1;
		this.txtTiersIntitule.TabIndex = 7;
		this.txtTiersIdentifiant.Location = new System.Drawing.Point(340, 26);
		this.txtTiersIdentifiant.Name = "txtTiersIdentifiant";
		this.txtTiersIdentifiant.Properties.ReadOnly = true;
		this.txtTiersIdentifiant.Size = new System.Drawing.Size(175, 20);
		this.txtTiersIdentifiant.StyleController = this.layoutControl1;
		this.txtTiersIdentifiant.TabIndex = 5;
		this.txtDomaine.Location = new System.Drawing.Point(82, 2);
		this.txtDomaine.Name = "txtDomaine";
		this.txtDomaine.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDomaine.Size = new System.Drawing.Size(174, 20);
		this.txtDomaine.StyleController = this.layoutControl1;
		this.txtDomaine.TabIndex = 8;
		this.txtTypeLigne.Location = new System.Drawing.Point(340, 2);
		this.txtTypeLigne.Name = "txtTypeLigne";
		this.txtTypeLigne.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtTypeLigne.Size = new System.Drawing.Size(175, 20);
		this.txtTypeLigne.StyleController = this.layoutControl1;
		this.txtTypeLigne.TabIndex = 9;
		this.txtDate.EditValue = null;
		this.txtDate.Location = new System.Drawing.Point(340, 74);
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
		this.txtDate.Size = new System.Drawing.Size(175, 20);
		this.txtDate.StyleController = this.layoutControl1;
		this.txtDate.TabIndex = 10;
		this.txtTypePayement.Location = new System.Drawing.Point(340, 194);
		this.txtTypePayement.Name = "txtTypePayement";
		this.txtTypePayement.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtTypePayement.Size = new System.Drawing.Size(175, 20);
		this.txtTypePayement.StyleController = this.layoutControl1;
		this.txtTypePayement.TabIndex = 11;
		this.txtTiersCode.Location = new System.Drawing.Point(82, 26);
		this.txtTiersCode.Name = "txtTiersCode";
		this.txtTiersCode.Properties.AutoHeight = false;
		this.txtTiersCode.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtTiersCode.Properties.NullText = "";
		this.txtTiersCode.Properties.PopupView = this.searchLookUpEdit2View;
		this.txtTiersCode.Size = new System.Drawing.Size(174, 20);
		this.txtTiersCode.StyleController = this.layoutControl1;
		this.txtTiersCode.TabIndex = 4;
		this.searchLookUpEdit2View.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
		this.searchLookUpEdit2View.Name = "searchLookUpEdit2View";
		this.searchLookUpEdit2View.OptionsSelection.EnableAppearanceFocusedCell = false;
		this.searchLookUpEdit2View.OptionsView.ShowGroupPanel = false;
		this.txtErpTaxeCode.Location = new System.Drawing.Point(82, 146);
		this.txtErpTaxeCode.Name = "txtErpTaxeCode";
		this.txtErpTaxeCode.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtErpTaxeCode.Properties.NullText = "";
		this.txtErpTaxeCode.Properties.PopupView = this.searchLookUpEdit1View;
		this.txtErpTaxeCode.Size = new System.Drawing.Size(174, 20);
		this.txtErpTaxeCode.StyleController = this.layoutControl1;
		this.txtErpTaxeCode.TabIndex = 18;
		this.searchLookUpEdit1View.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
		this.searchLookUpEdit1View.Name = "searchLookUpEdit1View";
		this.searchLookUpEdit1View.OptionsSelection.EnableAppearanceFocusedCell = false;
		this.searchLookUpEdit1View.OptionsView.ShowGroupPanel = false;
		this.txtCodeActivite.Location = new System.Drawing.Point(82, 194);
		this.txtCodeActivite.Name = "txtCodeActivite";
		this.txtCodeActivite.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtCodeActivite.Properties.NullText = "";
		this.txtCodeActivite.Properties.PopupView = this.gridView1;
		this.txtCodeActivite.Size = new System.Drawing.Size(174, 20);
		this.txtCodeActivite.StyleController = this.layoutControl1;
		this.txtCodeActivite.TabIndex = 19;
		this.gridView1.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
		this.gridView1.Name = "gridView1";
		this.gridView1.OptionsSelection.EnableAppearanceFocusedCell = false;
		this.gridView1.OptionsView.ShowGroupPanel = false;
		this.txtDateMouvement.EditValue = null;
		this.txtDateMouvement.Location = new System.Drawing.Point(340, 122);
		this.txtDateMouvement.Name = "txtDateMouvement";
		this.txtDateMouvement.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDateMouvement.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[1]
		{
			new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)
		});
		this.txtDateMouvement.Properties.Mask.EditMask = "ddMMyy";
		this.txtDateMouvement.Properties.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.DateTimeAdvancingCaret;
		this.txtDateMouvement.Size = new System.Drawing.Size(175, 20);
		this.txtDateMouvement.StyleController = this.layoutControl1;
		this.txtDateMouvement.TabIndex = 22;
		this.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
		this.Root.GroupBordersVisible = false;
		this.Root.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[21]
		{
			this.emptySpaceItem1, this.layoutControlItem5, this.layoutControlItem9, this.layoutControlItem6, this.layoutControlItem3, this.layoutControlItem1, this.layoutControlItem2, this.layoutControlItem4, this.layoutControlItem13, this.layoutControlItem12,
			this.layoutControlItem14, this.layoutControlItem10, this.layoutControlItem11, this.layoutControlItem8, this.layoutControlItem15, this.layoutControlItem16, this.emptySpaceItem2, this.layoutControlItem17, this.layoutControlItem18, this.layoutControlItem7,
			this.layoutControlItem19
		});
		this.Root.Name = "Root";
		this.Root.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 0);
		this.Root.Size = new System.Drawing.Size(517, 266);
		this.Root.TextVisible = false;
		this.emptySpaceItem1.AllowHotTrack = false;
		this.emptySpaceItem1.Location = new System.Drawing.Point(0, 240);
		this.emptySpaceItem1.Name = "emptySpaceItem1";
		this.emptySpaceItem1.Size = new System.Drawing.Size(317, 26);
		this.emptySpaceItem1.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem5.Control = this.txtDomaine;
		this.layoutControlItem5.Location = new System.Drawing.Point(0, 0);
		this.layoutControlItem5.Name = "layoutControlItem5";
		this.layoutControlItem5.Size = new System.Drawing.Size(258, 24);
		this.layoutControlItem5.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem5.Text = "Domaine";
		this.layoutControlItem5.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem5.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem5.TextToControlDistance = 0;
		this.layoutControlItem9.Control = this.txtAssiette;
		this.layoutControlItem9.Location = new System.Drawing.Point(0, 168);
		this.layoutControlItem9.Name = "layoutControlItem9";
		this.layoutControlItem9.Size = new System.Drawing.Size(258, 24);
		this.layoutControlItem9.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem9.Text = "Assiette";
		this.layoutControlItem9.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem9.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem9.TextToControlDistance = 0;
		this.layoutControlItem6.Control = this.txtTypeLigne;
		this.layoutControlItem6.Location = new System.Drawing.Point(258, 0);
		this.layoutControlItem6.Name = "layoutControlItem6";
		this.layoutControlItem6.Size = new System.Drawing.Size(259, 24);
		this.layoutControlItem6.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem6.Text = "Type";
		this.layoutControlItem6.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem6.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem6.TextToControlDistance = 0;
		this.layoutControlItem3.Control = this.txtTiersCode;
		this.layoutControlItem3.Location = new System.Drawing.Point(0, 24);
		this.layoutControlItem3.Name = "layoutControlItem3";
		this.layoutControlItem3.Size = new System.Drawing.Size(258, 24);
		this.layoutControlItem3.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem3.Text = "Tiers";
		this.layoutControlItem3.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem3.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem3.TextToControlDistance = 0;
		this.layoutControlItem1.Control = this.txtTiersIdentifiant;
		this.layoutControlItem1.Location = new System.Drawing.Point(258, 24);
		this.layoutControlItem1.Name = "layoutControlItem1";
		this.layoutControlItem1.Size = new System.Drawing.Size(259, 24);
		this.layoutControlItem1.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem1.Text = "Identifiant";
		this.layoutControlItem1.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem1.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem1.TextToControlDistance = 0;
		this.layoutControlItem2.Control = this.txtTiersIntitule;
		this.layoutControlItem2.Location = new System.Drawing.Point(0, 48);
		this.layoutControlItem2.Name = "layoutControlItem2";
		this.layoutControlItem2.Size = new System.Drawing.Size(517, 24);
		this.layoutControlItem2.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem2.Text = "Intitulé";
		this.layoutControlItem2.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem2.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem2.TextToControlDistance = 0;
		this.layoutControlItem4.Control = this.btnAnnuler;
		this.layoutControlItem4.Location = new System.Drawing.Point(417, 240);
		this.layoutControlItem4.MaxSize = new System.Drawing.Size(100, 26);
		this.layoutControlItem4.MinSize = new System.Drawing.Size(100, 26);
		this.layoutControlItem4.Name = "layoutControlItem4";
		this.layoutControlItem4.Size = new System.Drawing.Size(100, 26);
		this.layoutControlItem4.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem4.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem4.TextVisible = false;
		this.layoutControlItem13.Control = this.btnEnregistrer;
		this.layoutControlItem13.Location = new System.Drawing.Point(317, 240);
		this.layoutControlItem13.MaxSize = new System.Drawing.Size(100, 26);
		this.layoutControlItem13.MinSize = new System.Drawing.Size(100, 26);
		this.layoutControlItem13.Name = "layoutControlItem13";
		this.layoutControlItem13.Size = new System.Drawing.Size(100, 26);
		this.layoutControlItem13.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
		this.layoutControlItem13.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem13.TextVisible = false;
		this.layoutControlItem12.Control = this.txtNumeroPiece;
		this.layoutControlItem12.Location = new System.Drawing.Point(0, 72);
		this.layoutControlItem12.Name = "layoutControlItem12";
		this.layoutControlItem12.Size = new System.Drawing.Size(258, 24);
		this.layoutControlItem12.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem12.Text = "N° Pièce";
		this.layoutControlItem12.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem12.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem12.TextToControlDistance = 0;
		this.layoutControlItem14.Control = this.txtErpTaxeCode;
		this.layoutControlItem14.Location = new System.Drawing.Point(0, 144);
		this.layoutControlItem14.Name = "layoutControlItem14";
		this.layoutControlItem14.Size = new System.Drawing.Size(258, 24);
		this.layoutControlItem14.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem14.Text = "Taxe";
		this.layoutControlItem14.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem14.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem14.TextToControlDistance = 0;
		this.layoutControlItem10.Control = this.txtTaux;
		this.layoutControlItem10.Location = new System.Drawing.Point(258, 144);
		this.layoutControlItem10.Name = "layoutControlItem10";
		this.layoutControlItem10.Size = new System.Drawing.Size(259, 24);
		this.layoutControlItem10.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem10.Text = "Taux";
		this.layoutControlItem10.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem10.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem10.TextToControlDistance = 0;
		this.layoutControlItem11.Control = this.txtMontant;
		this.layoutControlItem11.Location = new System.Drawing.Point(258, 168);
		this.layoutControlItem11.Name = "layoutControlItem11";
		this.layoutControlItem11.Size = new System.Drawing.Size(259, 24);
		this.layoutControlItem11.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem11.Text = "Montant";
		this.layoutControlItem11.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem11.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem11.TextToControlDistance = 0;
		this.layoutControlItem8.Control = this.txtTypePayement;
		this.layoutControlItem8.Location = new System.Drawing.Point(258, 192);
		this.layoutControlItem8.Name = "layoutControlItem8";
		this.layoutControlItem8.Size = new System.Drawing.Size(259, 24);
		this.layoutControlItem8.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem8.Text = "Payement";
		this.layoutControlItem8.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem8.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem8.TextToControlDistance = 0;
		this.layoutControlItem15.Control = this.txtCodeActivite;
		this.layoutControlItem15.Location = new System.Drawing.Point(0, 192);
		this.layoutControlItem15.Name = "layoutControlItem15";
		this.layoutControlItem15.Size = new System.Drawing.Size(258, 24);
		this.layoutControlItem15.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem15.Text = "Code activité";
		this.layoutControlItem15.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem15.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem15.TextToControlDistance = 0;
		this.layoutControlItem16.Control = this.txtProrata;
		this.layoutControlItem16.Location = new System.Drawing.Point(0, 216);
		this.layoutControlItem16.Name = "layoutControlItem16";
		this.layoutControlItem16.Size = new System.Drawing.Size(258, 24);
		this.layoutControlItem16.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem16.Text = "Prorata";
		this.layoutControlItem16.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem16.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem16.TextToControlDistance = 0;
		this.emptySpaceItem2.AllowHotTrack = false;
		this.emptySpaceItem2.Location = new System.Drawing.Point(258, 216);
		this.emptySpaceItem2.Name = "emptySpaceItem2";
		this.emptySpaceItem2.Size = new System.Drawing.Size(259, 24);
		this.emptySpaceItem2.TextSize = new System.Drawing.Size(0, 0);
		this.layoutControlItem17.Control = this.txtNumeroMouvement;
		this.layoutControlItem17.Location = new System.Drawing.Point(0, 120);
		this.layoutControlItem17.Name = "layoutControlItem17";
		this.layoutControlItem17.Size = new System.Drawing.Size(258, 24);
		this.layoutControlItem17.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem17.Text = "N° mvt.";
		this.layoutControlItem17.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem17.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem17.TextToControlDistance = 0;
		this.layoutControlItem18.Control = this.txtDateMouvement;
		this.layoutControlItem18.Location = new System.Drawing.Point(258, 120);
		this.layoutControlItem18.Name = "layoutControlItem18";
		this.layoutControlItem18.Size = new System.Drawing.Size(259, 24);
		this.layoutControlItem18.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem18.Text = "Date mvt.";
		this.layoutControlItem18.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem18.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem18.TextToControlDistance = 0;
		this.layoutControlItem7.Control = this.txtDate;
		this.layoutControlItem7.Location = new System.Drawing.Point(258, 72);
		this.layoutControlItem7.Name = "layoutControlItem7";
		this.layoutControlItem7.Size = new System.Drawing.Size(259, 24);
		this.layoutControlItem7.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem7.Text = "Date pièce";
		this.layoutControlItem7.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem7.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem7.TextToControlDistance = 0;
		this.txtDesignationDocument.Location = new System.Drawing.Point(82, 98);
		this.txtDesignationDocument.Name = "txtDesignationDocument";
		this.txtDesignationDocument.Size = new System.Drawing.Size(433, 20);
		this.txtDesignationDocument.StyleController = this.layoutControl1;
		this.txtDesignationDocument.TabIndex = 23;
		this.layoutControlItem19.Control = this.txtDesignationDocument;
		this.layoutControlItem19.Location = new System.Drawing.Point(0, 96);
		this.layoutControlItem19.Name = "layoutControlItem19";
		this.layoutControlItem19.Size = new System.Drawing.Size(517, 24);
		this.layoutControlItem19.Spacing = new DevExpress.XtraLayout.Utils.Padding(5, 0, 0, 0);
		this.layoutControlItem19.Text = "Désignation";
		this.layoutControlItem19.TextAlignMode = DevExpress.XtraLayout.TextAlignModeItem.CustomSize;
		this.layoutControlItem19.TextSize = new System.Drawing.Size(75, 20);
		this.layoutControlItem19.TextToControlDistance = 0;
		base.AcceptButton = this.btnEnregistrer;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.CancelButton = this.btnAnnuler;
		base.ClientSize = new System.Drawing.Size(517, 266);
		base.Controls.Add(this.layoutControl1);
		base.IconOptions.ShowIcon = false;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "FrmAddLigneDeclarationTvaEncaissement";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Ajouter ligne déclaration TVA/Encaissement";
		((System.ComponentModel.ISupportInitialize)this.layoutControl1).EndInit();
		this.layoutControl1.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.txtNumeroMouvement.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtProrata.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtNumeroPiece.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtMontant.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtTaux.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtAssiette.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtTiersIntitule.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtTiersIdentifiant.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDomaine.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtTypeLigne.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDate.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtTypePayement.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtTiersCode.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.searchLookUpEdit2View).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtErpTaxeCode.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.searchLookUpEdit1View).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtCodeActivite.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.gridView1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateMouvement.Properties.CalendarTimeProperties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDateMouvement.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.Root).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem5).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem9).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem6).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem3).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem4).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem13).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem12).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem14).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem10).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem11).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem8).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem15).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem16).EndInit();
		((System.ComponentModel.ISupportInitialize)this.emptySpaceItem2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem17).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem18).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem7).EndInit();
		((System.ComponentModel.ISupportInitialize)this.txtDesignationDocument.Properties).EndInit();
		((System.ComponentModel.ISupportInitialize)this.layoutControlItem19).EndInit();
		base.ResumeLayout(false);
	}

	private void Initialize()
	{
		InitDomaine();
		InitTypeLigne();
		InitTiers();
		InitAssiette();
		InitErpTaxe();
		InitTaux();
		InitProrata();
		InitMontant();
		InitTypePayement();
		InitCodeActivite();
	}

	private void InitCodeActivite()
	{
		txtCodeActivite.Properties.DataSource = _controller.GetAllCodeActivite();
		txtCodeActivite.Properties.ValueMember = "Code";
		txtCodeActivite.Properties.DisplayMember = "Code";
		txtCodeActivite.Properties.View.Columns.Add(new GridColumn
		{
			FieldName = "Code",
			Caption = "Code",
			Visible = true
		});
		txtCodeActivite.Properties.View.Columns.Add(new GridColumn
		{
			FieldName = "Intitule",
			Caption = "Intitulé",
			Visible = true
		});
	}

	private void InitDomaine()
	{
		txtDomaine.Properties.AddEnum<LigneDeclarationTvaEncaissementDomaine>();
	}

	private void InitTypeLigne()
	{
		txtTypeLigne.Properties.AddEnum<LigneDeclarationTvaEncaissementEntityType>();
		txtTypeLigne.ReadOnly = true;
	}

	private void InitErpTaxe()
	{
		txtErpTaxeCode.Properties.View.Columns.Add(new GridColumn
		{
			FieldName = "Code",
			Visible = true
		});
		txtErpTaxeCode.Properties.View.Columns.Add(new GridColumn
		{
			FieldName = "Intitule",
			Visible = true
		});
		txtErpTaxeCode.Properties.View.Columns.Add(new GridColumn
		{
			FieldName = "Sens",
			Visible = true
		});
		txtErpTaxeCode.Properties.View.Columns.Add(new GridColumn
		{
			FieldName = "CompteGeneral",
			Visible = true
		});
		txtErpTaxeCode.Properties.View.Columns.Add(new GridColumn
		{
			FieldName = "Taux",
			Visible = true,
			Tag = new
			{
				Default = true
			}
		});
		txtErpTaxeCode.Properties.DataSource = (from x in _controller.GetAllErpTaxe()
			where x.Sens == ErpSensTaxe.Deductible
			select x).ToList();
		txtErpTaxeCode.Properties.DisplayMember = "Intitule";
		txtErpTaxeCode.Properties.ValueMember = "Code";
	}

	private void InitTiers()
	{
		txtTiersCode.Properties.View.Columns.Add(new GridColumn
		{
			Caption = "Numéro",
			FieldName = "Numero",
			Visible = true
		});
		txtTiersCode.Properties.View.Columns.Add(new GridColumn
		{
			Caption = "Abrégé",
			FieldName = "Abrege",
			Visible = true
		});
		txtTiersCode.Properties.View.Columns.Add(new GridColumn
		{
			Caption = "Intitulé",
			FieldName = "Intitule",
			Visible = true
		});
		txtTiersCode.Properties.DisplayMember = "Numero";
		txtTiersCode.Properties.ValueMember = "Numero";
		txtTiersCode.Properties.DataSource = _controller.GetAllFournisseur();
	}

	private void InitAssiette()
	{
		txtAssiette.Tag = new
		{
			Default = true
		};
		txtAssiette.Properties.Mask.UseMaskAsDisplayFormat = true;
		txtAssiette.Properties.Mask.ShowPlaceHolders = false;
		txtAssiette.Properties.Appearance.TextOptions.HAlignment = HorzAlignment.Far;
		txtAssiette.Properties.Appearance.Options.UseTextOptions = true;
		txtAssiette.Properties.Mask.MaskType = MaskType.Numeric;
		txtAssiette.Properties.Mask.EditMask = _deviseFormat;
		txtAssiette.Properties.MaxLength = 18;
	}

	private void InitTaux()
	{
		txtTaux.Properties.Mask.UseMaskAsDisplayFormat = true;
		txtTaux.Properties.Mask.ShowPlaceHolders = false;
		txtTaux.Properties.Appearance.TextOptions.HAlignment = HorzAlignment.Far;
		txtTaux.Properties.Appearance.Options.UseTextOptions = true;
		txtTaux.Properties.Mask.MaskType = MaskType.Numeric;
		txtTaux.Properties.Mask.EditMask = "N3";
		txtTaux.Properties.MaxLength = 18;
		txtTaux.Properties.ReadOnly = true;
	}

	private void InitProrata()
	{
		txtProrata.Properties.Mask.UseMaskAsDisplayFormat = true;
		txtProrata.Properties.Mask.ShowPlaceHolders = false;
		txtProrata.Properties.Appearance.TextOptions.HAlignment = HorzAlignment.Far;
		txtProrata.Properties.Appearance.Options.UseTextOptions = true;
		txtProrata.Properties.Mask.MaskType = MaskType.Numeric;
		txtProrata.Properties.Mask.EditMask = "N3";
		txtProrata.Properties.MaxLength = 18;
	}

	private void InitMontant()
	{
		txtMontant.Tag = new
		{
			Default = true
		};
		txtMontant.Properties.Mask.UseMaskAsDisplayFormat = true;
		txtMontant.Properties.Mask.ShowPlaceHolders = false;
		txtMontant.Properties.Appearance.TextOptions.HAlignment = HorzAlignment.Far;
		txtMontant.Properties.Appearance.Options.UseTextOptions = true;
		txtMontant.Properties.Mask.MaskType = MaskType.Numeric;
		txtMontant.Properties.Mask.EditMask = _deviseFormat;
		txtMontant.Properties.MaxLength = 18;
		txtMontant.Properties.ReadOnly = true;
	}

	private void InitTypePayement()
	{
		txtTypePayement.Properties.Items.Add(new ImageComboBoxItem("Chèque", ReglementType.Cheque));
		txtTypePayement.Properties.Items.Add(new ImageComboBoxItem("Traite", ReglementType.Traite));
		txtTypePayement.Properties.Items.Add(new ImageComboBoxItem("Virement", ReglementType.Virement));
		txtTypePayement.Properties.Items.Add(new ImageComboBoxItem("Espèce", ReglementType.Espece));
	}
}
