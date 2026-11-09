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
    private readonly TokenValidationService _validationService;
    private string _accessToken = string.Empty;
    private string _expectedAudience = "my-apii";
    private string _validationResult = string.Empty;

    // Defaults previously used by TokenService. KID is deliberately empty.
    private string _tokenEndpoint = "https://auth.horntvedt.com/realms/master/protocol/openid-connect/token";
    private string _clientId = "my-api-client";
    private string _certificateThumbprint = "4A6820E3E9CA31C4696E9672C60D0D7E0B7F3CB4";
    private string _kid = string.Empty;
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

    public string Kid
    {
        get => _kid;
        set { if (_kid == value) return; _kid = value; OnPropertyChanged(); }
    }


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

    public string ExpectedAudience
    {
        get => _expectedAudience;
        set { if (_expectedAudience == value) return; _expectedAudience = value; OnPropertyChanged(); }
    }

    public string ValidationResult
    {
        get => _validationResult;
        private set { if (_validationResult == value) return; _validationResult = value; OnPropertyChanged(); }
    }

    public ICommand TokenCommand { get; }
    public ICommand ValidateTokenCommand { get; }

    public TokenViewModel(TokenService tokenService, TokenValidationService validationService)
    {
        _tokenService = tokenService;
        _validationService = validationService;
        TokenCommand = new TaskDelegateCommand(ExecuteTokenCommand, () => true);
        ValidateTokenCommand = new TaskDelegateCommand(ExecuteValidateTokenCommand, () => true);
    }

    private async Task ExecuteTokenCommand()
    {
        TokenJson = string.Empty;
        _accessToken = string.Empty;
        ValidationResult = string.Empty;
        var response = await _tokenService.GetToken(
            TokenEndpoint.Trim(), ClientId.Trim(), CertificateThumbprint.Trim(), Kid.Trim());
        if (response is null) return;

        var json = JObject.Parse(response);
        _accessToken = json.Value<string>("access_token") ?? string.Empty;
        TokenJson = json.ToString(Formatting.Indented);
        await PlatformDialogs.CopyAsync(TokenJson);
    }

    private async Task ExecuteValidateTokenCommand()
    {
        ValidationResult = "Validating token against Keycloak signing keys…";
        ValidationResult = await _validationService.ValidateAsync(
            TokenEndpoint.Trim(), ExpectedAudience.Trim(), _accessToken);
    }
}
