using System.Windows.Forms;

namespace SecureDesktop.Forms
{
    public class ConfigurationForm : Form
    {
        public ConfigurationForm()
        {
            this.Text = "Konfiguracja";
            this.Size = new System.Drawing.Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;
        }
    }
}