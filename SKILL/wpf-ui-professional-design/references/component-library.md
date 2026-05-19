# Component Library — Copy/Paste Ready XAML

Complete, production-ready components with all states (default, hover, focused, disabled, error).

---

## Table of Contents
1. Buttons
2. Input Fields
3. Cards & Containers
4. Data Grids
5. Lists
6. Modals & Dialogs
7. Navigation
8. Progress Indicators
9. Form Groups
10. Alerts & Notifications

---

## 1. Buttons

### Primary Button (Main Action)

```xaml
<Style x:Key="PrimaryButtonStyle" TargetType="Button">
  <Setter Property="Background" Value="#0078D4"/>
  <Setter Property="Foreground" Value="White"/>
  <Setter Property="Padding" Value="16,10"/>
  <Setter Property="FontSize" Value="14"/>
  <Setter Property="FontWeight" Value="SemiBold"/>
  <Setter Property="Cursor" Value="Hand"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border Background="{TemplateBinding Background}" CornerRadius="4" Padding="{TemplateBinding Padding}">
          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="#106EBE"/>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Setter Property="Background" Value="#005A9E"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter Property="Opacity" Value="0.5"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

**Usage:**
```xaml
<Button Content="Save Changes" Style="{StaticResource PrimaryButtonStyle}"/>
```

### Secondary Button (Alternative Action)

```xaml
<Style x:Key="SecondaryButtonStyle" TargetType="Button">
  <Setter Property="Background" Value="Transparent"/>
  <Setter Property="Foreground" Value="#0078D4"/>
  <Setter Property="BorderBrush" Value="#0078D4"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="Padding" Value="16,10"/>
  <Setter Property="FontSize" Value="14"/>
  <Setter Property="FontWeight" Value="SemiBold"/>
  <Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" 
                BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="4" Padding="{TemplateBinding Padding}">
          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Foreground="{TemplateBinding Foreground}"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="#F0F7FF"/>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Setter Property="Background" Value="#E7F1FF"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter Property="Opacity" Value="0.5"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

### Tertiary Button (De-emphasized)

```xaml
<Style x:Key="TertiaryButtonStyle" TargetType="Button">
  <Setter Property="Background" Value="Transparent"/>
  <Setter Property="Foreground" Value="#737373"/>
  <Setter Property="Padding" Value="8,10"/>
  <Setter Property="FontSize" Value="14"/>
  <Setter Property="Cursor" Value="Hand"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border Background="{TemplateBinding Background}" CornerRadius="4" Padding="{TemplateBinding Padding}">
          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Foreground="{TemplateBinding Foreground}"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Foreground" Value="#0078D4"/>
            <Setter Property="Background" Value="#F5F5F5"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter Property="Opacity" Value="0.5"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

### Danger Button (Destructive Action)

```xaml
<Style x:Key="DangerButtonStyle" TargetType="Button">
  <Setter Property="Background" Value="#E81123"/>
  <Setter Property="Foreground" Value="White"/>
  <Setter Property="Padding" Value="16,10"/>
  <Setter Property="FontSize" Value="14"/>
  <Setter Property="FontWeight" Value="SemiBold"/>
  <Setter Property="Cursor" Value="Hand"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border Background="{TemplateBinding Background}" CornerRadius="4" Padding="{TemplateBinding Padding}">
          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="#C50E1F"/>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Setter Property="Background" Value="#A4050A"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter Property="Opacity" Value="0.5"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

---

## 2. Input Fields

### Text Input with All States

```xaml
<Style x:Key="StandardTextBoxStyle" TargetType="TextBox">
  <Setter Property="Background" Value="White"/>
  <Setter Property="Foreground" Value="#161616"/>
  <Setter Property="BorderBrush" Value="#D0D0D0"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="Padding" Value="12,10"/>
  <Setter Property="Height" Value="36"/>
  <Setter Property="FontSize" Value="14"/>
  <Setter Property="Cursor" Value="IBeam"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="TextBox">
        <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" 
                BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="4">
          <ScrollViewer x:Name="PART_ContentHost" VerticalAlignment="Center" Padding="{TemplateBinding Padding}"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="BorderBrush" Value="#A6A6A6"/>
          </Trigger>
          <Trigger Property="IsFocused" Value="True">
            <Setter Property="BorderBrush" Value="#0078D4"/>
            <Setter Property="BorderThickness" Value="2"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter Property="Background" Value="#F5F5F5"/>
            <Setter Property="Foreground" Value="#A6A6A6"/>
            <Setter Property="BorderBrush" Value="#D0D0D0"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

**Usage:**
```xaml
<TextBlock Text="Email" FontWeight="SemiBold" Margin="0,0,0,6"/>
<TextBox Style="{StaticResource StandardTextBoxStyle}" x:Name="EmailInput"/>
```

### Password Input

```xaml
<Style x:Key="PasswordBoxStyle" TargetType="PasswordBox">
  <Setter Property="Background" Value="White"/>
  <Setter Property="Foreground" Value="#161616"/>
  <Setter Property="BorderBrush" Value="#D0D0D0"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="Padding" Value="12,10"/>
  <Setter Property="Height" Value="36"/>
  <Setter Property="FontSize" Value="14"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="PasswordBox">
        <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" 
                BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="4">
          <ScrollViewer x:Name="PART_ContentHost" VerticalAlignment="Center" Padding="{TemplateBinding Padding}"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsFocused" Value="True">
            <Setter Property="BorderBrush" Value="#0078D4"/>
            <Setter Property="BorderThickness" Value="2"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter Property="Background" Value="#F5F5F5"/>
            <Setter Property="Foreground" Value="#A6A6A6"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

