using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using CertTool.Backend.LetsEncrypt.Entities;
using CertTool.Backend.LetsEncrypt.IO;
using CertTool.Backend.LetsEncrypt.WebClient;
using CertTool.Backend.OAuthTest;
using CertTool.Models;
using CertTool.Models.Jwk;
using CertTool.Services;
using CertTool.Services.Acme;
using CertTool.Services.Porkbun;
using CertTool.Stores;
using CertTool.ViewModels;

namespace CertTool;
public class App : Application
{
    private ServiceProvider? _services;
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationStore>();
        services.AddSingleton<JwkCreator>();
        services.AddSingleton<PubKeyHandler>();
        services.AddSingleton<AppConfig>();
        services.AddSingleton<NavigationService<PemConverterViewModel>>(sp => new(
            sp.GetRequiredService<NavigationStore>(), () => sp.GetRequiredService<PemConverterViewModel>()));
        services.AddSingleton<NavigationService<CreateCertificateViewModel>>(sp => new(
            sp.GetRequiredService<NavigationStore>(), () => sp.GetRequiredService<CreateCertificateViewModel>()));
        services.AddSingleton<NavigationService<SelfSignedCertViewModel>>(sp => new(
            sp.GetRequiredService<NavigationStore>(), () => sp.GetRequiredService<SelfSignedCertViewModel>()));
        services.AddSingleton<MainViewModel>();
        services.AddTransient<CreateCertificateViewModel>();
        services.AddTransient<PemConverterViewModel>();
        services.AddTransient<SelfSignedCertViewModel>();
        services.AddHttpClient<PorkbunDnsService>();
        services.AddSingleton<DnsPropagationService>();
        services.AddSingleton<AcmeAccountRepository>();
        services.AddSingleton<IAcmeService, AcmeService>();
        services.AddSingleton<TokenService>();
        services.AddSingleton(new AcmeClient(ApiEnvironment.LetsEncryptV2));
        services.AddSingleton<LocalStorage>();
        services.AddSingleton<CertificateExportService>();
        
                
        ConfigureButtonStyles();
        ConfigureTextBoxStyles();
        
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(_services);
            desktop.Exit += (_, _) => _services.Dispose();
        }
        base.OnFrameworkInitializationCompleted();

    }
    
    
    private void ConfigureTextBoxStyles()
    {
        var normal = new SolidColorBrush(Color.Parse("#606060"));
        var hover = new SolidColorBrush(Color.Parse("#808080"));
        var focused = new SolidColorBrush(Color.Parse("#4388C7"));

        // Normal textbox border
        var normalStyle = new Style(x =>
            x.OfType<TextBox>()
                .Template()
                .OfType<Border>()
                .Name("PART_BorderElement"));

        normalStyle.Setters.Add(
            new Setter(Border.BorderBrushProperty, normal));

        normalStyle.Setters.Add(
            new Setter(Border.BorderThicknessProperty, new Thickness(1)));

        Styles.Add(normalStyle);

        // Mouse-over border
        var hoverStyle = new Style(x =>
            x.OfType<TextBox>()
                .Class(":pointerover")
                .Template()
                .OfType<Border>()
                .Name("PART_BorderElement"));

        hoverStyle.Setters.Add(
            new Setter(Border.BorderBrushProperty, hover));

        Styles.Add(hoverStyle);

        // Focused textbox border
        var focusStyle = new Style(x =>
            x.OfType<TextBox>()
                .Class(":focus")
                .Template()
                .OfType<Border>()
                .Name("PART_BorderElement"));

        focusStyle.Setters.Add(
            new Setter(Border.BorderBrushProperty, focused));

        focusStyle.Setters.Add(
            new Setter(Border.BorderThicknessProperty, new Thickness(1)));

        Styles.Add(focusStyle);
    }

    
    private void ConfigureButtonStyles()
    {
        var normal = new SolidColorBrush(Color.Parse("#353535"));
        var hover = new SolidColorBrush(Color.Parse("#505050"));
        var pressed = new SolidColorBrush(Color.Parse("#252525"));

        Styles.Add(new Style(x => x.OfType<Button>())
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, normal),
                new Setter(Button.ForegroundProperty, Brushes.White),
                new Setter(Button.OpacityProperty, 1.0),
                new Setter(Button.BorderBrushProperty,
                    new SolidColorBrush(Color.Parse("#606060"))),
                new Setter(Button.BorderThicknessProperty,
                    new Thickness(1))
            }
        });

        Styles.Add(new Style(x => x.OfType<Button>().Class(":pointerover"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, hover),
                new Setter(Button.OpacityProperty, 1.0)
            }
        });

        Styles.Add(new Style(x => x.OfType<Button>().Class(":pressed"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, pressed),
                new Setter(Button.OpacityProperty, 1.0)
            }
        });
    }
    
    
    
}
