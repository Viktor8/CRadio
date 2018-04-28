<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="CRadio.Default" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>My Radio</title>
    <link rel="shortcut icon" type="image/x-icon" href="wave2-32.ico" />
    <script type="text/javascript">

        function onLoad() {
            var sec = parseInt(document.location.search.substr(1));

            if (!isNaN(sec))
                this.currentTime = sec;
            //mainPlayer.currentTime = sec;
        };

        var xhr;

        window.onload = function () {

            xhr = new XMLHttpRequest();

            var texbox_input = document.getElementById("TextBoxInput");
            texbox_input.onkeypress = PressHandler;
        }


        function PressHandler(event) {
            if (event.keyCode == 13) {
                var tb = document.getElementById("TextBoxInput");
                var str = "InputHandler.ashx?cmd=" + tb.value;
                xhr.open("POST", str);
                xhr.send();
                tb.value = "";
            }
        }

    </script>
    <style type="text/css">
        .auto-style1 {
            resize: none;
            position: center;
            width: 90%;
            height: 300px;
        }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div>
            <h3>Streams testing</h3>
            <hr />
            <label>Progress loading of static file</label><br />
         <%--   <audio id="testPlayer" style="width: 100%"
                controls="controls" onloadeddata="onLoad()">
                <source src="api/media/play?f=Aiva - 1 hour music collection.mp3" />
            </audio>
            <br />

            <label>Mp3 static concatenation</label><br />
            <audio id="ConcatPlayer" style="width: 100%"
                controls="controls" onloadeddata="onLoad()">
                <source src="api/media/play?f=sum.mp3" />
            </audio>--%>
            <br />

            <label>Live streaming through progress loading</label>
            <audio id="MainPlayer" style="width: 100%"
                controls="controls" onloadeddata="onLoad()">

                <% Response.Write(" <source src=\"api/media/play?f=LiveStream" +
                      CRadio.Default._antiHashSufix++ + "mp3\"/>"); %>
            </audio>
            <hr />
            <div style="align-items: center; text-align: center">
                <asp:ScriptManager runat="server" />
                <asp:UpdatePanel runat="server">
                    <ContentTemplate>
                        <asp:Timer ID="Timer1" runat="server" OnTick="Timer_Tick" Interval="1000"></asp:Timer>
                        <asp:TextBox ID="OutputWindow" class="auto-style1" TextMode="MultiLine" Rows="25" name="S1" runat="server"></asp:TextBox>
                    </ContentTemplate>
                </asp:UpdatePanel>

                <asp:TextBox ID="TextBoxInput" runat="server" Width="90%"></asp:TextBox>
            </div>


        </div>
    </form>
</body>
</html>
