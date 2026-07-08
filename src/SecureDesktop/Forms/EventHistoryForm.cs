using System.Windows.Forms;

namespace SecureDesktop.Forms
{
    public class EventHistoryForm : Form
    {
        public EventHistoryForm()
        {
            this.Text = "Historia zdarzeń";
            this.Size = new System.Drawing.Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
        }
    }
}