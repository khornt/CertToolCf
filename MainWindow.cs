
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Extensions.DependencyInjection;
using CertTool.Services;
using CertTool.ViewModels;

namespace CertTool;

public sealed class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly ContentControl _content = new();

    public MainWindow(IServiceProvider services)
    {
        _services = services;

        Title = "CertTool — Certificate Manager";
        Width = 780;
        Height = 790;
        MinWidth = 620;
        MinHeight = 600;

        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        Foreground = Brushes.White;

        PlatformDialogs.Owner = this;

        var assemblyName = typeof(MainWindow).Assembly.GetName().Name;

        var background = new ImageBrush
        {
            Source = new Bitmap(AssetLoader.Open(
                new Uri(
                    $"avares://{assemblyName}/Images/PemBackground.png"))),
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };

        // One background behind both navigation and page content.
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = background
        };

        var tabs = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(14),
            Background = Brushes.Transparent
        };

        tabs.Children.Add(
            NavButton("PEM / JWK", ShowPem));

        tabs.Children.Add(
            NavButton("Let's Encrypt", ShowAcme));

        tabs.Children.Add(
            NavButton("Self-signed", ShowSelfSigned));

        Grid.SetRow(tabs, 0);
        layout.Children.Add(tabs);

        // Vertical scrolling only. Horizontal scrolling is disabled
        // to constrain the page to the available window width.
        var pageSurface = new Border
        {
            Background = Brushes.Transparent,

            Child = new ScrollViewer
            {
                Background = Brushes.Transparent,
                Content = _content,

                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,

                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto
            }
        };

        Grid.SetRow(pageSurface, 1);
        layout.Children.Add(pageSurface);

        Content = layout;

        ShowPem();
    }

    private static Button NavButton(string caption, Action action)
    {
        var button = new Button
        {
            Content = caption,
            MinWidth = 120
        };

        button.Click += (_, _) => action();

        return button;
    }

    private static StackPanel Panel() => new()
    {
        Margin = new Thickness(24),
        Spacing = 12,
        Background = Brushes.Transparent,

        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 21,
        FontWeight = FontWeight.SemiBold
    };

    private static void Field(
        StackPanel parent,
        string label,
        TextBox input)
    {
        parent.Children.Add(new TextBlock
        {
            Text = label
        });

        var container = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3*,1*")
        };

        Grid.SetColumn(input, 0);
        container.Children.Add(input);

        parent.Children.Add(container);
    }

    private static TextBox BoundText(
        Func<string?> get,
        Action<string> set)
    {
        var box = new TextBox
        {
            Text = get() ?? "",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        box.TextChanged += (_, _) =>
            set(box.Text ?? "");

        return box;
    }

    private static TextBox Password(
        Func<string?> get,
        Action<string> set)
    {
        var box = BoundText(get, set);
        box.PasswordChar = '●';

        return box;
    }

    private static void PasswordField(
        StackPanel panel,
        string label,
        Func<string?> get,
        Action<string> set)
    {
        panel.Children.Add(new TextBlock
        {
            Text = label
        });

        // Outer grid limits the entire password row to 75%.
        var container = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3*,1*")
        };

        // Inner grid contains the password and visibility toggle.
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10
        };

        var box = Password(get, set);

        box.HorizontalAlignment = HorizontalAlignment.Stretch;

        Grid.SetColumn(box, 0);
        row.Children.Add(box);

        var eye = new CheckBox
        {
            Content = "Show",
            VerticalAlignment = VerticalAlignment.Center
        };

        eye.IsCheckedChanged += (_, _) =>
        {
            box.PasswordChar = eye.IsChecked == true ? '\0' : '●';
        };

        Grid.SetColumn(eye, 1);
        row.Children.Add(eye);

        Grid.SetColumn(row, 0);
        container.Children.Add(row);

        panel.Children.Add(container);
    }

    private void ShowAcme()
    {
        var vm = _services
            .GetRequiredService<CreateCertificateViewModel>();

        var panel = Panel();

        panel.Children.Add(
            Heading("Let's Encrypt certificate (Porkbun DNS-01)"));

        Field(
            panel,
            "Fully qualified domain name",
            BoundText(() => vm.FQDN, v => vm.FQDN = v));

        Field(
            panel,
            "Contact email",
            BoundText(() => vm.Email, v => vm.Email = v));

        PasswordField(
            panel,
            "PFX password",
            () => vm.PfxPassword,
            v => vm.PfxPassword = v);

        PasswordField(
            panel,
            "Confirm PFX password",
            () => vm.ConfirmPfxPassword,
            v => vm.ConfirmPfxPassword = v);

        var mismatch = new TextBlock
        {
            Text = "Both PFX passwords must match.",
            IsVisible = !vm.PasswordsMatch
        };

        panel.Children.Add(mismatch);

        var start = new Button
        {
            Content = "Create Certificate (Automatic)",
            Command = vm.CreateCertificateCommand,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        panel.Children.Add(start);

        var progress = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 285,
            Text = vm.ChallengeText
        };

        ConfigureTextBoxScrolling(progress);

        panel.Children.Add(
            new TextBlock { Text = "ACME progress" });

        panel.Children.Add(progress);

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.ChallengeText))
                progress.Text = vm.ChallengeText;

            if (e.PropertyName == nameof(vm.PasswordsMatch))
                mismatch.IsVisible = !vm.PasswordsMatch;
        };

        _content.Content = panel;
    }

    private void ShowPem()
    {
        var vm = _services
            .GetRequiredService<PemConverterViewModel>();

        var panel = Panel();

        panel.Children.Add(
            Heading("PEM certificate to JWK"));

        Field(
            panel,
            "Key identifier (kid)",
            BoundText(() => vm.Kid, v => vm.Kid = v));

        var algorithm = new ComboBox
        {
            ItemsSource = vm.AlgorithmDropDown,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        algorithm.SelectionChanged += (_, _) =>
            vm.Algorithm =
                algorithm.SelectedItem?.ToString() ?? "";

        vm.Algorithm =
            algorithm.SelectedItem?.ToString() ?? "";

        panel.Children.Add(
            new TextBlock { Text = "RSA algorithm" });

        panel.Children.Add(algorithm);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        actions.Children.Add(new Button
        {
            Content = "Open PEM",
            Command = vm.SelectFileCommand
        });

        actions.Children.Add(new Button
        {
            Content = "From certificate store",
            Command = vm.SelectCertificateCommand
        });

        actions.Children.Add(new Button
        {
            Content = "Save JWK",
            Command = vm.CreateJwkFileCommand
        });

        panel.Children.Add(actions);

        var path = new TextBlock
        {
            Text = vm.FileLabel,
            TextWrapping = TextWrapping.Wrap
        };

        panel.Children.Add(path);

        // Long Base64/JWK values now wrap inside the available width.
        var output = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,

            HorizontalAlignment = HorizontalAlignment.Stretch,

            Height = 300,
            Text = vm.Jwk
        };

        ConfigureTextBoxScrolling(output);

        panel.Children.Add(output);

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Jwk))
                output.Text = vm.Jwk;

            if (e.PropertyName == nameof(vm.FileLabel))
                path.Text = vm.FileLabel;
        };

        _content.Content = panel;
    }

    private void ShowSelfSigned()
    {
        var vm = _services
            .GetRequiredService<SelfSignedCertViewModel>();

        var panel = Panel();

        panel.Children.Add(
            Heading("Self-signed certificate"));

        Field(
            panel,
            "Common name / hostname",
            BoundText(() => vm.CommonName,
                v => vm.CommonName = v));

        Field(
            panel,
            "Organization",
            BoundText(() => vm.Org,
                v => vm.Org = v));

        Field(
            panel,
            "Organizational unit",
            BoundText(() => vm.OrgUnit,
                v => vm.OrgUnit = v));

        Field(
            panel,
            "Locality",
            BoundText(() => vm.Localilty,
                v => vm.Localilty = v));

        Field(
            panel,
            "State",
            BoundText(() => vm.State,
                v => vm.State = v));

        Field(
            panel,
            "Country",
            BoundText(() => vm.Country,
                v => vm.Country = v));

        var algo = new ComboBox
        {
            ItemsSource = vm.Algorithms,
            SelectedItem = vm.SelectedAlgorithm
        };

        algo.SelectionChanged += (_, _) =>
        {
            if (algo.SelectedItem is
                CertificateGenerator.KeyAlgorithm value)
            {
                vm.SelectedAlgorithm = value;
            }
        };

        panel.Children.Add(
            new TextBlock { Text = "Key algorithm" });

        panel.Children.Add(algo);

        var size = new ComboBox
        {
            ItemsSource = vm.KeySizes,
            SelectedItem = vm.SelectedKeySize
        };

        size.SelectionChanged += (_, _) =>
        {
            if (size.SelectedItem is int value)
                vm.SelectedKeySize = value;
        };

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.SelectedAlgorithm))
                size.SelectedItem = vm.SelectedKeySize;
        };

        panel.Children.Add(
            new TextBlock { Text = "Key size" });

        panel.Children.Add(size);

        var hash = new ComboBox
        {
            ItemsSource = vm.SignatureHashes,
            SelectedItem = vm.SelectedSignatureHash
        };

        hash.SelectionChanged += (_, _) =>
            vm.SelectedSignatureHash =
                hash.SelectedItem?.ToString() ?? "SHA256";

        panel.Children.Add(
            new TextBlock { Text = "Signature hash" });

        panel.Children.Add(hash);

        PasswordField(
            panel,
            "Root CA password",
            () => vm.RootPwd,
            v => vm.RootPwd = v);

        PasswordField(
            panel,
            "Certificate password",
            () => vm.CertPwd,
            v => vm.CertPwd = v);

        panel.Children.Add(new Button
        {
            Content = "Generate self-signed certificate",
            Command = vm.SelfSignedCertCommand,
            HorizontalAlignment = HorizontalAlignment.Left
        });

        panel.Children.Add(new Button
        {
            Content = "Get token and copy JSON",
            Command = vm.TokenCommand,
            HorizontalAlignment = HorizontalAlignment.Left
        });

        _content.Content = panel;
    }

    // Attached ScrollViewer properties are supported by Avalonia TextBox.
    private static void ConfigureTextBoxScrolling(TextBox textBox)
    {
        ScrollViewer.SetHorizontalScrollBarVisibility(
            textBox,
            ScrollBarVisibility.Disabled);

        ScrollViewer.SetVerticalScrollBarVisibility(
            textBox,
            ScrollBarVisibility.Auto);
    }
}
