# Theming System — Dark Mode, Light Mode & Custom Themes

Build a complete dynamic theming system that allows runtime theme switching.

---

## Complete App.xaml Resource Dictionary

Create a centralized theme system in your `App.xaml`:

```xaml
<Application x:Class="YourApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
  <Application.Resources>
    
    <!-- ============================================
         COLOR PALETTE - LIGHT THEME (Default)
         ============================================ -->
    
    <!-- Primary Brand Colors -->
    <SolidColorBrush x:Key="PrimaryBrush">#0078D4</SolidColorBrush>
    <SolidColorBrush x:Key="PrimaryDarkBrush">#106EBE</SolidColorBrush>
    <SolidColorBrush x:Key="PrimaryDarkerBrush">#005A9E</SolidColorBrush>
    
    <!-- Semantic Colors -->
    <SolidColorBrush x:Key="SuccessBrush">#107C10</SolidColorBrush>
    <SolidColorBrush x:Key="ErrorBrush">#E81123</SolidColorBrush>
    <SolidColorBrush x:Key="WarningBrush">#FFB900</SolidColorBrush>
    <SolidColorBrush x:Key="InfoBrush">#0078D4</SolidColorBrush>
    
    <!-- Surface Colors (Light Theme) -->
    <SolidColorBrush x:Key="SurfacePrimaryBrush">#FFFFFF</SolidColorBrush>
    <SolidColorBrush x:Key="SurfaceSecondaryBrush">#F5F5F5</SolidColorBrush>
    <SolidColorBrush x:Key="SurfaceTertiaryBrush">#EBEBEB</SolidColorBrush>
    
    <!-- Text Colors (Light Theme) -->
    <SolidColorBrush x:Key="TextPrimaryBrush">#161616</SolidColorBrush>
    <SolidColorBrush x:Key="TextSecondaryBrush">#737373</SolidColorBrush>
    <SolidColorBrush x:Key="TextTertiaryBrush">#A6A6A6</SolidColorBrush>
    <SolidColorBrush x:Key="TextDisabledBrush">#D0D0D0</SolidColorBrush>
    
    <!-- UI Element Colors (Light Theme) -->
    <SolidColorBrush x:Key="BorderBrush">#D0D0D0</SolidColorBrush>
    <SolidColorBrush x:Key="DividerBrush">#EBEBEB</SolidColorBrush>
    <SolidColorBrush x:Key="BackgroundBrush">#F9F9F9</SolidColorBrush>
    
    <!-- ============================================
         TYPOGRAPHY
         ============================================ -->
    
    <FontFamily x:Key="DefaultFont">Segoe UI</FontFamily>
    
    <!-- Font Size Hierarchy -->
    <sys:Double x:Key="FontSizeDisplay">28</sys:Double>
    <sys:Double x:Key="FontSizeHeading1">20</sys:Double>
    <sys:Double x:Key="FontSizeHeading2">16</sys:Double>
    <sys:Double x:Key="FontSizeBody">14</sys:Double>
    <sys:Double x:Key="FontSizeCaption">12</sys:Double>
    
    <!-- Font Weight -->
    <FontWeight x:Key="FontWeightRegular">Regular</FontWeight>
    <FontWeight x:Key="FontWeightMedium">Medium</FontWeight>
    <FontWeight x:Key="FontWeightSemiBold">SemiBold</FontWeight>
    <FontWeight x:Key="FontWeightBold">Bold</FontWeight>
    
    <!-- ============================================
         SPACING
         ============================================ -->
    
    <sys:Double x:Key="SpacingXS">4</sys:Double>
    <sys:Double x:Key="SpacingSM">8</sys:Double>
    <sys:Double x:Key="SpacingMD">16</sys:Double>
    <sys:Double x:Key="SpacingLG">24</sys:Double>
    <sys:Double x:Key="SpacingXL">32</sys:Double>
    <sys:Double x:Key="SpacingXXL">48</sys:Double>
    
    <!-- ============================================
         CORNER RADIUS
         ============================================ -->
    
    <CornerRadius x:Key="CornerRadiusSmall">4</CornerRadius>
    <CornerRadius x:Key="CornerRadiusMedium">8</CornerRadius>
    <CornerRadius x:Key="CornerRadiusLarge">12</CornerRadius>
    
    <!-- ============================================
         COMPONENT STYLES
         ============================================ -->
    
    <!-- Primary Button -->
    <Style x:Key="PrimaryButtonStyle" TargetType="Button">
      <Setter Property="Background" Value="{StaticResource PrimaryBrush}"/>
      <Setter Property="Foreground" Value="White"/>
      <Setter Property="Padding" Value="16,10"/>
      <Setter Property="FontSize" Value="{StaticResource FontSizeBody}"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="BorderThickness" Value="0"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border Background="{TemplateBinding Background}" 
                    CornerRadius="{StaticResource CornerRadiusSmall}"
                    Padding="{TemplateBinding Padding}">
              <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="{StaticResource PrimaryDarkBrush}"/>
              </Trigger>
              <Trigger Property="IsEnabled" Value="False">
                <Setter Property="Opacity" Value="0.5"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    
    <!-- Standard TextBox -->
    <Style x:Key="StandardTextBoxStyle" TargetType="TextBox">
      <Setter Property="Background" Value="{StaticResource SurfacePrimaryBrush}"/>
      <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
      <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Padding" Value="12,10"/>
      <Setter Property="Height" Value="36"/>
      <Setter Property="FontSize" Value="{StaticResource FontSizeBody}"/>
    </Style>
    
    <!-- Form Label -->
    <Style x:Key="FormLabelStyle" TargetType="TextBlock">
      <Setter Property="FontSize" Value="{StaticResource FontSizeBody}"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
      <Setter Property="Margin" Value="0,0,0,6"/>
    </Style>
    
  </Application.Resources>
</Application>
```

