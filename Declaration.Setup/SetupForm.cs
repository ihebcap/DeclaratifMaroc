using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Declaration.Setup.Models;
using Declaration.Setup.Services;

namespace Declaration.Setup;

/// <summary>
/// Formulaire couvrant installation et mise à jour (TASK-115), restructuré en assistant
/// séquentiel (wizard, TASK-115 §Inclus point 6bis) : chaque section (dossier, connexions SQL,
/// Sage &amp; licence, service Windows, prérequis, récapitulatif) est une étape, avec navigation
/// Suivant/Précédent et validation par étape (plus de <c>TabControl</c> à onglets librement
/// navigables). Le mode install/mise à jour est déterminé par <see cref="InstallDetector"/> dès
/// que le dossier cible est renseigné (étape 1), et pilote le pré-remplissage (jamais de secret en
/// clair, cf. <see cref="ConnectionsFileService"/>). Portée strictement présentation/navigation :
/// aucune logique métier (<see cref="ConnectionsFileService"/>, <see cref="WinSwServiceManager"/>,
/// <see cref="PrerequisiteChecker"/>, etc.) n'est modifiée par cette refonte.
/// </summary>
public sealed class SetupForm : Form
{
    private static readonly string[] StepTitles =
    {
        "Dossier d'installation",
        "Connexions SQL",
        "Sage & Licence ApLicence",
        "Service Windows",
        "Prérequis",
        "Récapitulatif"
    };

    private readonly TextBox _txtInstallFolder = new() { Width = 380 };
    private readonly Label _lblMode = new() { AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };

    // TASK-122 : une seule base physique sert à la fois GRF et Persistance -> un seul groupe de
    // saisie, dupliqué vers SetupData.Grf/Persistence à la lecture (cf. ReadDataFromForm).
    private readonly ConnectionGroup _connection = new("Connexion SQL (GRF + Persistance)");
    private readonly Label _lblConnectionDivergence = new()
    {
        AutoSize = true,
        Visible = false,
        MaximumSize = new Size(560, 0),
        ForeColor = Color.DarkOrange
    };

    private readonly ComboBox _cboSageVersion = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly TextBox _txtApLicenceServerAddress = new() { Width = 150, Text = "127.0.0.1" };
    private readonly NumericUpDown _numApLicenceServerPort = new() { Width = 80, Minimum = 1, Maximum = 65535, Value = 8003 };

    private readonly NumericUpDown _numPort = new() { Width = 80, Minimum = 1, Maximum = 65535, Value = 5000 };

    private readonly Label _lblNet48Status = new() { AutoSize = true };
    private readonly Label _lblSageOmStatus = new() { AutoSize = true };
    private readonly CheckBox _chkConfirmSageOm = new()
    {
        AutoSize = true,
        Text = "Je confirme que Sage OM (composants Sage 100) est installé et configuré sur cette machine."
    };

    private readonly Label _lblRecap = new() { AutoSize = true, MaximumSize = new Size(560, 0) };