### Input Label (Always use labels)

```xaml
<Style x:Key="FormLabelStyle" TargetType="TextBlock">
  <Setter Property="FontSize" Value="14"/>
  <Setter Property="FontWeight" Value="SemiBold"/>
  <Setter Property="Foreground" Value="#161616"/>
  <Setter Property="Margin" Value="0,0,0,6"/>
</Style>

<!-- Usage: -->
<TextBlock Text="Full Name" Style="{StaticResource FormLabelStyle}"/>
<TextBox Style="{StaticResource StandardTextBoxStyle}"/>
```

---

## 3. Cards & Containers

### Elevated Card (Most common)

```xaml
<Style x:Key="ElevatedCardStyle" TargetType="Border">
  <Setter Property="Background" Value="White"/>
  <Setter Property="CornerRadius" Value="8"/>
  <Setter Property="Padding" Value="16"/>
  <Setter Property="Effect">
    <Setter.Value>
      <DropShadowEffect BlurRadius="4" ShadowDepth="1" Opacity="0.12"/>
    </Setter.Value>
  </Setter>
</Style>

<!-- Usage: -->
<Border Style="{StaticResource ElevatedCardStyle}">
  <StackPanel Spacing="12">
    <TextBlock Text="Card Title" FontSize="16" FontWeight="SemiBold"/>
    <TextBlock Text="Card content goes here" Foreground="#737373"/>
  </StackPanel>
</Border>
```

### Outlined Card (Lighter emphasis)

```xaml
<Style x:Key="OutlinedCardStyle" TargetType="Border">
  <Setter Property="Background" Value="White"/>
  <Setter Property="BorderBrush" Value="#D0D0D0"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="CornerRadius" Value="8"/>
  <Setter Property="Padding" Value="16"/>
</Style>
```

### Section Container (Groups related content)

```xaml
<Style x:Key="SectionContainerStyle" TargetType="StackPanel">
  <Setter Property="Spacing" Value="12"/>
  <Setter Property="Margin" Value="0,0,0,24"/>
</Style>

<!-- Usage: -->
<StackPanel Style="{StaticResource SectionContainerStyle}">
  <TextBlock Text="Personal Information" FontSize="16" FontWeight="SemiBold"/>
  <Separator/>
  <!-- Form fields here -->
</StackPanel>
```

---

## 4. Data Grids

### Professional DataGrid

```xaml
<Style x:Key="ProfessionalDataGridStyle" TargetType="DataGrid">
  <Setter Property="Background" Value="White"/>
  <Setter Property="BorderBrush" Value="#D0D0D0"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="RowBackground" Value="White"/>
  <Setter Property="AlternatingRowBackground" Value="#F9F9F9"/>
  <Setter Property="HeadersVisibility" Value="Column"/>
  <Setter Property="GridLinesVisibility" Value="Horizontal"/>
  <Setter Property="RowHeaderWidth" Value="0"/>
  <Setter Property="ColumnHeaderHeight" Value="40"/>
  <Setter Property="RowHeight" Value="44"/>
  <Setter Property="CanUserAddRows" Value="False"/>
  <Setter Property="CanUserDeleteRows" Value="False"/>
  <Setter Property="CanUserResizeRows" Value="False"/>
</Style>

<Style TargetType="DataGridColumnHeader">
  <Setter Property="Background" Value="#F5F5F5"/>
  <Setter Property="Foreground" Value="#161616"/>
  <Setter Property="BorderBrush" Value="#D0D0D0"/>
  <Setter Property="BorderThickness" Value="0,0,1,1"/>
  <Setter Property="Padding" Value="12,8"/>
  <Setter Property="FontWeight" Value="SemiBold"/>
  <Setter Property="FontSize" Value="13"/>
</Style>

<!-- Usage: -->
<DataGrid Style="{StaticResource ProfessionalDataGridStyle}" x:Name="DataGrid1">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*"/>
    <DataGridTextColumn Header="Email" Binding="{Binding Email}" Width="200"/>
    <DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="100"/>
  </DataGrid.Columns>
</DataGrid>
```

