<%@ Page Language="C#" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Redirect("~/Account/Login", true);
    }
</script>
<!DOCTYPE html>
<html>
<head runat="server">
    <title>Redirecting</title>
</head>
<body>
</body>
</html>