    private readonly Label _lblStepIndicator = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 10, FontStyle.Bold)
    };

    private readonly Button _btnPrevious = new() { Text = "< Précédent", Width = 120, Height = 32 };

    // Vert signature TASK-120 (#178a4c) : seul le bouton principal est restylé (périmètre TASK-122).
    // Bouton unique "Suivant" pendant la navigation, devient "Installer"/"Mettre à jour" sur la
    // dernière étape (récapitulatif) — remplace l'ancien _btnInstallUpdate fixe en pied de page.
    private readonly Button _btnNext = new()
    {
        Text = "Suivant >",
        Width = 140,
        Height = 32,
        FlatStyle = FlatStyle.Flat,
        BackColor = ColorTranslator.FromHtml("#178a4c"),
        ForeColor = Color.White,
        FlatAppearance = { BorderSize = 0 }
    };
    private readonly Button _btnCancel = new() { Text = "Annuler", Width = 100, Height = 32 };

    private readonly Panel[] _stepPanels = new Panel[6];
    private int _currentStep;
    private bool _isUpdateMode;
    private int _originalPort;

    public SetupForm()
    {
        Text = "Déclaratif Maroc — Installation / Mise à jour";
        Width = 660;
        Height = 640;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        _cboSageVersion.Items.AddRange(new object[]
        {
            SageVersion.V7, SageVersion.V9, SageVersion.V10, SageVersion.V12
        });
        _cboSageVersion.Format += (_, e) => e.Value = e.ListItem is SageVersion v ? v.DisplayLabel() : e.Value;
        _cboSageVersion.SelectedItem = SageVersion.V10;

        BuildLayout();
        WireEvents();

        _txtInstallFolder.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "APBS",
            "Declaratif Maroc");
        RefreshMode();
        UpdateStepChrome();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 0: bandeau de marque
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 1: indicateur d'étape
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 2: contenu de l'étape courante
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 3: pied de page (copyright + navigation)

        root.Controls.Add(BuildHeaderPanel(), 0, 0);

        var indicatorPadded = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 10, 12, 4), AutoSize = true };
        indicatorPadded.Controls.Add(_lblStepIndicator);
        root.Controls.Add(indicatorPadded, 0, 1);

        var stepPadded = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
        stepPadded.Controls.Add(BuildStepHost());
        root.Controls.Add(stepPadded, 0, 2);

        var footerPadded = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 12), AutoSize = true };
        footerPadded.Controls.Add(BuildFooterPanel());
        root.Controls.Add(footerPadded, 0, 3);

        Controls.Add(root);
    }

    /// <summary>
    /// Bandeau de marque (TASK-122, charte TASK-120) : fond `#1a1a1a`, icône DM + titre en vert
    /// signature. Fixe sur toutes les étapes du wizard (TASK-115 §6bis).
    /// </summary>
    private Control BuildHeaderPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 56,
            BackColor = ColorTranslator.FromHtml("#1a1a1a")
        };

        var icon = new PictureBox
        {
            Width = 36,
            Height = 36,
            Location = new Point(12, 10),
            SizeMode = PictureBoxSizeMode.Zoom
        };
        try
        {
            icon.Image = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath)?.ToBitmap();
        }
        catch (Exception)
        {
            // Icône non extractible (ex. exécution hors .exe en dev) : bandeau sans icône, non bloquant.
        }

        var title = new Label
        {
            Text = "Déclaratif Maroc — Installation / Mise à jour",
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = ColorTranslator.FromHtml("#3ddc84"),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(58, 16)
        };

        panel.Controls.Add(icon);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildStepHost()
    {
        _stepPanels[0] = BuildFolderStepPanel();
        _stepPanels[1] = BuildConnectionsStepPanel();
        _stepPanels[2] = BuildSageLicenceStepPanel();
        _stepPanels[3] = BuildServiceStepPanel();
        _stepPanels[4] = BuildPrerequisitesStepPanel();
        _stepPanels[5] = BuildRecapStepPanel();

        var host = new Panel { Dock = DockStyle.Fill };
        foreach (var panel in _stepPanels)
        {
            panel.Dock = DockStyle.Fill;
            panel.Visible = false;
            host.Controls.Add(panel);
        }
        _stepPanels[0].Visible = true;
        return host;
    }

    /// <summary>
    /// Héberge le contenu d'une étape en le centrant horizontalement dans la zone disponible
    /// (demande PO du 19/07/2026) — le contenu garde sa largeur naturelle/fixe, seul son
    /// positionnement s'adapte à la taille de la fenêtre.
    /// </summary>
    private static Panel CenterHorizontally(Control content)
    {
        var host = new Panel { AutoScroll = true, Padding = new Padding(0, 12, 0, 0) };
        content.Anchor = AnchorStyles.Top;
        host.Controls.Add(content);

        void Reposition()
        {
            var left = Math.Max(0, (host.ClientSize.Width - content.Width) / 2);
            if (content.Left != left)
            {
                content.Left = left;
            }
        }

        host.Resize += (_, _) => Reposition();
        content.SizeChanged += (_, _) => Reposition();
        return host;
    }

    private Panel BuildFolderStepPanel()
    {
        var flow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        var row = new FlowLayoutPanel { AutoSize = true };
        var btnBrowse = new Button { Text = "Parcourir...", AutoSize = true };
        btnBrowse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { SelectedPath = _txtInstallFolder.Text };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _txtInstallFolder.Text = dialog.SelectedPath;
            }
        };

        row.Controls.Add(new Label { Text = "Dossier d'installation :", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        row.Controls.Add(_txtInstallFolder);
        row.Controls.Add(btnBrowse);
        flow.Controls.Add(row);
        flow.Controls.Add(_lblMode);
        return CenterHorizontally(flow);
    }

    private Panel BuildConnectionsStepPanel()
    {
        var flow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        flow.Controls.Add(_connection.GroupBox);
        flow.Controls.Add(_lblConnectionDivergence);
        return CenterHorizontally(flow);
    }

    private Panel BuildSageLicenceStepPanel()
    {
        var flow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };

        var versionGroup = new GroupBox { Text = "Version Sage 100 installée", AutoSize = true, Width = 580 };
        var versionTable = LabeledTable(("Version", _cboSageVersion));
        versionTable.Controls.Add(new Label
        {
            Text = "Sage 100 v8 non disponible (composant non fourni à ce jour).",
            AutoSize = true,
            ForeColor = Color.DimGray
        }, 1, 1);
        versionGroup.Controls.Add(versionTable);
        flow.Controls.Add(versionGroup);

        var licenceGroup = new GroupBox { Text = "Licence ApLicence", AutoSize = true, Width = 580 };
        var licenceTable = LabeledTable(
            ("Adresse serveur", _txtApLicenceServerAddress),
            ("Port serveur", _numApLicenceServerPort));
        licenceGroup.Controls.Add(licenceTable);
        flow.Controls.Add(licenceGroup);

        return CenterHorizontally(flow);
    }

    private Panel BuildServiceStepPanel()
    {
        var flow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };

        var group = new GroupBox { Text = "Écoute HTTP", AutoSize = true, Width = 580 };
        var table = LabeledTable(("Port", _numPort));
        group.Controls.Add(table);
        flow.Controls.Add(group);

        flow.Controls.Add(new Label
        {
            Text = $"Nom du service Windows : {WinSwServiceManager.ServiceName} ({WinSwServiceManager.ServiceId})",
            AutoSize = true,
            Margin = new Padding(3, 12, 3, 3)
        });

        return CenterHorizontally(flow);
    }

    private Panel BuildPrerequisitesStepPanel()
    {
        var group = new GroupBox { Text = "Prérequis", AutoSize = true, Width = 580 };
        var flow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        flow.Controls.Add(_lblNet48Status);
        flow.Controls.Add(_lblSageOmStatus);
        flow.Controls.Add(_chkConfirmSageOm);
        group.Controls.Add(flow);
        return CenterHorizontally(group);
    }

    private Panel BuildRecapStepPanel()
    {
        var group = new GroupBox { Text = "Récapitulatif avant installation / mise à jour", AutoSize = true, Width = 580 };
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Padding = new Padding(6) };
        inner.Controls.Add(_lblRecap);
        group.Controls.Add(inner);
        return CenterHorizontally(group);
    }

    /// <summary>
    /// Pied de page : copyright (rétrécit avec ellipse si besoin) + navigation. La navigation est
    /// ancrée à droite avec <c>WrapContents = false</c> pour garantir Annuler/Précédent/Suivant
    /// toujours sur une seule ligne, quelle que soit la largeur de la fenêtre (demande PO du
    /// 19/07/2026 — l'ancien layout Percent+AutoSize en TableLayoutPanel AutoSize était fragile).
    /// </summary>
    private Control BuildFooterPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 44, ColumnCount = 2, RowCount = 1 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblCopyright = new Label
        {
            Text = $"© {DateTime.Now.Year} APBS Groupe — Tous droits réservés",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            ForeColor = Color.DimGray
        };
        panel.Controls.Add(lblCopyright, 0, 0);

        var nav = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Dock = DockStyle.Right
        };
        nav.Controls.Add(_btnCancel);
        nav.Controls.Add(_btnPrevious);
        nav.Controls.Add(_btnNext);
        panel.Controls.Add(nav, 1, 0);

        return panel;
    }

    private static TableLayoutPanel LabeledTable(params (string Label, Control Field)[] rows)
    {
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = rows.Length };
        for (var i = 0; i < rows.Length; i++)
        {
            table.Controls.Add(new Label { Text = rows[i].Label + " :", AutoSize = true, Margin = new Padding(3, 8, 6, 3) }, 0, i);
            table.Controls.Add(rows[i].Field, 1, i);
        }

        return table;
    }

    private void WireEvents()
    {
        _txtInstallFolder.Leave += (_, _) => RefreshMode();
        _btnCancel.Click += (_, _) => Close();
        _btnPrevious.Click += (_, _) => GoToStep(_currentStep - 1);
        _btnNext.Click += async (_, _) => await OnNextOrFinishAsync();
    }

    private void GoToStep(int step)
    {
        if (step < 0 || step >= StepTitles.Length)
        {
            return;
        }

        _stepPanels[_currentStep].Visible = false;
        _currentStep = step;
        _stepPanels[_currentStep].Visible = true;
        UpdateStepChrome();
    }

    private void UpdateStepChrome()
    {
        _lblStepIndicator.Text = $"Étape {_currentStep + 1} / {StepTitles.Length} — {StepTitles[_currentStep]}";
        _btnPrevious.Enabled = _currentStep > 0;

        var isLast = _currentStep == StepTitles.Length - 1;
        _btnNext.Text = isLast ? (_isUpdateMode ? "Mettre à jour" : "Installer") : "Suivant >";

        if (isLast)
        {
            RefreshRecap();
        }
    }

    private async Task OnNextOrFinishAsync()
    {
        var isLast = _currentStep == StepTitles.Length - 1;
        if (!isLast)
        {
            if (!await ValidateStepAsync(_currentStep))
            {
                return;
            }

            GoToStep(_currentStep + 1);
            return;
        }

        await OnFinishAsync();
    }

    /// <summary>
    /// Validation des champs obligatoires de l'étape courante avant de pouvoir avancer (TASK-115
    /// §6bis) — plus de validation reportée en bloc au clic final comme avant cette refonte.
    /// </summary>
    private async Task<bool> ValidateStepAsync(int step)
    {
        switch (step)
        {
            case 0: // Dossier d'installation
                RefreshMode();
                if (string.IsNullOrWhiteSpace(_txtInstallFolder.Text.Trim()))
                {
                    MessageBox.Show(this, "Le dossier d'installation est obligatoire.", "Champ manquant", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                return true;

            case 1: // Connexions SQL
                var conn = _connection.ReadValues();
                if (string.IsNullOrWhiteSpace(conn.Server) || string.IsNullOrWhiteSpace(conn.Database) || string.IsNullOrWhiteSpace(conn.UserId))
                {
                    MessageBox.Show(this, "Serveur, base et utilisateur SQL sont obligatoires.", "Champs manquants", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                return true;

            case 2: // Sage & Licence ApLicence
                if (string.IsNullOrWhiteSpace(_txtApLicenceServerAddress.Text.Trim()))
                {
                    MessageBox.Show(this, "L'adresse du serveur de licence ApLicence est obligatoire.", "Champ manquant", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                return true;

            case 3: // Service Windows (port)
                var port = (int)_numPort.Value;
                // Mode mise à jour, port inchangé : le port est déjà occupé par le service en cours
                // (celui que l'on s'apprête à mettre à jour), pas par un tiers — IsFree() le
                // signalerait à tort comme indisponible et bloquerait le scénario de mise à jour le
                // plus courant. Un port modifié en mise à jour reste soumis au contrôle habituel.
                var portUnchanged = _isUpdateMode && port == _originalPort;
                if (!portUnchanged && !PortAvailability.IsFree(port))
                {
                    MessageBox.Show(this, $"Le port {port} est déjà utilisé par un autre processus. Choisissez un autre port.", "Port indisponible", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                return true;

            case 4: // Prérequis
                return await ValidatePrerequisitesStepAsync();

            default:
                return true;
        }
    }

    private async Task<bool> ValidatePrerequisitesStepAsync()
    {
        if (_lblSageOmStatus.Text.Contains("indétectable", StringComparison.OrdinalIgnoreCase) && !_chkConfirmSageOm.Checked)
        {
            MessageBox.Show(this, "Merci de confirmer explicitement la présence de Sage OM avant de continuer.", "Prérequis non confirmé", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (PrerequisiteChecker.CheckDotNetFramework48() == PrerequisiteStatus.Absent)
        {
            var installNow = MessageBox.Show(
                this,
                ".NET Framework 4.8 est absent (requis par le worker Sage). L'installer maintenant ? " +
                "Un redémarrage Windows peut être nécessaire après l'installation.",
                ".NET Framework 4.8 manquant",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (installNow == DialogResult.Yes)
            {
                Enabled = false;
                try
                {
                    var ok = await NetFrameworkInstaller.DownloadAndInstallSilentlyAsync();
                    if (!ok)
                    {
                        MessageBox.Show(this, "L'installation de .NET Framework 4.8 a échoué. Installez-le manuellement puis relancez ce setup.", "Échec", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    MessageBox.Show(this, ".NET Framework 4.8 installé. Un redémarrage Windows peut être requis avant que le worker fonctionne.", "Installé", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"L'installation de .NET Framework 4.8 a échoué ({ex.Message}). Installez-le manuellement puis relancez ce setup.", "Échec", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                finally
                {
                    Enabled = true;
                }
            }
        }

        return true;
    }

    private void RefreshMode()
    {
        var folder = _txtInstallFolder.Text.Trim();
        _isUpdateMode = !string.IsNullOrWhiteSpace(folder) && InstallDetector.IsExistingInstall(folder);

        if (_isUpdateMode)
        {
            _lblMode.Text = "Mode détecté : MISE À JOUR (connections.json existant dans ce dossier)";
            _lblMode.ForeColor = Color.DarkOrange;

            var root = ConnectionsFileService.LoadOrEmpty(folder);
            var data = ConnectionsFileService.ExtractForPrefill(root, folder);
            ApplyDataToForm(data);
        }
        else
        {
            _lblMode.Text = "Mode détecté : INSTALLATION (dossier vide ou inexistant)";
            _lblMode.ForeColor = Color.SeaGreen;
        }

        _lblNet48Status.Text = FormatStatus(".NET Framework 4.8 (worker)", PrerequisiteChecker.CheckDotNetFramework48());
        var sageOmStatus = PrerequisiteChecker.CheckSageOm();
        _lblSageOmStatus.Text = FormatStatus("Sage OM (composants Sage 100)", sageOmStatus);
        _chkConfirmSageOm.Visible = sageOmStatus != PrerequisiteStatus.Present;
    }

    private static string FormatStatus(string label, PrerequisiteStatus status) => status switch
    {
        PrerequisiteStatus.Present => $"{label} : présent",
        PrerequisiteStatus.Absent => $"{label} : ABSENT",
        _ => $"{label} : indétectable automatiquement — confirmation manuelle requise"
    };

    /// <summary>
    /// Pré-remplissage en mode mise à jour (TASK-122 §Périmètre point 3) : une seule saisie
    /// représente désormais les deux clés <c>GrfConnection</c>/<c>PersistenceConnection</c>, donc
    /// seule <see cref="SetupData.Grf"/> (canonique) est affichée. Si une installation antérieure
    /// à ce TASK a laissé les deux clés diverger, l'installateur en est averti explicitement au
    /// lieu d'un écrasement silencieux : poursuivre écrira la valeur GRF affichée dans les deux
    /// clés de <c>connections.json</c>.
    /// </summary>
    private void ApplyDataToForm(SetupData data)
    {
        _connection.SetValues(data.Grf);

        if (ConnectionsDiverge(data.Grf, data.Persistence))
        {
            _lblConnectionDivergence.Text =
                "Attention : ce connections.json existant contient des connexions GRF et Persistance " +
                "différentes (installation antérieure à l'unification). Seule la connexion GRF est " +
                "affichée ci-dessus ; poursuivre écrasera la connexion Persistance avec cette même valeur.";
            _lblConnectionDivergence.Visible = true;
        }
        else
        {
            _lblConnectionDivergence.Visible = false;
        }

        _numPort.Value = data.Port;
        _originalPort = data.Port;
        _cboSageVersion.SelectedItem = data.SageVersion;
        _txtApLicenceServerAddress.Text = data.ApLicenceServerAddress;
        _numApLicenceServerPort.Value = data.ApLicenceServerPort;
    }

    private static bool ConnectionsDiverge(SqlConnectionParts a, SqlConnectionParts b) =>
        !string.Equals(a.Server, b.Server, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(a.Database, b.Database, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(a.UserId, b.UserId, StringComparison.OrdinalIgnoreCase);

    private SetupData ReadDataFromForm() => new()
    {
        InstallFolder = _txtInstallFolder.Text.Trim(),
        Grf = _connection.ReadValues(),
        Persistence = _connection.ReadValues(),
        Port = (int)_numPort.Value,
        SageVersion = (SageVersion)(_cboSageVersion.SelectedItem ?? SageVersion.V10),
        ApLicenceServerAddress = _txtApLicenceServerAddress.Text.Trim(),
        ApLicenceServerPort = (int)_numApLicenceServerPort.Value
    };

    /// <summary>Recalcule le texte du récapitulatif (étape 6) à partir de la saisie courante.</summary>
    private void RefreshRecap()
    {
        var data = ReadDataFromForm();
        var passwordHint = string.IsNullOrEmpty(data.Grf.Password)
            ? (_isUpdateMode ? "(inchangé)" : "(non renseigné)")
            : "••••••••";

        _lblRecap.Text =
            $"Dossier d'installation : {data.InstallFolder}\n" +
            $"Mode détecté : {(_isUpdateMode ? "MISE À JOUR" : "INSTALLATION")}\n\n" +
            "Connexion SQL (GRF + Persistance)\n" +
            $"  Serveur : {data.Grf.Server}\n" +
            $"  Base : {data.Grf.Database}\n" +
            $"  Utilisateur : {data.Grf.UserId}\n" +
            $"  Mot de passe : {passwordHint}\n\n" +
            $"Version Sage 100 : {data.SageVersion.DisplayLabel()}\n" +
            $"Licence ApLicence : {data.ApLicenceServerAddress}:{data.ApLicenceServerPort}\n\n" +
            $"Port d'écoute HTTP : {data.Port}\n" +
            $"Service Windows : {WinSwServiceManager.ServiceName} ({WinSwServiceManager.ServiceId})";
    }

    private Task OnFinishAsync()
    {
        var data = ReadDataFromForm();

        try
        {
            RunInstallOrUpdate(data);
            MessageBox.Show(this, _isUpdateMode ? "Mise à jour terminée." : "Installation terminée.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Échec : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return Task.CompletedTask;
    }

    private void RunInstallOrUpdate(SetupData data)
    {
        var setupSourceFolder = PayloadExtractor.Resolve(out var cleanupPath);
        try
        {
            var winSw = new WinSwServiceManager(data.InstallFolder);

            // Le service doit être installé si et seulement s'il n'est pas déjà enregistré au SCM —
            // pas simplement quand _isUpdateMode (déduit de la présence de connections.json, étape
            // 1/6) est faux. Un connections.json peut exister sans que le service ait jamais été
            // installé (ex. tentative précédente interrompue avant Install(), bug constaté en essai
            // réel le 19/07/2026 : Start() échouait avec le code SCM 1060, service non installé).
            var serviceAlreadyRegistered = winSw.IsServiceRegistered();

            if (serviceAlreadyRegistered)
            {
                winSw.Stop();
            }

            // Juste après Stop(), le SCM peut mettre quelques instants à libérer les fichiers du
            // service arrêté (handle process encore en cours de fermeture) : sans ce retry, une
            // copie ou une régénération de WinSW.exe lancée immédiatement échoue par IOException
            // (fichier verrouillé) alors que le scénario de mise à jour le plus courant devrait
            // aboutir sans intervention manuelle (bug constaté en essai réel le 19/07/2026).
            RetryOnFileLock(() => DeploymentCopier.CopyBinaries(setupSourceFolder, data.InstallFolder));

            var root = ConnectionsFileService.LoadOrEmpty(data.InstallFolder);
            ConnectionsFileService.ApplyChanges(root, data, _isUpdateMode);
            ConnectionsFileService.Save(root, data.InstallFolder);

            // WinSW.exe lui-même est recopié ici (à l'identique) : même risque de verrou que les
            // binaires applicatifs si le process WinSW précédent n'a pas encore complètement libéré
            // son fichier exécutable.
            RetryOnFileLock(() => winSw.PrepareServiceFiles(setupSourceFolder));

            if (!serviceAlreadyRegistered)
            {
                winSw.Install();
            }

            winSw.Start();
        }
        finally
        {
            if (cleanupPath is not null)
            {
                Directory.Delete(cleanupPath, recursive: true);
            }
        }
    }

    /// <summary>
    /// Ré-essaie une opération de fichiers pendant 15s tant qu'elle échoue avec une exception de
    /// verrouillage Windows (verrou de fichier remonté en IOException, ou parfois en
    /// UnauthorizedAccessException — cas constaté en essai réel : "Access to the path '...' is
    /// denied" sur un fichier tout juste libéré par l'arrêt du service, sans lien avec les droits
    /// puisque le processus tourne déjà en administrateur). Si le verrou persiste au-delà du délai,
    /// l'exception remonte telle quelle — pas de comportement silencieux, OnFinishAsync affiche déjà
    /// un message clair à l'utilisateur.
    /// </summary>
    private static void RetryOnFileLock(Action action)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
            }
        }
    }

    /// <summary>Regroupe les 4 champs (Server/Database/User/Password) d'une connexion SQL en un contrôle réutilisable.</summary>
    private sealed class ConnectionGroup
    {
        private readonly TextBox _server = new() { Width = 220 };
        private readonly TextBox _database = new() { Width = 220 };
        private readonly TextBox _user = new() { Width = 220 };
        private readonly TextBox _password = new() { Width = 220, UseSystemPasswordChar = true, PlaceholderText = "(inchangé si laissé vide)" };

        public GroupBox GroupBox { get; }

        public ConnectionGroup(string title)
        {
            GroupBox = new GroupBox { Text = title, AutoSize = true, Width = 580 };
            var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 4 };
            AddRow(table, 0, "Serveur", _server);
            AddRow(table, 1, "Base", _database);
            AddRow(table, 2, "Utilisateur", _user);
            AddRow(table, 3, "Mot de passe", _password);
            GroupBox.Controls.Add(table);
        }

        private static void AddRow(TableLayoutPanel table, int row, string label, Control field)
        {
            table.Controls.Add(new Label { Text = label + " :", AutoSize = true, Margin = new Padding(3, 8, 6, 3) }, 0, row);
            table.Controls.Add(field, 1, row);
        }

        public void SetValues(SqlConnectionParts parts)
        {
            _server.Text = parts.Server;
            _database.Text = parts.Database;
            _user.Text = parts.UserId;
            _password.Text = "";
        }

        public SqlConnectionParts ReadValues() => new()
        {
            Server = _server.Text.Trim(),
            Database = _database.Text.Trim(),
            UserId = _user.Text.Trim(),
            Password = _password.Text,
            TrustServerCertificate = true
        };
    }
}
