namespace CertTool.Models;
public class AppConfig
{
    public string PorkbunDomain { get; set; } = "horntvedt.com";
    public string PorkbunApiKey { get; set; } = "";
    public string PorkbunSecretApiKey { get; set; } = "";
    public List<string> RsaKeys { get; } = ["RS256", "RS384", "RS512"];
    
}