

using System.Windows.Input;

using CertTool.Models;
using CertTool.Models.Jwk;
using CertTool.Services;

namespace CertTool.ViewModels
{
    public class PemConverterViewModel : ViewModelBase
    {
        private string _selectedFilePath = "";
        private string _publicKey = "";
        private string _fileLabel = "";
        private string _kid = "";
        private string _jwk = "";
        private string _rsaKey = "";

        private readonly JwkCreator _jwkCreator;
        private readonly PubKeyHandler _pubKeyHandler;

        private readonly INavigationService _navigationService;

        public ICommand SelectFileCommand { get; private set; }
        public ICommand SelectCertificateCommand { get; private set; }
        public ICommand CreateJwkFileCommand { get; private set; }
        public ICommand NavigateTestPage { get; private set; }

        public PemConverterViewModel(
            JwkCreator jwkCreator,
            PubKeyHandler pubKeyHandler,
            NavigationService<CreateCertificateViewModel> navigationService,
            AppConfig config)
        {
            SelectFileCommand = new DelegateCommand(async () => await ExecuteSelectFileCommand());
            CreateJwkFileCommand = new DelegateCommand(async () => await ExecuteCreateJwkCommand());
            SelectCertificateCommand = new DelegateCommand(ExecuteSelectCertificateCommand);
            
            _jwkCreator = jwkCreator;
            _pubKeyHandler = pubKeyHandler;
            _navigationService = navigationService;


            AlgorithmDropDown = config.RsaKeys;
            NavigateTestPage = new DelegateCommand(ExecuteTestCommand); ;
        }

  

        public string SelectedFilePath
        {
            get { return _selectedFilePath; }
            set { _selectedFilePath = value; OnPropertyChanged("SelectedFilePath"); }
        }

        public string FileLabel
        {
            get { return _fileLabel; }
            set { _fileLabel = value; OnPropertyChanged("FileLabel"); }
        }


        public string Algorithm
        {
            get { return _rsaKey; }
            set { _rsaKey = value; OnPropertyChanged("Algorithm"); }
        }

        public string PublicKey
        {
            get { return _publicKey; }
            set { _publicKey = value; OnPropertyChanged("PublicKey"); }
        }


        public string Kid
        {
            get { return _kid; }
            set { _kid = value; OnPropertyChanged("Kid"); }
        }

        public string Jwk
        {
            get { return _jwk; }
            set { _jwk = value; OnPropertyChanged("Jwk"); }
        }

        public List<string> AlgorithmDropDown { get; }

        

        
        private void ExecuteSelectCertificateCommand()
        {
            
            if (!PreValidateInput()) return;

            var key = _pubKeyHandler.GetPublicKeyFromSertificate(Kid);

            if (string.IsNullOrEmpty(key))
            {
                PlatformDialogs.ShowMessage("Could not find Certificate: " + Kid);
                return;
            }
             

            var response = _jwkCreator.CreateJwk(key, Kid, Algorithm);

            if (response.Success == true && response.DigDirJwkString != null)
            {
                Jwk = response.DigDirJwkString;
            }
            else
            {

                PlatformDialogs.ShowMessage(response.ErrorCode);
            }
            return;
        }
       
        private async Task ExecuteCreateJwkCommand()
        {
           

            Kid = Kid.ToUpper();
            //var response = _jwkCreator.CreateJwk(_publicKey, Kid, Algorithm);

            if (!string.IsNullOrEmpty(Jwk))
            {                 
                var savePath = await PlatformDialogs.SaveFileAsync("certificate.jwk");
                if (savePath != null && _jwkCreator.SaveToFile(Jwk, savePath))
                {
                    PlatformDialogs.ShowMessage("Result saved successfully!");
                }
            }
            else
            {
                PlatformDialogs.ShowMessage("Failed to save Jwk to file");
            }
            return;
        }

        private bool PreValidateInput()
        {
            if (string.IsNullOrEmpty(Kid))
            {
            
                PlatformDialogs.ShowMessage("Enter Certificate Kid!");

                return false;
            }

            if (string.IsNullOrEmpty(Algorithm))
            {
                PlatformDialogs.ShowMessage("Select RSA Key!");

                return false;
            }
            return true;
        }

        private async Task ExecuteSelectFileCommand()
        {
            if (!PreValidateInput()) return;
            var path = await PlatformDialogs.OpenFileAsync();
            if (path is null) return;
            SelectedFilePath = path;
            FileLabel = "Selected File: " + Path.GetFileName(path);
            PublicKey = _pubKeyHandler.GetPublicKey(path);
            var response = _jwkCreator.CreateJwk(PublicKey, Kid, Algorithm);
            if (response.Success && response.DigDirJwkString is not null)
                Jwk = response.DigDirJwkString;
            else
                PlatformDialogs.ShowMessage(response.ErrorCode ?? "PEM conversion failed.");
        }

        private void ExecuteTestCommand()
        {
            _navigationService.Navigate();

        }
        
    }
}
