namespace ShareDealAttend;

partial class MainForm
{
    /// <summary>Required designer variable.</summary>
    private System.ComponentModel.IContainer components = null!;

    /// <summary>Clean up any resources being used.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
            _webView?.Dispose();
            _trayIcon?.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        this.ClientSize = new System.Drawing.Size(900, 600);
        this.MinimumSize = new System.Drawing.Size(420, 320);
        this.Name = "MainForm";
        this.Text = "ShareDeal Attend";
        this.BackColor = System.Drawing.Color.White;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.KeyPreview = true;
        this.DoubleBuffered = true;
    }

    #endregion

    private Microsoft.Web.WebView2.WinForms.WebView2 _webView = null!;
}
