﻿using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GitCommands;
using GitExtUtils.GitUI;
using GitUI.Editor;
using GitUI.Properties;
using ResourceManager;

namespace GitUI.CommandsDialogs.BrowseDialog.DashboardControl
{
    public partial class Dashboard : GitModuleControl
    {
        private readonly TranslationString _cloneFork = new TranslationString("Clone {0} repository");
        private readonly TranslationString _cloneRepository = new TranslationString("Clone repository");
        private readonly TranslationString _createRepository = new TranslationString("Create new repository");
        private readonly TranslationString _develop = new TranslationString("Develop");
        private readonly TranslationString _donate = new TranslationString("Donate");
        private readonly TranslationString _issues = new TranslationString("Issues");
        private readonly TranslationString _openRepository = new TranslationString("Open repository");
        private readonly TranslationString _translate = new TranslationString("Translate");
        private readonly TranslationString _showCurrentBranch = new TranslationString("Show current branch");

        private DashboardTheme _selectedTheme;

        public event EventHandler<GitModuleEventArgs> GitModuleChanged;

        public Dashboard()
        {
            InitializeComponent();
            Translate();

            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            Visible = false;

            tableLayoutPanel1.AutoSize = true;
            tableLayoutPanel1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel1.Dock = DockStyle.Fill;
            pnlLeft.Dock = DockStyle.Fill;
            flpnlStart.Dock = DockStyle.Fill;
            flpnlContribute.Dock = DockStyle.Bottom;
            flpnlContribute.SendToBack();

            recentRepositoriesList1.GitModuleChanged += OnModuleChanged;

            // apply scaling
            pnlLogo.Padding = DpiUtil.Scale(pnlLogo.Padding);
            flpnlStart.Padding = DpiUtil.Scale(flpnlStart.Padding);
            flpnlContribute.Padding = DpiUtil.Scale(flpnlContribute.Padding);
            tableLayoutPanel1.ColumnStyles[1].Width = DpiUtil.Scale(tableLayoutPanel1.ColumnStyles[1].Width);
            recentRepositoriesList1.HeaderHeight = pnlLogo.Height;
        }

        // need this to stop flickering of the background images, nothing else works
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        public void RefreshContent()
        {
            InitDashboardLayout();
            ApplyTheme();
            recentRepositoriesList1.ShowRecentRepositories();

            void ApplyTheme()
            {
                _selectedTheme = SystemColors.ControlText.IsLightColor() ? DashboardTheme.Dark : DashboardTheme.Light;

                BackColor = _selectedTheme.Primary;
                pnlLogo.BackColor = _selectedTheme.PrimaryVeryDark;
                flpnlStart.BackColor = _selectedTheme.PrimaryLight;
                flpnlContribute.BackColor = _selectedTheme.PrimaryVeryLight;
                lblContribute.ForeColor = _selectedTheme.SecondaryHeadingText;
                recentRepositoriesList1.BranchNameColor = AppSettings.BranchColor; // _selectedTheme.SecondaryText;
                recentRepositoriesList1.FavouriteColor = _selectedTheme.AccentedText;
                recentRepositoriesList1.ForeColor = _selectedTheme.PrimaryText;
                recentRepositoriesList1.HeaderColor = _selectedTheme.SecondaryHeadingText;
                recentRepositoriesList1.HeaderBackColor = _selectedTheme.PrimaryDark;
                recentRepositoriesList1.HoverColor = _selectedTheme.PrimaryLight;
                recentRepositoriesList1.MainBackColor = _selectedTheme.Primary;
                BackgroundImage = _selectedTheme.BackgroundImage;

                foreach (var item in flpnlContribute.Controls.OfType<LinkLabel>().Union(flpnlStart.Controls.OfType<LinkLabel>()))
                {
                    item.LinkColor = _selectedTheme.PrimaryText;
                }
            }

            void InitDashboardLayout()
            {
                try
                {
                    pnlLeft.SuspendLayout();

                    AddLinks(flpnlContribute,
                        panel =>
                        {
                            panel.Controls.Add(lblContribute);
                            lblContribute.Font = new Font(AppSettings.Font.FontFamily, AppSettings.Font.SizeInPoints + 5.5f);

                            CreateLink(panel, _develop.Text, Resources.develop.ToBitmap(), GitHubItem_Click);
                            CreateLink(panel, _donate.Text, Resources.dollar.ToBitmap(), DonateItem_Click);
                            CreateLink(panel, _translate.Text, Resources.EditItem, TranslateItem_Click);
                            var lastControl = CreateLink(panel, _issues.Text, Resources.bug, IssuesItem_Click);
                            return lastControl;
                        },
                        (panel, lastControl) =>
                        {
                            var height = DpiUtil.Scale(lastControl.Location.Y + lastControl.Size.Height) + panel.Padding.Bottom;
                            panel.Height = height;
                            panel.MinimumSize = new Size(0, height);
                        });

                    AddLinks(flpnlStart,
                        panel =>
                        {
                            CreateLink(panel, _createRepository.Text, Resources.IconRepoCreate, createItem_Click);
                            CreateLink(panel, _openRepository.Text, Resources.IconRepoOpen, openItem_Click);
                            var lastControl = CreateLink(panel, _cloneRepository.Text, Resources.IconCloneRepoGit, cloneItem_Click);

                            foreach (var gitHoster in PluginRegistry.GitHosters)
                            {
                                lastControl = CreateLink(panel, string.Format(_cloneFork.Text, gitHoster.Description), Resources.IconCloneRepoGithub,
                                    (repoSender, eventArgs) => UICommands.StartCloneForkFromHoster(this, gitHoster, GitModuleChanged));
                            }

                            return lastControl;
                        },
                        (panel, lastControl) =>
                        {
                            var height = DpiUtil.Scale(lastControl.Location.Y + lastControl.Size.Height) + panel.Padding.Bottom;
                            panel.MinimumSize = new Size(0, height);
                        });
                }
                finally
                {
                    pnlLeft.ResumeLayout(false);
                    pnlLeft.PerformLayout();
                    AutoScrollMinSize = new Size(0, pnlLogo.Height + flpnlStart.MinimumSize.Height + flpnlContribute.MinimumSize.Height);
                }

                void AddLinks(Panel panel, Func<Panel, Control> addLinks, Action<Panel, Control> onLayout)
                {
                    panel.SuspendLayout();
                    panel.Controls.Clear();

                    var lastControl = addLinks(panel);

                    panel.ResumeLayout(false);
                    panel.PerformLayout();

                    onLayout(panel, lastControl);
                }

                Control CreateLink(Control container, string text, Image icon, EventHandler handler)
                {
                    var padding24 = DpiUtil.Scale(24);
                    var padding3 = DpiUtil.Scale(3);
                    var linkLabel = new LinkLabel
                    {
                        AutoSize = true,
                        AutoEllipsis = true,
                        Font = AppSettings.Font,
                        Image = icon,
                        ImageAlign = ContentAlignment.MiddleLeft,
                        LinkBehavior = LinkBehavior.NeverUnderline,
                        Margin = new Padding(padding3, 0, padding3, DpiUtil.Scale(8)),
                        Padding = new Padding(padding24, padding3, padding3, padding3),
                        TabStop = true,
                        Text = text,
                        TextAlign = ContentAlignment.MiddleLeft
                    };
                    linkLabel.MouseHover += (s, e) => linkLabel.LinkColor = _selectedTheme.AccentedText;
                    linkLabel.MouseLeave += (s, e) => linkLabel.LinkColor = _selectedTheme.PrimaryText;

                    if (handler != null)
                    {
                        linkLabel.Click += handler;
                    }

                    container.Controls.Add(linkLabel);

                    return linkLabel;
                }
            }
        }

