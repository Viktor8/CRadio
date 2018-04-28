using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace CRadio
{
    public partial class Default : System.Web.UI.Page
    {

         public static int _antiHashSufix = 0;

        protected void Page_Load(object sender, EventArgs e)
        { 
            
        }
        
        protected void TextBox2_TextChanged(object sender, EventArgs e)
        {
            PlayerConnection.PushCommand(TextBoxInput.Text);
            PlayerConnection._sb.AppendLine(TextBoxInput.Text);
            TextBoxInput.Text = string.Empty;

        }

        protected void Timer_Tick(object sender, EventArgs e)
        {
            OutputWindow.Text = PlayerConnection._sb.ToString();
        }
    }
}