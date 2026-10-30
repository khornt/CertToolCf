using System.Windows.Input;
using CertTool.Commands;
using CertTool.Services;
using CertTool.Stores;

namespace CertTool.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly NavigationStore _navigationStore;
        


        public ViewModelBase? CurrentViewModel => _navigationStore.CurrentViewModel;
        public ICommand PemConverterCommand { get; internal set; }
        public ICommand CreateCertificateCommand { get; internal set; }
        public ICommand SelfSignedCertPageCommand { get; internal set; }



        public MainViewModel(
            NavigationStore navigationStore,
            NavigationService<PemConverterViewModel> pemNavigationService,
            NavigationService<SelfSignedCertViewModel> selfSignedNavigationService,
            NavigationService<CreateCertificateViewModel> certificateNavigationService)
        {
            _navigationStore = navigationStore;

            PemConverterCommand = new DelegateCommand(
                pemNavigationService.Navigate);

            SelfSignedCertPageCommand = new DelegateCommand(
                selfSignedNavigationService.Navigate);

            CreateCertificateCommand = new DelegateCommand(
                certificateNavigationService.Navigate);

            _navigationStore.CurrentViewModelChanged += OnCurrentViewModelChanged;
        }

        private void OnCurrentViewModelChanged()
        {
            OnPropertyChanged(nameof(CurrentViewModel));
        }

    }
}