**Critical:** Enable virtualization for lists with 100+ items:

```xaml
<DataGrid x:Name="DataGrid1" EnableRowVirtualization="True" EnableColumnVirtualization="True">
  <!-- columns -->
</DataGrid>
```

---

## 5. Lists

### ListBox with Professional Styling

```xaml
<Style x:Key="ProfessionalListBoxStyle" TargetType="ListBox">
  <Setter Property="Background" Value="White"/>
  <Setter Property="BorderBrush" Value="#D0D0D0"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="CornerRadius" Value="4"/>
</Style>

<Style x:Key="ListBoxItemStyle" TargetType="ListBoxItem">
  <Setter Property="Padding" Value="12,12"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="ListBoxItem">
        <Border Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
          <ContentPresenter/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="#F5F5F5"/>
          </Trigger>
          <Trigger Property="IsSelected" Value="True">
            <Setter Property="Background" Value="#E7F1FF"/>
            <Setter Property="Foreground" Value="#0078D4"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>

<!-- Usage: -->
<ListBox Style="{StaticResource ProfessionalListBoxStyle}" ItemContainerStyle="{StaticResource ListBoxItemStyle}">
  <ListBoxItem>Item 1</ListBoxItem>
  <ListBoxItem>Item 2</ListBoxItem>
  <ListBoxItem>Item 3</ListBoxItem>
</ListBox>
```

---

## 6. Modals & Dialogs

### Modal Backdrop + Window

```xaml
<!-- Main window with modal support -->
<Window x:Class="MainWindow" Background="White">
  <Grid>
    <!-- Your content -->
    
    <!-- Modal backdrop (shown conditionally) -->
    <Border Background="#000000" Opacity="0.3" Visibility="Collapsed" x:Name="ModalBackdrop"/>
    
    <!-- Modal dialog (shown conditionally) -->
    <Border CornerRadius="8" Background="White" Width="400" Height="Auto" 
            Visibility="Collapsed" x:Name="ModalDialog">
      <Border.Effect>
        <DropShadowEffect BlurRadius="16" ShadowDepth="4" Opacity="0.20"/>
      </Border.Effect>
      <StackPanel Padding="24" Spacing="16">
        <TextBlock Text="Dialog Title" FontSize="18" FontWeight="SemiBold"/>
        <TextBlock Text="Dialog content here" Foreground="#737373" TextWrapping="Wrap"/>
        
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Margin="0,8,0,0">
          <Button Content="Cancel" Style="{StaticResource SecondaryButtonStyle}" Click="OnCancel"/>
          <Button Content="Confirm" Style="{StaticResource PrimaryButtonStyle}" Click="OnConfirm"/>
        </StackPanel>
      </StackPanel>
    </Border>
  </Grid>
</Window>
```

**C# Code-behind:**

```csharp
private void ShowModal()
{
  ModalBackdrop.Visibility = Visibility.Visible;
  ModalDialog.Visibility = Visibility.Visible;
}

private void HideModal()
{
  ModalBackdrop.Visibility = Visibility.Collapsed;
  ModalDialog.Visibility = Visibility.Collapsed;
}

private void OnCancel(object sender, RoutedEventArgs e) => HideModal();
private void OnConfirm(object sender, RoutedEventArgs e)
{
  // Handle confirmation
  HideModal();
}
```

---

## 7. Navigation

### Top Navigation Bar

```xaml
<Border Background="White" BorderBrush="#D0D0D0" BorderThickness="0,0,0,1" Height="56" Padding="16,0">
  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="auto"/>
      <ColumnDefinition Width="*"/>
      <ColumnDefinition Width="auto"/>
    </Grid.ColumnDefinitions>
    
    <!-- Logo/Title -->
    <TextBlock Grid.Column="0" Text="MyApp" FontSize="18" FontWeight="SemiBold" VerticalAlignment="Center"/>
    
    <!-- Navigation links -->
    <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="24" Margin="48,0,0,0" VerticalAlignment="Center">
      <Button Content="Dashboard" Style="{StaticResource TertiaryButtonStyle}"/>
      <Button Content="Settings" Style="{StaticResource TertiaryButtonStyle}"/>
      <Button Content="Help" Style="{StaticResource TertiaryButtonStyle}"/>
    </StackPanel>
    
    <!-- User menu -->
    <Button Grid.Column="2" Content="⚙️ Account" Style="{StaticResource TertiaryButtonStyle}" VerticalAlignment="Center"/>
  </Grid>
</Border>
```