---

## Theme Manager — Runtime Switching

Create a C# class to manage theme switching:

**ThemeManager.cs:**

```csharp
using System;
using System.Windows;
using System.Windows.Media;

namespace YourApp.Services
{
    public enum ThemeMode
    {
        Light,
        Dark,
        Auto
    }

    public class ThemeManager
    {
        private static ThemeManager _instance;
        public static ThemeManager Instance => _instance ??= new ThemeManager();

        private ThemeMode _currentTheme = ThemeMode.Light;

        public event EventHandler ThemeChanged;

        public ThemeMode CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ApplyTheme(value);
                    ThemeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void ApplyTheme(ThemeMode theme)
        {
            var resourceDict = theme switch
            {
                ThemeMode.Light => new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/LightTheme.xaml") },
                ThemeMode.Dark => new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/DarkTheme.xaml") },
                ThemeMode.Auto => GetAutoTheme(),
                _ => throw new ArgumentException("Unknown theme", nameof(theme))
            };

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(resourceDict);
        }

        private ResourceDictionary GetAutoTheme()
        {
            // Windows 10/11: Check system theme preference
            var isDarkMode = GetSystemThemePreference();
            return isDarkMode 
                ? new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/DarkTheme.xaml") }
                : new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/LightTheme.xaml") };
        }

        private bool GetSystemThemePreference()
        {
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme", 1);
                return (int)value == 0; // 0 = Dark, 1 = Light
            }
            catch
            {
                return false; // Default to Light if registry read fails
            }
        }

        public void ToggleTheme()
        {
            CurrentTheme = CurrentTheme == ThemeMode.Light ? ThemeMode.Dark : ThemeMode.Light;
        }
    }
}
```

---

## Dark Theme Resource Dictionary

Create `Themes/DarkTheme.xaml`:

```xaml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  
  <!-- ============================================
       DARK THEME COLORS
       ============================================ -->
  
  <!-- Primary Brand (stays same) -->
  <SolidColorBrush x:Key="PrimaryBrush">#0078D4</SolidColorBrush>
  <SolidColorBrush x:Key="PrimaryDarkBrush">#1084D9</SolidColorBrush>
  <SolidColorBrush x:Key="PrimaryDarkerBrush">#1A8ADF</SolidColorBrush>
  
  <!-- Semantic Colors (adjusted for contrast) -->
  <SolidColorBrush x:Key="SuccessBrush">#34B34B</SolidColorBrush>
  <SolidColorBrush x:Key="ErrorBrush">#F17F7F</SolidColorBrush>
  <SolidColorBrush x:Key="WarningBrush">#FFD335</SolidColorBrush>
  <SolidColorBrush x:Key="InfoBrush">#0078D4</SolidColorBrush>
  
  <!-- Surface Colors (Dark Theme) -->
  <SolidColorBrush x:Key="SurfacePrimaryBrush">#1F1F1F</SolidColorBrush>
  <SolidColorBrush x:Key="SurfaceSecondaryBrush">#2D2D2D</SolidColorBrush>
  <SolidColorBrush x:Key="SurfaceTertiaryBrush">#3A3A3A</SolidColorBrush>
  
  <!-- Text Colors (Dark Theme) -->
  <SolidColorBrush x:Key="TextPrimaryBrush">#FFFFFF</SolidColorBrush>
  <SolidColorBrush x:Key="TextSecondaryBrush">#C9C9C9</SolidColorBrush>
  <SolidColorBrush x:Key="TextTertiaryBrush">#969696</SolidColorBrush>
  <SolidColorBrush x:Key="TextDisabledBrush">#5A5A5A</SolidColorBrush>
  
  <!-- UI Element Colors (Dark Theme) -->
  <SolidColorBrush x:Key="BorderBrush">#464646</SolidColorBrush>
  <SolidColorBrush x:Key="DividerBrush">#3A3A3A</SolidColorBrush>
  <SolidColorBrush x:Key="BackgroundBrush">#0D0D0D</SolidColorBrush>
  
  <!-- Typography (same as light theme) -->
  <FontFamily x:Key="DefaultFont">Segoe UI</FontFamily>
  <sys:Double x:Key="FontSizeDisplay">28</sys:Double>
  <sys:Double x:Key="FontSizeHeading1">20</sys:Double>
  <sys:Double x:Key="FontSizeHeading2">16</sys:Double>
  <sys:Double x:Key="FontSizeBody">14</sys:Double>
  <sys:Double x:Key="FontSizeCaption">12</sys:Double>
  
  <!-- Spacing (same as light theme) -->
  <sys:Double x:Key="SpacingXS">4</sys:Double>
  <sys:Double x:Key="SpacingSM">8</sys:Double>
  <sys:Double x:Key="SpacingMD">16</sys:Double>
  <sys:Double x:Key="SpacingLG">24</sys:Double>
  <sys:Double x:Key="SpacingXL">32</sys:Double>
  
</ResourceDictionary>
```

