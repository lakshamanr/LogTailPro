# Accessibility — WCAG AA Compliance & Inclusive Design

Build applications that work for everyone, including users with disabilities.

---

## WCAG 2.1 Level AA Checklist

### Perceivable (Users can see/hear content)

**Color Contrast:**
- Text: 4.5:1 minimum (normal text)
- Large text (18px+): 3:1 minimum
- UI components: 3:1 minimum

Test with: Contrast Ratio Checker, WAVE, or WebAIM

```xaml
<!-- GOOD: High contrast -->
<TextBlock Text="Dark text on light" Foreground="#161616" Background="#FFFFFF"/>

<!-- BAD: Low contrast -->
<TextBlock Text="Gray text on white" Foreground="#D0D0D0" Background="#FFFFFF"/>
```

**Color Not Only:**
- Don't use color alone to convey information
- Always include text, icon, or pattern

```xaml
<!-- GOOD: Icon + color + text -->
<StackPanel Orientation="Horizontal" Spacing="8">
  <TextBlock Text="✓" Foreground="#107C10"/>
  <TextBlock Text="Success" Foreground="#107C10"/>
</StackPanel>

<!-- BAD: Color only -->
<Border Background="#107C10"/> <!-- What does this mean? -->
```

### Operable (Users can navigate with keyboard)

**Keyboard Navigation:**
- All interactive elements must be reachable via Tab key
- Logical tab order (left-to-right, top-to-bottom)
- Never trap focus

```xaml
<!-- Set TabIndex for logical order -->
<StackPanel>
  <Button Content="First" TabIndex="0"/>
  <Button Content="Second" TabIndex="1"/>
  <Button Content="Third" TabIndex="2"/>
</StackPanel>
```

**Focus Indicator:**
- Clear, visible focus border (minimum 3px, high contrast)
- Don't hide default focus rectangle

```xaml
<Style x:Key="AccessibleButtonStyle" TargetType="Button">
  <Setter Property="FocusVisualStyle">
    <Setter.Value>
      <Style>
        <Setter Property="Control.Template">
          <Setter.Value>
            <ControlTemplate>
              <Border BorderBrush="#0078D4" BorderThickness="2" CornerRadius="4" 
                      Margin="-2"/>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Style>
    </Setter.Value>
  </Setter>
</Style>
```

**Keyboard Shortcuts:**
- Document all keyboard shortcuts
- Provide Access keys (Alt+letter) for main actions

```xaml
<Button Content="_Save" ToolTip="Alt+S"/> <!-- Alt+S triggers button -->
```

### Understandable (Users understand content)

**Clear Labels:**
- Every input must have visible, associated label
- Use `AutomationProperties.Name` for screen readers

```xaml
<!-- GOOD: Label + input -->
<StackPanel Spacing="6">
  <TextBlock Text="Email Address" x:Name="EmailLabel"/>
  <TextBox AutomationProperties.LabeledBy="{Binding ElementName=EmailLabel}"/>
</StackPanel>

<!-- BAD: Input without label -->
<TextBox/> <!-- What is this for? -->
```

**Help Text:**
- Provide hints for complex inputs
- Error messages should be specific

```xaml
<StackPanel Spacing="6">
  <TextBlock Text="Password" FontWeight="SemiBold"/>
  <PasswordBox/>
  <TextBlock Text="Minimum 8 characters with uppercase, lowercase, number" 
             FontSize="12" Foreground="#737373"/>
</StackPanel>
```

### Robust (Works with assistive technology)

**Automation Properties (for screen readers):**

```xaml
<!-- Make invisible elements accessible to screen readers -->
<Button AutomationProperties.Name="Close dialog"
        AutomationProperties.HelpText="Press Escape key to close">
  ✕
</Button>

<!-- Label complex controls -->
<DataGrid AutomationProperties.Name="Employee data table"
          AutomationProperties.HelpText="Contains 5 columns and 200 rows"/>
```

---

## Screen Reader Testing

### JAWS (Windows) / NVDA (Free)

```xaml
<!-- GOOD: Properly structured -->
<StackPanel>
  <TextBlock Text="Login Form" Role="Heading" AutomationProperties.ItemStatus="Main"/>
  
  <StackPanel>
    <Label Target="{Binding ElementName=EmailInput}">Email</Label>
    <TextBox x:Name="EmailInput"/>
  </StackPanel>
  
  <Button Content="Sign In" AutomationProperties.Name="Sign in button"/>
</StackPanel>

<!-- BAD: Inaccessible structure -->
<Grid>
  <TextBox/> <!-- No label - reader has no idea what this is -->
  <Button Content="Click"/> <!-- Unhelpful name -->
</Grid>
```

**Test with:**
1. NVDA (free): https://www.nvaccess.org/
2. JAWS (paid): https://www.freedomscientific.com/
3. Windows Narrator (built-in): Win+Ctrl+Enter

---