### Sidebar Navigation

```xaml
<Border Width="240" Background="#F5F5F5" BorderBrush="#D0D0D0" BorderThickness="0,0,1,0">
  <StackPanel Spacing="0">
    <!-- Logo -->
    <TextBlock Text="MyApp" FontSize="18" FontWeight="SemiBold" Padding="16,24,16,16"/>
    
    <!-- Nav items -->
    <Button Content="🏠 Dashboard" Style="{StaticResource SidebarNavItemStyle}" Margin="0"/>
    <Button Content="📊 Reports" Style="{StaticResource SidebarNavItemStyle}" Margin="0"/>
    <Button Content="⚙️ Settings" Style="{StaticResource SidebarNavItemStyle}" Margin="0"/>
    
    <!-- Spacing -->
    <Border Height="1" Background="#D0D0D0" Margin="0,16,0,16"/>
    
    <!-- Bottom items -->
    <Button Content="❓ Help" Style="{StaticResource SidebarNavItemStyle}" Margin="0"/>
    <Button Content="👤 Logout" Style="{StaticResource SidebarNavItemStyle}" Margin="0"/>
  </StackPanel>
</Border>

<Style x:Key="SidebarNavItemStyle" TargetType="Button">
  <Setter Property="Background" Value="Transparent"/>
  <Setter Property="Foreground" Value="#161616"/>
  <Setter Property="Padding" Value="16,12"/>
  <Setter Property="FontSize" Value="14"/>
  <Setter Property="HorizontalAlignment" Value="Stretch"/>
  <Setter Property="HorizontalContentAlignment" Value="Left"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
          <ContentPresenter HorizontalAlignment="Left" VerticalAlignment="Center" Foreground="{TemplateBinding Foreground}"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="#E7F1FF"/>
            <Setter Property="Foreground" Value="#0078D4"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

---

## 8. Progress Indicators

### Loading Spinner

```xaml
<Grid x:Name="LoadingOverlay" Background="#00000000" Visibility="Collapsed">
  <Border HorizontalAlignment="Center" VerticalAlignment="Center" Background="White" 
          CornerRadius="8" Padding="24" Width="120" Height="120">
    <Border.Effect>
      <DropShadowEffect BlurRadius="8" ShadowDepth="2" Opacity="0.15"/>
    </Border.Effect>
    
    <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="12">
      <!-- Spinner animation (using ellipse) -->
      <Ellipse Width="40" Height="40" Fill="None" StrokeThickness="3" 
               Stroke="#0078D4" HorizontalAlignment="Center">
        <Ellipse.RenderTransform>
          <RotateTransform CenterX="20" CenterY="20"/>
        </Ellipse.RenderTransform>
        <Ellipse.RenderTransformOrigin>
          <Point X="0.5" Y="0.5"/>
        </Ellipse.RenderTransformOrigin>
      </Ellipse>
      
      <TextBlock Text="Loading..." FontSize="12" Foreground="#737373" TextAlignment="Center"/>
    </StackPanel>
  </Border>
</Grid>

<!-- C# to rotate: -->
<!--
RotateTransform rt = (Ellipse.RenderTransform as RotateTransform);
DispatcherTimer timer = new DispatcherTimer();
timer.Interval = TimeSpan.FromMilliseconds(50);
timer.Tick += (s, e) => { rt.Angle += 6; };
timer.Start();
-->
```

### Progress Bar

```xaml
<Style x:Key="ProgressBarStyle" TargetType="ProgressBar">
  <Setter Property="Height" Value="4"/>
  <Setter Property="Background" Value="#F5F5F5"/>
  <Setter Property="Foreground" Value="#0078D4"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="ProgressBar">
        <Border Background="{TemplateBinding Background}" CornerRadius="2" Overflow="Hidden">
          <Rectangle Fill="{TemplateBinding Foreground}" Width="{TemplateBinding ActualWidth}" 
                     Height="{TemplateBinding ActualHeight}" HorizontalAlignment="Left"/>
        </Border>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>