---

## Usage in XAML

Once defined, use theme resources throughout your app:

```xaml
<!-- TextBlock using theme colors -->
<TextBlock Text="Hello World" 
           Foreground="{StaticResource TextPrimaryBrush}"
           FontSize="{StaticResource FontSizeHeading1}"/>

<!-- Button using theme -->
<Button Content="Save" Style="{StaticResource PrimaryButtonStyle}"/>

<!-- TextBox using theme -->
<TextBox Style="{StaticResource StandardTextBoxStyle}"/>

<!-- Border using theme -->
<Border Background="{StaticResource SurfacePrimaryBrush}"
        BorderBrush="{StaticResource BorderBrush}">
  <!-- Content -->
</Border>
```

---

## Theme Switcher UI

Add a theme switcher to your app settings/menu:

```xaml
<!-- Theme selector in settings -->
<StackPanel Spacing="12">
  <TextBlock Text="Theme" FontWeight="SemiBold" FontSize="14"/>
  
  <ComboBox x:Name="ThemeSelector" SelectedIndex="0" Height="36">
    <ComboBoxItem>Light</ComboBoxItem>
    <ComboBoxItem>Dark</ComboBoxItem>
    <ComboBoxItem>System</ComboBoxItem>
  </ComboBox>
</StackPanel>
```

**Code-behind:**

```csharp
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        
        // Load current theme
        ThemeSelector.SelectedIndex = (int)ThemeManager.Instance.CurrentTheme;
        
        // Listen for theme changes
        ThemeSelector.SelectionChanged += (s, e) =>
        {
            if (Enum.TryParse<ThemeMode>(ThemeSelector.SelectedItem.ToString(), out var theme))
            {
                ThemeManager.Instance.CurrentTheme = theme;
                SaveUserPreference(theme);
            }
        };
    }

    private void SaveUserPreference(ThemeMode theme)
    {
        Properties.Settings.Default.Theme = theme.ToString();
        Properties.Settings.Default.Save();
    }
}
```

---

## Best Practices

1. **Consistency**: Use the same resource keys everywhere
2. **Semantic naming**: Use "Primary", "Success", "Error" not "Blue", "Green", "Red"
3. **Sufficient contrast**: Dark theme text must be light enough (WCAG AA: 4.5:1)
4. **Test both themes**: Review UI in light AND dark mode
5. **Persisting preference**: Save user's theme choice in app settings
6. **System sync**: Allow "Auto" mode to follow Windows theme
7. **Gradual transition**: Consider adding a fade animation when switching themes

---

## Advanced: Custom Brand Themes

For multi-branded applications, extend the system:

```xaml
<!-- BlueTheme.xaml -->
<SolidColorBrush x:Key="PrimaryBrush">#0078D4</SolidColorBrush>

<!-- OrangeTheme.xaml -->
<SolidColorBrush x:Key="PrimaryBrush">#FF8C00</SolidColorBrush>

<!-- PurpleTheme.xaml -->
<SolidColorBrush x:Key="PrimaryBrush">#8764B8</SolidColorBrush>
```

Switch between them the same way using `ThemeManager`.

---

## Summary

With this theming system you can:
- ✅ Switch between light/dark modes instantly
- ✅ Auto-detect system preference
- ✅ Persist user preference
- ✅ Support custom brand themes
- ✅ Maintain consistency across entire app
- ✅ Update all colors in one place
