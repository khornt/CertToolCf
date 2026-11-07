using CertTool.Commands;
using CertTool.Services;
using System.Collections.ObjectModel;
using System.IO;

using System.Windows.Input;


namespace CertTool.ViewModels
{
    public class SelfSignedCertViewModel : ViewModelBase
    {
        public ICommand SelfSignedCertCommand { get; private set; }
        private readonly INavigationService _navigationService;
        
        public Array Algorithms { get; } = Enum.GetValues(typeof(CertificateGenerator.KeyAlgorithm));
        public string[] SignatureHashes { get; } = ["SHA256", "SHA384", "SHA512"];
        public ObservableCollection<int> KeySizes { get; } = [2048, 3072, 4096];

        private CertificateGenerator.KeyAlgorithm _selectedAlgorithm = CertificateGenerator.KeyAlgorithm.Rsa;
        public CertificateGenerator.KeyAlgorithm SelectedAlgorithm
        {
            get => _selectedAlgorithm;
            set
            {
                if (_selectedAlgorithm == value) return;
                _selectedAlgorithm = value;
                OnPropertyChanged();
                KeySizes.Clear();
                foreach (var size in CertificateGenerator.AllowedKeySizes(value))
                    KeySizes.Add(size);
                SelectedKeySize = KeySizes[0];
                SelectedSignatureHash = "SHA256";
            }
        }

        private int _selectedKeySize = 2048;
        public int SelectedKeySize
        {
            get => _selectedKeySize;
            set { _selectedKeySize = value; OnPropertyChanged(); }
        }

        private string _selectedSignatureHash = "SHA256";
        public string SelectedSignatureHash
        {
            get => _selectedSignatureHash;
            set { _selectedSignatureHash = value; OnPropertyChanged(); }
        }

        private string _commonName = string.Empty;
        private string _orgUnit = string.Empty;
        private string _org = string.Empty;
        private string _localilty = string.Empty;
        private string _state = string.Empty;
        private string _country = string.Empty;
        private string _rootPwd = string.Empty;
        private string _certPwd = string.Empty;
        
        public SelfSignedCertViewModel(NavigationService<SelfSignedCertViewModel> navigationService)
        {
            _navigationService = navigationService;
            SelfSignedCertCommand = new TaskDelegateCommand(ExecuteCreateSertificateCommand, CanExecute);
        }

        private async Task ExecuteCreateSertificateCommand()
        {
            var pfxFilePath = await PlatformDialogs.SelectFolderAsync();
            if (pfxFilePath is null) return;
            var hostname = CommonName;
            var subject = $"CN={CommonName}, OU={OrgUnit}, O={Org}, " +
                          $"L={Localilty}, S={State}, C={Country}";
            var rootPassword = RootPwd;
            var certPassword = CertPwd;
            var algorithm = SelectedAlgorithm;
            var keySize = SelectedKeySize;
            var hash = SelectedSignatureHash;

            try
            {
                await Task.Run(() => CertificateGenerator.Generate(
                    pfxFilePath, hostname, subject, rootPassword, certPassword,
                    algorithm: algorithm, keySize: keySize, signatureHash: hash));
            }
            catch (IOException ex)
            {
                PlatformDialogs.ShowMessage(ex.Message);
            }
            catch (Exception ex)
            {
                PlatformDialogs.ShowMessage(ex.Message);
            }
        }

        private bool CanExecute()
        {
            return true;
        }
        
        public string RootPwd
        {
            get { return _rootPwd; }
            set { _rootPwd = value; OnPropertyChanged("RootPwd"); }
        }
        
        public string CertPwd
        {
            get { return _certPwd; }
            set { _certPwd = value; OnPropertyChanged("CertPwd"); }
        }

        public string CommonName
        {
            get { return _commonName; }
            set { _commonName = value; OnPropertyChanged("CommonName"); }
        }
        
        public string OrgUnit
        {
            get { return _orgUnit; }
            set { _orgUnit = value; OnPropertyChanged("OrgUnit"); }
        }
        public string Org
        {
            get { return _org; }
            set { _org = value; OnPropertyChanged("Org"); }
        }
        public string Localilty
        {
            get { return _localilty; }
            set { _localilty = value; OnPropertyChanged("Localilty"); }
        }
        public string State
        {
            get { return _state; }
            set { _state = value; OnPropertyChanged("State"); }
        }
        public string Country
        {
            get { return _country; }
            set { _country = value; OnPropertyChanged("Country"); }
        }
    }
}
