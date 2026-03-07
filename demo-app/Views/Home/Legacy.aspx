<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Legacy Web Forms Page</h2>
    
    <p>This page simulates legacy ASP.NET Web Forms patterns for testing legacy mode.</p>
    
    <!-- Inline Script in Web Forms -->
    <script type="text/javascript">
    function legacyFunction() {
        // Legacy function - update UI
        var btn = document.getElementById('<%= btnLegacy.ClientID %>');
        if (btn) {
            btn.style.backgroundColor = '#28a745';
            setTimeout(function() {
                btn.style.backgroundColor = '';
            }, 1000);
        }
    }
        
        // Simulate WebResource.axd reference
        function loadWebResource() {
            var script = document.createElement('script');
            script.src = '/WebResource.axd?d=abc123&t=1234567890';
            document.head.appendChild(script);
        }
    </script>
    
    <!-- Inline Style -->
    <style type="text/css">
        .legacy-container {
            width: 800px;
            margin: 0 auto;
            padding: 20px;
        }
        
        .legacy-button {
            background-color: #007bff;
            color: white;
            padding: 10px 20px;
            border: none;
            cursor: pointer;
        }
    </style>
    
    <div class="legacy-container">
        <asp:Button ID="btnLegacy" runat="server" Text="Legacy Button" 
                    OnClientClick="legacyFunction(); return false;" 
                    CssClass="legacy-button" />
        
        <asp:Label ID="lblMessage" runat="server" Text="Legacy Label"></asp:Label>
    </div>
    
    <!-- Event Handler in Web Forms -->
    <script type="text/javascript">
        window.onload = function() {
            loadWebResource();
        };
    </script>
</asp:Content>