## Contrast Checker Tool

```csharp
public class ContrastChecker
{
    /// Calculate contrast ratio between two colors
    public static double GetContrastRatio(Color color1, Color color2)
    {
        var lum1 = GetRelativeLuminance(color1);
        var lum2 = GetRelativeLuminance(color2);
        
        var lighter = Math.Max(lum1, lum2);
        var darker = Math.Min(lum1, lum2);
        
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;
        
        r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);
        
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    public static bool MeetsWCAGAA(double contrastRatio, bool largeText = false)
    {
        return largeText ? contrastRatio >= 3 : contrastRatio >= 4.5;
    }
}

// Usage
var ratio = ContrastChecker.GetContrastRatio(Colors.Black, Colors.White);
var passes = ContrastChecker.MeetsWCAGAA(ratio); // true
```

---

## Keyboard Navigation Implementation

```csharp
// Handle Tab, Escape, Enter in main window
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Tab: Native WPF handles this
        // Enter: Activate focused button
        if (e.Key == Key.Enter && FocusManager.GetFocusedElement(this) is Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
        }
        
        // Escape: Close dialog
        if (e.Key == Key.Escape)
        {
            if (ModalBackdrop.Visibility == Visibility.Visible)
            {
                HideModal();
                e.Handled = true;
            }
        }
    }
}
```

---

## Accessible Form Example

```xaml
<StackPanel Spacing="16" Padding="24">
  <TextBlock Text="Account Registration" FontSize="24" FontWeight="SemiBold"
             AutomationProperties.Name="Account registration form"/>
  
  <!-- Email field -->
  <StackPanel Spacing="6">
    <TextBlock Text="Email Address *" FontWeight="SemiBold" x:Name="EmailLabel"/>
    <TextBox x:Name="EmailInput" Height="36" Padding="12,8"
             AutomationProperties.LabeledBy="{Binding ElementName=EmailLabel}"
             AutomationProperties.IsRequiredForForm="True"/>
    <TextBlock Text="We'll send a confirmation link to this address" 
               FontSize="12" Foreground="#737373" Foreground="#737373"
               AutomationProperties.HelpText="Confirmation link will be sent to this email"/>
  </StackPanel>
  
  <!-- Password field -->
  <StackPanel Spacing="6">
    <TextBlock Text="Password *" FontWeight="SemiBold" x:Name="PasswordLabel"/>
    <PasswordBox x:Name="PasswordInput" Height="36" Padding="12,8"
                 AutomationProperties.LabeledBy="{Binding ElementName=PasswordLabel}"
                 AutomationProperties.IsRequiredForForm="True"/>
    <TextBlock Text="Min 8 characters: uppercase, lowercase, number, symbol" 
               FontSize="12" Foreground="#737373"
               AutomationProperties.HelpText="Requirements: 8+ characters, uppercase, lowercase, number, symbol"/>
  </StackPanel>
  
  <!-- Checkbox -->
  <CheckBox Content="I agree to the terms" AutomationProperties.Name="Accept terms and conditions"/>
  
  <!-- Button -->
  <Button Content="Create Account" Height="40" Background="#0078D4" Foreground="White"
          AutomationProperties.Name="Create account button"
          TabIndex="0"/>
</StackPanel>
```

---

## Color-Blind Testing

Test your palette for the three main types:

**Tools:**
- Color Oracle (free): https://colororacle.org/
- Sim Daltonism (Mac): Free
- WebAIM Color Blind Simulator: Online

**Avoid:**
- Red + green alone (8% of men are red-green colorblind)
- Use patterns + color (e.g., diagonal stripes + color)

---

## Motion & Animation Accessibility

Respect user's motion preferences:

```csharp
// Check if user prefers reduced motion
if (SystemParameters.MenuShowDelay == 0) // Indicates accessibility mode
{
    // Disable animations, use instant transitions
}
```

---

## Accessibility Checklist

- [ ] All text has sufficient contrast (4.5:1 minimum)
- [ ] Color not used as only way to convey information
- [ ] Focus visible on all interactive elements
- [ ] All form inputs have labels
- [ ] Logical Tab order (test with Tab key)
- [ ] All buttons/links have meaningful names
- [ ] Keyboard shortcuts documented
- [ ] No keyboard traps (can escape with Tab)
- [ ] Error messages specific and helpful
- [ ] Screen reader labels set (Name, HelpText)
- [ ] Images have alt text or description
- [ ] Animations can be disabled
- [ ] Tested with screen reader (NVDA, JAWS)
- [ ] Tested with keyboard only (no mouse)
- [ ] Meets WCAG AA minimum (Level A+)

---

## Summary

Accessible WPF apps:
- ✅ 4.5:1 contrast ratio (WCAG AA)
- ✅ Full keyboard navigation
- ✅ Clear focus indicators
- ✅ Proper labels for all inputs
- ✅ Screen reader compatible
- ✅ Works for everyone