        protected virtual void OnModuleChanged(object sender, GitModuleEventArgs e)
        {
            var handler = GitModuleChanged;
            handler?.Invoke(this, e);
        }

        private static T FindControl<T>(IEnumerable controls, Func<T, bool> predicate) where T : Control
        {
            foreach (Control control in controls)
            {
                if (control is T result && predicate(result))
                {
                    return result;
                }

                result = FindControl(control.Controls, predicate);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void dashboard_ParentChanged(object sender, EventArgs e)
        {
            if (Parent == null)
            {
                Visible = false;
                return;
            }

            //
            // create Show current branch menu item and add to Dashboard menu
            //
            var showCurrentBranchMenuItem = new ToolStripMenuItem(_showCurrentBranch.Text);
            showCurrentBranchMenuItem.Click += showCurrentBranchMenuItem_Click;
            showCurrentBranchMenuItem.Checked = AppSettings.DashboardShowCurrentBranch;

            var form = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Name == nameof(FormBrowse));
            if (form != null)
            {
                var menuStrip = FindControl<MenuStrip>(form.Controls, p => p.Name == "menuStrip1");
                var dashboardMenu = (ToolStripMenuItem)menuStrip.Items.Cast<ToolStripItem>().SingleOrDefault(p => p.Name == "dashboardToolStripMenuItem");
                dashboardMenu?.DropDownItems.Add(showCurrentBranchMenuItem);
            }

            Visible = true;
        }

        private void showCurrentBranchMenuItem_Click(object sender, EventArgs e)
        {
            bool newValue = !AppSettings.DashboardShowCurrentBranch;
            AppSettings.DashboardShowCurrentBranch = newValue;
            ((ToolStripMenuItem)sender).Checked = newValue;
            RefreshContent();
        }

        private static void TranslateItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.transifex.com/git-extensions/git-extensions/translate/");
        }

        private static void GitHubItem_Click(object sender, EventArgs e)
        {
            Process.Start(@"http://github.com/gitextensions/gitextensions");
        }

        private static void IssuesItem_Click(object sender, EventArgs e)
        {
            Process.Start(@"http://github.com/gitextensions/gitextensions/issues");
        }

        private void openItem_Click(object sender, EventArgs e)
        {
            GitModule module = FormOpenDirectory.OpenModule(this, currentModule: null);
            if (module != null)
            {
                OnModuleChanged(this, new GitModuleEventArgs(module));
            }
        }

        private void cloneItem_Click(object sender, EventArgs e)
        {
            UICommands.StartCloneDialog(this, null, false, OnModuleChanged);
        }

        private void createItem_Click(object sender, EventArgs e)
        {
            UICommands.StartInitializeDialog(this, Module.WorkingDir, OnModuleChanged);
        }

        private static void DonateItem_Click(object sender, EventArgs e)
        {
            Process.Start(FormDonate.DonationUrl);
        }
    }
}
