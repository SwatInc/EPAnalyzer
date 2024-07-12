namespace EPAnalyzer
{
    partial class MainView
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainView));
            ribbonControl = new DevExpress.XtraBars.Ribbon.RibbonControl();
            barButtonItemSimplePrecision = new DevExpress.XtraBars.BarButtonItem();
            barButtonItemComplexPrecision = new DevExpress.XtraBars.BarButtonItem();
            barStaticItemCurrentProject = new DevExpress.XtraBars.BarStaticItem();
            ribbonPageHome = new DevExpress.XtraBars.Ribbon.RibbonPage();
            ribbonPageGroupPrecision = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
            ribbonPageGroupAccuracyAndLinearity = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
            ribbonStatusBar1 = new DevExpress.XtraBars.Ribbon.RibbonStatusBar();
            panelControlMain = new DevExpress.XtraEditors.PanelControl();
            ribbonPageGroupProject = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
            barButtonItemNewProject = new DevExpress.XtraBars.BarButtonItem();
            barButtonItemOpenProject = new DevExpress.XtraBars.BarButtonItem();
            ((System.ComponentModel.ISupportInitialize)ribbonControl).BeginInit();
            ((System.ComponentModel.ISupportInitialize)panelControlMain).BeginInit();
            SuspendLayout();
            // 
            // ribbonControl
            // 
            ribbonControl.ExpandCollapseItem.Id = 0;
            ribbonControl.Items.AddRange(new DevExpress.XtraBars.BarItem[] { ribbonControl.ExpandCollapseItem, barButtonItemSimplePrecision, barButtonItemComplexPrecision, barStaticItemCurrentProject, barButtonItemNewProject, barButtonItemOpenProject });
            ribbonControl.Location = new System.Drawing.Point(0, 0);
            ribbonControl.MaxItemId = 6;
            ribbonControl.Name = "ribbonControl";
            ribbonControl.Pages.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPage[] { ribbonPageHome });
            ribbonControl.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonControlStyle.Office365;
            ribbonControl.ShowApplicationButton = DevExpress.Utils.DefaultBoolean.False;
            ribbonControl.Size = new System.Drawing.Size(1063, 170);
            ribbonControl.StatusBar = ribbonStatusBar1;
            // 
            // barButtonItemSimplePrecision
            // 
            barButtonItemSimplePrecision.Caption = "Simple Precision";
            barButtonItemSimplePrecision.Id = 1;
            barButtonItemSimplePrecision.ImageOptions.SvgImage = (DevExpress.Utils.Svg.SvgImage)resources.GetObject("barButtonItemSimplePrecision.ImageOptions.SvgImage");
            barButtonItemSimplePrecision.Name = "barButtonItemSimplePrecision";
            // 
            // barButtonItemComplexPrecision
            // 
            barButtonItemComplexPrecision.Caption = "Complex Precision";
            barButtonItemComplexPrecision.Id = 2;
            barButtonItemComplexPrecision.ImageOptions.SvgImage = (DevExpress.Utils.Svg.SvgImage)resources.GetObject("barButtonItemComplexPrecision.ImageOptions.SvgImage");
            barButtonItemComplexPrecision.Name = "barButtonItemComplexPrecision";
            // 
            // barStaticItemCurrentProject
            // 
            barStaticItemCurrentProject.Caption = "CURRENT_PROJECT";
            barStaticItemCurrentProject.Id = 3;
            barStaticItemCurrentProject.Name = "barStaticItemCurrentProject";
            // 
            // ribbonPageHome
            // 
            ribbonPageHome.Groups.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPageGroup[] { ribbonPageGroupProject, ribbonPageGroupPrecision, ribbonPageGroupAccuracyAndLinearity });
            ribbonPageHome.Name = "ribbonPageHome";
            ribbonPageHome.Text = "HOME";
            // 
            // ribbonPageGroupPrecision
            // 
            ribbonPageGroupPrecision.ItemLinks.Add(barButtonItemSimplePrecision);
            ribbonPageGroupPrecision.ItemLinks.Add(barButtonItemComplexPrecision);
            ribbonPageGroupPrecision.Name = "ribbonPageGroupPrecision";
            ribbonPageGroupPrecision.Text = "PRECISION";
            // 
            // ribbonPageGroupAccuracyAndLinearity
            // 
            ribbonPageGroupAccuracyAndLinearity.Name = "ribbonPageGroupAccuracyAndLinearity";
            ribbonPageGroupAccuracyAndLinearity.Text = "ACCURACY AND LINEARITY";
            // 
            // ribbonStatusBar1
            // 
            ribbonStatusBar1.ItemLinks.Add(barStaticItemCurrentProject);
            ribbonStatusBar1.Location = new System.Drawing.Point(0, 475);
            ribbonStatusBar1.Name = "ribbonStatusBar1";
            ribbonStatusBar1.Ribbon = ribbonControl;
            ribbonStatusBar1.Size = new System.Drawing.Size(1063, 31);
            // 
            // panelControlMain
            // 
            panelControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            panelControlMain.Location = new System.Drawing.Point(0, 170);
            panelControlMain.Name = "panelControlMain";
            panelControlMain.Size = new System.Drawing.Size(1063, 305);
            panelControlMain.TabIndex = 2;
            // 
            // ribbonPageGroupProject
            // 
            ribbonPageGroupProject.ItemLinks.Add(barButtonItemNewProject);
            ribbonPageGroupProject.ItemLinks.Add(barButtonItemOpenProject);
            ribbonPageGroupProject.Name = "ribbonPageGroupProject";
            ribbonPageGroupProject.Text = "PROJECT";
            // 
            // barButtonItemNewProject
            // 
            barButtonItemNewProject.Caption = "New Project";
            barButtonItemNewProject.Id = 4;
            barButtonItemNewProject.ImageOptions.SvgImage = (DevExpress.Utils.Svg.SvgImage)resources.GetObject("barButtonItemNewProject.ImageOptions.SvgImage");
            barButtonItemNewProject.Name = "barButtonItemNewProject";
            // 
            // barButtonItemOpenProject
            // 
            barButtonItemOpenProject.Caption = "Open Project";
            barButtonItemOpenProject.Id = 5;
            barButtonItemOpenProject.ImageOptions.SvgImage = (DevExpress.Utils.Svg.SvgImage)resources.GetObject("barButtonItem1.ImageOptions.SvgImage");
            barButtonItemOpenProject.Name = "barButtonItemOpenProject";
            // 
            // MainView
            // 
            AllowFormGlass = DevExpress.Utils.DefaultBoolean.True;
            Appearance.Options.UseFont = true;
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1063, 506);
            Controls.Add(panelControlMain);
            Controls.Add(ribbonStatusBar1);
            Controls.Add(ribbonControl);
            Font = new System.Drawing.Font("Segoe UI", 8.25F);
            IconOptions.SvgImage = (DevExpress.Utils.Svg.SvgImage)resources.GetObject("MainView.IconOptions.SvgImage");
            Name = "MainView";
            Ribbon = ribbonControl;
            StatusBar = ribbonStatusBar1;
            Text = "Evaluation Protocol Analyzer";
            ((System.ComponentModel.ISupportInitialize)ribbonControl).EndInit();
            ((System.ComponentModel.ISupportInitialize)panelControlMain).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DevExpress.XtraBars.Ribbon.RibbonControl ribbonControl;
        private DevExpress.XtraBars.Ribbon.RibbonPage ribbonPageHome;
        private DevExpress.XtraBars.Ribbon.RibbonPageGroup ribbonPageGroupPrecision;
        private DevExpress.XtraBars.BarButtonItem barButtonItemSimplePrecision;
        private DevExpress.XtraBars.BarButtonItem barButtonItemComplexPrecision;
        private DevExpress.XtraBars.Ribbon.RibbonPageGroup ribbonPageGroupAccuracyAndLinearity;
        private DevExpress.XtraBars.BarStaticItem barStaticItemCurrentProject;
        private DevExpress.XtraBars.Ribbon.RibbonStatusBar ribbonStatusBar1;
        private DevExpress.XtraEditors.PanelControl panelControlMain;
        private DevExpress.XtraBars.Ribbon.RibbonPageGroup ribbonPageGroupProject;
        private DevExpress.XtraBars.BarButtonItem barButtonItemNewProject;
        private DevExpress.XtraBars.BarButtonItem barButtonItemOpenProject;
    }
}

