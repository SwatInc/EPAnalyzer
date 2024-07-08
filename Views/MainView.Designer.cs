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
            ribbonPageProtocols = new DevExpress.XtraBars.Ribbon.RibbonPage();
            ribbonPageGroupPrecision = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
            ribbonPageGroupAccuracyAndLinearity = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
            ((System.ComponentModel.ISupportInitialize)ribbonControl).BeginInit();
            SuspendLayout();
            // 
            // ribbonControl
            // 
            ribbonControl.ExpandCollapseItem.Id = 0;
            ribbonControl.Items.AddRange(new DevExpress.XtraBars.BarItem[] { ribbonControl.ExpandCollapseItem, barButtonItemSimplePrecision, barButtonItemComplexPrecision });
            ribbonControl.Location = new System.Drawing.Point(0, 0);
            ribbonControl.MaxItemId = 3;
            ribbonControl.Name = "ribbonControl";
            ribbonControl.Pages.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPage[] { ribbonPageProtocols });
            ribbonControl.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonControlStyle.Office365;
            ribbonControl.ShowApplicationButton = DevExpress.Utils.DefaultBoolean.False;
            ribbonControl.Size = new System.Drawing.Size(1063, 170);
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
            // ribbonPageProtocols
            // 
            ribbonPageProtocols.Groups.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPageGroup[] { ribbonPageGroupPrecision, ribbonPageGroupAccuracyAndLinearity });
            ribbonPageProtocols.Name = "ribbonPageProtocols";
            ribbonPageProtocols.Text = "PROTOCOLS";
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
            // MainView
            // 
            AllowFormGlass = DevExpress.Utils.DefaultBoolean.True;
            Appearance.Options.UseFont = true;
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1063, 506);
            Controls.Add(ribbonControl);
            Font = new System.Drawing.Font("Segoe UI", 8.25F);
            IconOptions.SvgImage = (DevExpress.Utils.Svg.SvgImage)resources.GetObject("MainView.IconOptions.SvgImage");
            Name = "MainView";
            Ribbon = ribbonControl;
            Text = "Evaluation Protocol Analyzer";
            ((System.ComponentModel.ISupportInitialize)ribbonControl).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DevExpress.XtraBars.Ribbon.RibbonControl ribbonControl;
        private DevExpress.XtraBars.Ribbon.RibbonPage ribbonPageProtocols;
        private DevExpress.XtraBars.Ribbon.RibbonPageGroup ribbonPageGroupPrecision;
        private DevExpress.XtraBars.BarButtonItem barButtonItemSimplePrecision;
        private DevExpress.XtraBars.BarButtonItem barButtonItemComplexPrecision;
        private DevExpress.XtraBars.Ribbon.RibbonPageGroup ribbonPageGroupAccuracyAndLinearity;
    }
}

