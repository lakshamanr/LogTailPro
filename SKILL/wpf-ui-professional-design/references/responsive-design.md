# Responsive Design — Adaptive Layouts for All Screen Sizes

Build WPF applications that work from 800px to 4K displays.

---

## Breakpoint Strategy

Define breakpoints based on common window sizes:

```
Mobile:   < 600px
Tablet:   600px - 1024px  
Desktop:  1024px - 1920px
UltraWide: > 1920px
```

---

## Responsive Grid with Automatic Column Wrapping

```xaml
<!-- Parent container that adapts columns based on width -->
<ItemsControl x:Name="ItemsGrid" Background="White" Padding="24">
  <ItemsControl.ItemsPanel>
    <ItemsPanelTemplate>
      <WrapPanel Orientation="Horizontal" HorizontalAlignment="Left"/>
    </ItemsPanelTemplate>
  </ItemsControl.ItemsPanel>
  
  <ItemsControl.ItemTemplate>
    <DataTemplate>
      <!-- Cards that wrap automatically -->
      <Border Width="280" Height="200" Margin="8" Background="White" 
              BorderBrush="#D0D0D0" BorderThickness="1" CornerRadius="8">
        <StackPanel Padding="16" Spacing="12">
          <TextBlock Text="{Binding Title}" FontSize="14" FontWeight="SemiBold"/>
          <TextBlock Text="{Binding Description}" Foreground="#737373" TextWrapping="Wrap"/>
        </StackPanel>
      </Border>
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

---

## Responsive Column Layout

```xaml
<Grid>
  <Grid.ColumnDefinitions>
    <!-- This changes based on window width via C# code -->
    <ColumnDefinition Width="*" x:Name="Col1"/>
    <ColumnDefinition Width="*" x:Name="Col2" MinWidth="0"/>
    <ColumnDefinition Width="*" x:Name="Col3" MinWidth="0"/>
  </Grid.ColumnDefinitions>
  
  <!-- Content columns -->
  <Border Grid.Column="0" Background="White" Margin="8"/>
  <Border Grid.Column="1" Background="White" Margin="8"/>
  <Border Grid.Column="2" Background="White" Margin="8"/>
</Grid>
```

**C# Code-behind (adjust columns on resize):**

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.SizeChanged += (s, e) => UpdateLayout();
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        var width = this.ActualWidth;
        
        if (width < 800) // Mobile
        {
            // Show 1 column
            Col1.Width = new GridLength(1, GridUnitType.Star);
            Col2.MinWidth = 0;
            Col3.MinWidth = 0;
        }
        else if (width < 1200) // Tablet
        {
            // Show 2 columns
            Col1.Width = new GridLength(1, GridUnitType.Star);
            Col2.Width = new GridLength(1, GridUnitType.Star);
            Col3.MinWidth = 0;
        }
        else // Desktop
        {
            // Show 3 columns
            Col1.Width = new GridLength(1, GridUnitType.Star);
            Col2.Width = new GridLength(1, GridUnitType.Star);
            Col3.Width = new GridLength(1, GridUnitType.Star);
        }
    }
}
```

---

## Responsive Master-Detail

```xaml
<Grid>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="300" MinWidth="240" MaxWidth="400" x:Name="MasterColumn"/>
    <ColumnDefinition Width="*" x:Name="DetailColumn"/>
  </Grid.ColumnDefinitions>
  
  <!-- Master sidebar -->
  <Border Grid.Column="0" Background="#F5F5F5" x:Name="MasterPanel">
    <!-- List items -->
  </Border>
  
  <!-- Detail content -->
  <Border Grid.Column="1" Background="White" x:Name="DetailPanel">
    <!-- Content -->
  </Border>
</Grid>
```

**C# to hide master on narrow screens:**

```csharp
private void UpdateLayout()
{
    var width = this.ActualWidth;
    
    if (width < 800)
    {
        // Hide master, show detail only
        MasterPanel.Visibility = Visibility.Collapsed;
        MasterColumn.Width = new GridLength(0);
    }
    else
    {
        MasterPanel.Visibility = Visibility.Visible;
        MasterColumn.Width = new GridLength(300);
    }
}
```

---

## Responsive Form

```xaml
<ScrollViewer>
  <Grid MaxWidth="600" Margin="24">
    <!-- MaxWidth ensures form stays readable on ultra-wide screens -->
    <StackPanel Spacing="16">
      <!-- Form fields -->
    </StackPanel>
  </Grid>
</ScrollViewer>
```

---

## Responsive Navigation

```xaml
<!-- Top nav that collapses to hamburger menu on narrow screens -->
<Grid x:Name="NavBar" Height="56" Background="White" BorderBrush="#D0D0D0" BorderThickness="0,0,0,1" Padding="16,0">
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="auto"/>
    <ColumnDefinition Width="*"/>
    <ColumnDefinition Width="auto"/>
  </Grid.ColumnDefinitions>
  
  <!-- Logo -->
  <TextBlock Grid.Column="0" Text="MyApp" FontSize="18" FontWeight="SemiBold" VerticalAlignment="Center"/>
  
  <!-- Menu (hidden on small screens) -->
  <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="24" Margin="48,0,0,0" 
              VerticalAlignment="Center" x:Name="DesktopMenu">
    <Button Content="Home" Style="{StaticResource TertiaryButtonStyle}"/>
    <Button Content="About" Style="{StaticResource TertiaryButtonStyle}"/>
  </StackPanel>
  
  <!-- Hamburger menu (shown on small screens) -->
  <Button Grid.Column="2" Content="☰" FontSize="18" Background="Transparent" 
          Foreground="#161616" BorderThickness="0" x:Name="HamburgerMenu" VerticalAlignment="Center"/>
</Grid>
```

**C# to show/hide menu:**

```csharp
private void UpdateNavigation()
{
    var width = this.ActualWidth;
    DesktopMenu.Visibility = width > 800 ? Visibility.Visible : Visibility.Collapsed;
    HamburgerMenu.Visibility = width > 800 ? Visibility.Collapsed : Visibility.Visible;
}
```

---

## Flexible Spacing

Instead of fixed spacing, use responsive values:

```csharp
// In code-behind
private double GetResponsiveMargin()
{
    return this.ActualWidth switch
    {
        < 600 => 8,    // Mobile: tight spacing
        < 1024 => 16,  // Tablet: standard
        _ => 24        // Desktop: generous
    };
}
```

---

## Text Scaling

Make fonts responsive to screen size:

```csharp
private void UpdateFontSizes()
{
    var width = this.ActualWidth;
    
    if (width < 800)
    {
        TitleTextBlock.FontSize = 18;
        BodyTextBlock.FontSize = 12;
    }
    else
    {
        TitleTextBlock.FontSize = 24;
        BodyTextBlock.FontSize = 14;
    }
}
```

---

## Best Practices

1. **Test at multiple widths**: 800px, 1024px, 1440px, 1920px, 2560px
2. **No horizontal scrollbars**: Content should always fit
3. **Readable line length**: Keep text content to ~65 characters per line
4. **Touch-friendly sizes**: Button min size 44px on tablets/mobile
5. **Progressive disclosure**: Hide less important features on small screens
6. **Fluid spacing**: Use proportional spacing instead of fixed pixels
7. **Image scaling**: Use `Stretch="UniformToFill"` for images
8. **ListBox virtualization**: Always enable for long lists

---

## Summary

Responsive WPF apps:
- ✅ Adapt column counts based on width
- ✅ Show/hide content appropriately  
- ✅ Scale fonts and spacing
- ✅ Never require horizontal scrolling
- ✅ Work from 800px to 4K