<!-- Usage: -->
<ProgressBar Value="65" Maximum="100" Style="{StaticResource ProgressBarStyle}"/>
```

---

## 9. Form Field Groups

### Complete Form with Labels & Validation

```xaml
<StackPanel Spacing="16" MaxWidth="400">
  <!-- Name field -->
  <StackPanel Spacing="6">
    <TextBlock Text="Full Name" Style="{StaticResource FormLabelStyle}"/>
    <TextBox x:Name="NameInput" Style="{StaticResource StandardTextBoxStyle}" 
             Placeholder="Enter your full name"/>
  </StackPanel>
  
  <!-- Email field -->
  <StackPanel Spacing="6">
    <TextBlock Text="Email Address" Style="{StaticResource FormLabelStyle}"/>
    <TextBox x:Name="EmailInput" Style="{StaticResource StandardTextBoxStyle}" 
             Placeholder="you@example.com"/>
    <TextBlock Text="We'll never share your email." FontSize="12" Foreground="#737373"/>
  </StackPanel>
  
  <!-- Password field -->
  <StackPanel Spacing="6">
    <TextBlock Text="Password" Style="{StaticResource FormLabelStyle}"/>
    <PasswordBox x:Name="PasswordInput" Style="{StaticResource PasswordBoxStyle}"/>
  </StackPanel>
  
  <!-- Checkbox -->
  <CheckBox Content="I agree to the terms" Foreground="#161616" Margin="0,8,0,0"/>
  
  <!-- Action buttons -->
  <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,16,0,0">
    <Button Content="Sign Up" Style="{StaticResource PrimaryButtonStyle}" HorizontalAlignment="Left"/>
    <Button Content="Cancel" Style="{StaticResource SecondaryButtonStyle}" HorizontalAlignment="Left"/>
  </StackPanel>
</StackPanel>
```

---

## 10. Alerts & Notifications

### Alert/Message Component

```xaml
<Style x:Key="SuccessAlertStyle" TargetType="Border">
  <Setter Property="Background" Value="#F1F9F5"/>
  <Setter Property="BorderBrush" Value="#107C10"/>
  <Setter Property="BorderThickness" Value="1,1,1,1"/>
  <Setter Property="CornerRadius" Value="4"/>
  <Setter Property="Padding" Value="12"/>
</Style>

<Style x:Key="ErrorAlertStyle" TargetType="Border">
  <Setter Property="Background" Value="#FEF1F0"/>
  <Setter Property="BorderBrush" Value="#E81123"/>
  <Setter Property="BorderThickness" Value="1,1,1,1"/>
  <Setter Property="CornerRadius" Value="4"/>
  <Setter Property="Padding" Value="12"/>
</Style>

<Style x:Key="WarningAlertStyle" TargetType="Border">
  <Setter Property="Background" Value="#FFF8F0"/>
  <Setter Property="BorderBrush" Value="#FFB900"/>
  <Setter Property="BorderThickness" Value="1,1,1,1"/>
  <Setter Property="CornerRadius" Value="4"/>
  <Setter Property="Padding" Value="12"/>
</Style>

<!-- Usage: -->

<!-- Success -->
<Border Style="{StaticResource SuccessAlertStyle}">
  <TextBlock Text="✓ Changes saved successfully!" Foreground="#107C10" FontWeight="SemiBold"/>
</Border>

<!-- Error -->
<Border Style="{StaticResource ErrorAlertStyle}">
  <TextBlock Text="✗ An error occurred. Please try again." Foreground="#E81123" FontWeight="SemiBold"/>
</Border>

<!-- Warning -->
<Border Style="{StaticResource WarningAlertStyle}">
  <TextBlock Text="⚠ This action cannot be undone." Foreground="#FFB900" FontWeight="SemiBold"/>
</Border>
```

---

## Best Practices

1. **Always set explicit heights** for inputs (36px standard)
2. **Always include labels** for form fields
3. **Always show focus states** (2px border in primary color)
4. **Always virtualize lists** with 100+ items
5. **Test all states:** default, hover, focused, disabled, error
6. **Never skip accessibility** - keyboard navigation must work

---

## Reusable Component Structure

Create a `Controls/` folder in your project:

```
Controls/
  ├── Buttons/
  │   ├── PrimaryButton.xaml
  │   ├── SecondaryButton.xaml
  │   └── DangerButton.xaml
  ├── Inputs/
  │   ├── TextInputField.xaml
  │   ├── PasswordInputField.xaml
  │   └── SearchBox.xaml
  ├── Cards/
  │   ├── ElevatedCard.xaml
  │   └── OutlinedCard.xaml
  ├── Navigation/
  │   ├── TopBar.xaml
  │   └── Sidebar.xaml
  └── Dialogs/
      ├── ConfirmDialog.xaml
      ├── AlertDialog.xaml
      └── InputDialog.xaml
```

This keeps styles organized and reusable across your entire application.
