using System.Windows.Input;
using CertTool.Backend.OAuthTest;
using CertTool.Commands;
using CertTool.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CertTool.ViewModels;

public sealed class TokenViewModel : ViewModelBase
{
    private readonly TokenService _tokenService;

    // Defaults previously used by TokenService. KID is deliberately empty.
    private string _tokenEndpoint = "https://auth.horntvedt.com/realms/master/protocol/openid-connect/token";
    private string _clientId = "my-api-client";
    private string _certificateThumbprint = string.Empty;
    private string _tokenJson = string.Empty;

    public string TokenEndpoint
    {
        get => _tokenEndpoint;
        set { if (_tokenEndpoint == value) return; _tokenEndpoint = value; OnPropertyChanged(); }
    }

    public string ClientId
    {
        get => _clientId;
        set { if (_clientId == value) return; _clientId = value; OnPropertyChanged(); }
    }

    public string CertificateThumbprint
    {
        get => _certificateThumbprint;
        set { if (_certificateThumbprint == value) return; _certificateThumbprint = value; OnPropertyChanged(); }
    }

    // public string Kid
    // {
    //     get => _kid;
    //     set { if (_kid == value) return; _kid = value; OnPropertyChanged(); }
    // }


    public string TokenJson
    {
        get => _tokenJson;
        private set
        {
            if (_tokenJson == value) return;
            _tokenJson = value;
            OnPropertyChanged();
        }
    }

    public ICommand TokenCommand { get; }

    public TokenViewModel(TokenService tokenService)
    {
        _tokenService = tokenService;
        TokenCommand = new TaskDelegateCommand(ExecuteTokenCommand, () => true);
    }

    private async Task ExecuteTokenCommand()
    {
        TokenJson = string.Empty;
        var response = await _tokenService.GetToken(
            TokenEndpoint.Trim(), ClientId.Trim(), CertificateThumbprint.Trim(), Kid.Trim());
        if (response is null) return;

        TokenJson = JToken.Parse(response).ToString(Formatting.Indented);
        await PlatformDialogs.CopyAsync(TokenJson);
    }
}
