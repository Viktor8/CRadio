using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CRadio
{
    /// <summary>
    /// Summary description for InputHandler
    /// </summary>
    public class InputHandler : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {
            PlayerConnection.Initialize();
            string cmd = context.Request.QueryString["cmd"];
            PlayerConnection.PushCommand(cmd);
        }

        public bool IsReusable
        {
            get
            {
                return true;
            }
        }
    }
}