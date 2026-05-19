# UI Patterns — 20+ Production-Ready Layouts

Complete layouts for every common application scenario, with XAML implementation and best practices.

---

## Table of Contents
1. Authentication & Forms
2. Dashboards & Data Views
3. Master-Detail & Navigation
4. Modals & Dialogs
5. Empty & Loading States
6. Settings & Configuration
7. Advanced Patterns

---

## 1. Authentication & Forms

### Login Form

**Layout:**
- Centered card on neutral background
- Email + password fields
- "Remember me" checkbox
- Sign in + Sign up buttons
- "Forgot password?" link

**XAML:**

```xaml
<Grid Background="#F5F5F5">
  <Border Background="White" CornerRadius="8" Width="380" Height="auto" 
          HorizontalAlignment="Center" VerticalAlignment="Center">
    <Border.Effect>
      <DropShadowEffect BlurRadius="8" ShadowDepth="2" Opacity="0.15"/>
    </Border.Effect>
    
    <StackPanel Padding="32" Spacing="20">
      <!-- Header -->
      <TextBlock Text="Welcome Back" FontSize="24" FontWeight="SemiBold" Foreground="#161616"/>
      <TextBlock Text="Sign in to your account to continue" FontSize="13" Foreground="#737373"/>
      
      <!-- Email field -->
      <StackPanel Spacing="6">
        <TextBlock Text="Email" FontWeight="SemiBold" FontSize="13"/>
        <TextBox x:Name="EmailInput" Height="36" Padding="12" 
                 BorderBrush="#D0D0D0" BorderThickness="1" CornerRadius="4"/>
      </StackPanel>
      
      <!-- Password field -->
      <StackPanel Spacing="6">
        <TextBlock Text="Password" FontWeight="SemiBold" FontSize="13"/>
        <PasswordBox x:Name="PasswordInput" Height="36" Padding="12" 
                    BorderBrush="#D0D0D0" BorderThickness="1" CornerRadius="4"/>
      </StackPanel>
      
      <!-- Remember me -->
      <CheckBox Content="Remember me" Foreground="#161616"/>
      
      <!-- Sign in button -->
      <Button Content="Sign In" Height="36" FontSize="14" FontWeight="SemiBold"
              Background="#0078D4" Foreground="White" BorderThickness="0" CornerRadius="4"
              Cursor="Hand"/>
      
      <!-- Signup/forgot password -->
      <Grid>
        <TextBlock HorizontalAlignment="Left">
          <Hyperlink Foreground="#0078D4" TextDecorations="">Forgot password?</Hyperlink>
        </TextBlock>
        <TextBlock HorizontalAlignment="Right" Foreground="#737373">
          No account? 
          <Hyperlink Foreground="#0078D4" TextDecorations="">Sign up</Hyperlink>
        </TextBlock>
      </Grid>
    </StackPanel>
  </Border>
</Grid>
```

**Best Practices:**
- Center form on screen for focus
- Show password toggle (eye icon)
- Validate email format in real-time
- Give clear error messages
- Minimum touch target 44px

---

### Multi-Step Form / Wizard

**Layout:**
- Step indicator at top
- Content area with current step
- Previous/Next buttons
- Progress visual

**XAML:**

```xaml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="auto"/>
    <RowDefinition Height="*"/>
    <RowDefinition Height="auto"/>
  </Grid.RowDefinitions>
  
  <!-- Step indicator -->
  <StackPanel Grid.Row="0" Orientation="Horizontal" Spacing="0" Background="#F5F5F5" Padding="16" Height="60" VerticalAlignment="Center">
    <!-- Step 1 -->
    <Grid Width="80">
      <TextBlock Text="1" FontSize="16" FontWeight="SemiBold" Foreground="White" 
                 Background="#0078D4" Width="32" Height="32" CornerRadius="16" 
                 TextAlignment="Center" VerticalAlignment="Center" HorizontalAlignment="Left"/>
      <TextBlock Text="Account" FontSize="13" Margin="40,0,0,0" VerticalAlignment="Center"/>
    </Grid>
    
    <!-- Connector -->
    <Border Width="40" Height="2" Background="#D0D0D0" VerticalAlignment="Center" Margin="0,0,0,14"/>
    
    <!-- Step 2 -->
    <Grid Width="80">
      <TextBlock Text="2" FontSize="16" FontWeight="SemiBold" Foreground="#A6A6A6" 
                 Background="#D0D0D0" Width="32" Height="32" CornerRadius="16" 
                 TextAlignment="Center" VerticalAlignment="Center" HorizontalAlignment="Left"/>
      <TextBlock Text="Profile" FontSize="13" Margin="40,0,0,0" VerticalAlignment="Center" Foreground="#737373"/>
    </Grid>
    
    <!-- Connector -->
    <Border Width="40" Height="2" Background="#D0D0D0" VerticalAlignment="Center" Margin="0,0,0,14"/>
    
    <!-- Step 3 -->
    <Grid Width="80">
      <TextBlock Text="3" FontSize="16" FontWeight="SemiBold" Foreground="#A6A6A6" 
                 Background="#D0D0D0" Width="32" Height="32" CornerRadius="16" 
                 TextAlignment="Center" VerticalAlignment="Center" HorizontalAlignment="Left"/>
      <TextBlock Text="Review" FontSize="13" Margin="40,0,0,0" VerticalAlignment="Center" Foreground="#737373"/>
    </Grid>
  </StackPanel>
  
  <!-- Content area -->
  <ScrollViewer Grid.Row="1" Padding="32">
    <StackPanel MaxWidth="600" Spacing="16">
      <!-- Content changes based on current step -->
      <TextBlock Text="Step 1: Create Your Account" FontSize="20" FontWeight="SemiBold"/>
      <!-- Form fields here -->
    </StackPanel>
  </ScrollViewer>
  
  <!-- Footer buttons -->
  <Border Grid.Row="2" BorderBrush="#D0D0D0" BorderThickness="0,1,0,0" Padding="32,16" Background="White">
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
      <Button Content="Cancel" Width="120" Height="36" Background="Transparent" 
              Foreground="#0078D4" BorderBrush="#0078D4" BorderThickness="1"/>
      <Button Content="Previous" Width="120" Height="36" Background="Transparent" 
              Foreground="#0078D4" BorderBrush="#0078D4" BorderThickness="1"/>
      <Button Content="Next" Width="120" Height="36" Background="#0078D4" 
              Foreground="White" BorderThickness="0"/>
    </StackPanel>
  </Border>
</Grid>
```

**Best Practices:**
- Show progress clearly (step indicators)
- Allow going back (unless destructive)
- Save progress locally in case of accident
- Validate each step before allowing next
- Final step should be review/confirmation

---

## 2. Dashboards & Data Views

### Analytics Dashboard

**Layout:**
- Header with title + filters
- 4-column grid of KPI cards
- Large chart below
- Data table at bottom

**XAML:**

```xaml
<Grid Background="White">
  <Grid.RowDefinitions>
    <RowDefinition Height="auto"/>
    <RowDefinition Height="auto"/>
    <RowDefinition Height="auto"/>
    <RowDefinition Height="*"/>
  </Grid.RowDefinitions>
  
  <!-- Header -->
  <Border Grid.Row="0" BorderBrush="#D0D0D0" BorderThickness="0,0,0,1" Padding="24,16">
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="auto"/>
      </Grid.ColumnDefinitions>
      
      <StackPanel Grid.Column="0" Spacing="8">
        <TextBlock Text="Analytics Dashboard" FontSize="24" FontWeight="SemiBold"/>
        <TextBlock Text="Last updated: Today at 3:45 PM" FontSize="12" Foreground="#737373"/>
      </StackPanel>
      
      <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8" VerticalAlignment="Center">
        <ComboBox Width="120" SelectedIndex="0">
          <ComboBoxItem>Last 7 days</ComboBoxItem>
          <ComboBoxItem>Last 30 days</ComboBoxItem>
          <ComboBoxItem>Last 90 days</ComboBoxItem>
        </ComboBox>
        <Button Content="Export" Height="36" Padding="16,0" Background="#0078D4" Foreground="White"/>
      </StackPanel>
    </Grid>
  </Border>
  
  <!-- KPI Cards -->
  <Grid Grid.Row="1" Padding="24" RowSpacing="16" ColumnSpacing="16">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="*"/>
      <ColumnDefinition Width="*"/>
      <ColumnDefinition Width="*"/>
      <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>
    
    <!-- KPI Card 1 -->
    <Border Grid.Column="0" Background="White" CornerRadius="8" Padding="16" BorderBrush="#D0D0D0" BorderThickness="1">
      <StackPanel Spacing="8">
        <TextBlock Text="Revenue" FontSize="12" Foreground="#737373" FontWeight="SemiBold"/>
        <TextBlock Text="$124,500" FontSize="24" FontWeight="SemiBold" Foreground="#161616"/>
        <TextBlock Text="↑ 12% from last week" FontSize="11" Foreground="#107C10"/>
      </StackPanel>
    </Border>
    
    <!-- Similar cards for Users, Conversions, Churn -->
    <!-- (repeat pattern for remaining 3 cards) -->
  </Grid>
  
  <!-- Chart -->
  <Border Grid.Row="2" Padding="24" BorderBrush="#D0D0D0" BorderThickness="0,1,0,1">
    <StackPanel Spacing="12">
      <TextBlock Text="Revenue Trend" FontSize="14" FontWeight="SemiBold"/>
      <!-- Place chart component here -->
      <Rectangle Height="300" Fill="#F5F5F5" CornerRadius="4"/>
    </StackPanel>
  </Border>
  
  <!-- Data Table -->
  <Border Grid.Row="3" Padding="24">
    <StackPanel Spacing="12">
      <TextBlock Text="Recent Transactions" FontSize="14" FontWeight="SemiBold"/>
      <!-- DataGrid here -->
    </StackPanel>
  </Border>
</Grid>
```

**Best Practices:**
- Show KPIs above detailed data
- Use cards for clear visual separation
- Make charts responsive to data
- Include export/print functionality
- Add date range filters

---

## 3. Master-Detail & Navigation

### Master-Detail Layout (Sidebar + Content)

**Layout:**
- Left sidebar with list (master)
- Right content area (detail)
- Responsive (hide sidebar on small screens)

**XAML:**

```xaml
<Grid>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="300" MinWidth="240"/>
    <ColumnDefinition Width="*"/>
  </Grid.ColumnDefinitions>
  
  <!-- Master (Sidebar List) -->
  <Border Grid.Column="0" Background="#F5F5F5" BorderBrush="#D0D0D0" BorderThickness="0,0,1,0">
    <StackPanel Spacing="0">
      <!-- Search -->
      <TextBox Padding="12" Height="44" Placeholder="Search..." Margin="12,12,12,0"/>
      
      <!-- List -->
      <ListBox x:Name="ItemsList" ItemContainerStyle="{StaticResource ListItemStyle}">
        <ListBoxItem>Item 1</ListBoxItem>
        <ListBoxItem>Item 2</ListBoxItem>
        <ListBoxItem>Item 3</ListBoxItem>
      </ListBox>
    </StackPanel>
  </Border>
  
  <!-- Detail (Content Area) -->
  <Border Grid.Column="1" Background="White">
    <ScrollViewer Padding="32">
      <StackPanel MaxWidth="800" Spacing="24">
        <!-- Breadcrumb -->
        <StackPanel Orientation="Horizontal" Spacing="8" Foreground="#737373">
          <TextBlock Text="All Items" Foreground="#0078D4" Cursor="Hand"/>
          <TextBlock Text=">" Margin="4,0"/>
          <TextBlock Text="Item 1" FontWeight="SemiBold" Foreground="#161616"/>
        </StackPanel>
        
        <!-- Detail content -->
        <TextBlock Text="Item Details" FontSize="24" FontWeight="SemiBold"/>
        <TextBlock Text="Detailed information about the selected item goes here" Foreground="#737373" TextWrapping="Wrap"/>
        
        <!-- Action buttons -->
        <StackPanel Orientation="Horizontal" Spacing="8">
          <Button Content="Edit" Height="36" Padding="16,0" Background="#0078D4" Foreground="White"/>
          <Button Content="Delete" Height="36" Padding="16,0" Background="#E81123" Foreground="White"/>
        </StackPanel>
      </StackPanel>
    </ScrollViewer>
  </Border>
</Grid>
```

**Best Practices:**
- Include search in master list
- Highlight selected item
- Show breadcrumb in detail area
- Use keyboard navigation (arrow keys)
- Make master collapsible on narrow screens

---

### Tabbed Navigation

```xaml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="auto"/>
    <RowDefinition Height="*"/>
  </Grid.RowDefinitions>
  
  <!-- Tab bar -->
  <Border Grid.Row="0" Background="White" BorderBrush="#D0D0D0" BorderThickness="0,0,0,1" Height="48">
    <StackPanel Orientation="Horizontal" Spacing="0" VerticalAlignment="Center">
      <Button Content="Overview" Padding="24,0" Background="Transparent" Foreground="#0078D4" 
              BorderThickness="0,3,0,0" BorderBrush="#0078D4" Height="48" VerticalAlignment="Bottom"/>
      <Button Content="Details" Padding="24,0" Background="Transparent" Foreground="#737373" 
              BorderThickness="0,0,0,0" Height="48" VerticalAlignment="Bottom"/>
      <Button Content="History" Padding="24,0" Background="Transparent" Foreground="#737373" 
              BorderThickness="0,0,0,0" Height="48" VerticalAlignment="Bottom"/>
    </StackPanel>
  </Border>
  
  <!-- Tab content -->
  <ContentControl Grid.Row="1">
    <!-- Content changes based on selected tab -->
  </ContentControl>
</Grid>
```

---

## 4. Modals & Dialogs

### Confirmation Dialog

```xaml
<Border Background="#000000" Opacity="0.3" x:Name="Backdrop"/>
<Border Width="400" Height="auto" CornerRadius="8" Background="White">
  <Border.Effect>
    <DropShadowEffect BlurRadius="16" ShadowDepth="4" Opacity="0.20"/>
  </Border.Effect>
  
  <StackPanel Padding="24" Spacing="16">
    <!-- Icon -->
    <TextBlock Text="⚠" FontSize="32" Foreground="#FFB900" HorizontalAlignment="Center"/>
    
    <!-- Message -->
    <TextBlock Text="Are you sure?" FontSize="18" FontWeight="SemiBold" TextAlignment="Center"/>
    <TextBlock Text="This action cannot be undone." FontSize="13" Foreground="#737373" 
               TextAlignment="Center" TextWrapping="Wrap"/>
    
    <!-- Buttons -->
    <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,8,0,0">
      <Button Content="Cancel" Width="*" Height="36" Background="Transparent" 
              Foreground="#0078D4" BorderBrush="#0078D4" BorderThickness="1"/>
      <Button Content="Delete" Width="*" Height="36" Background="#E81123" Foreground="White"/>
    </StackPanel>
  </StackPanel>
</Border>
```

---

### Input Dialog

```xaml
<StackPanel Padding="24" Spacing="16" Width="400">
  <TextBlock Text="Enter Value" FontSize="18" FontWeight="SemiBold"/>
  
  <StackPanel Spacing="6">
    <TextBlock Text="Name" FontWeight="SemiBold" FontSize="13"/>
    <TextBox x:Name="InputField" Height="36" Padding="12" BorderBrush="#D0D0D0" BorderThickness="1"/>
  </StackPanel>
  
  <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,8,0,0">
    <Button Content="Cancel" Width="*" Height="36" Background="Transparent" Foreground="#0078D4" BorderThickness="1"/>
    <Button Content="OK" Width="*" Height="36" Background="#0078D4" Foreground="White"/>
  </StackPanel>
</StackPanel>
```

---

## 5. Empty & Loading States

### Empty State

```xaml
<Grid Background="White" Padding="32">
  <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="16" MaxWidth="400">
    <TextBlock Text="📭" FontSize="64" TextAlignment="Center"/>
    <TextBlock Text="No items yet" FontSize="18" FontWeight="SemiBold" TextAlignment="Center"/>
    <TextBlock Text="Get started by creating your first item." FontSize="13" 
               Foreground="#737373" TextAlignment="Center" TextWrapping="Wrap"/>
    <Button Content="Create Item" Height="36" Width="auto" Padding="24,0" 
            Background="#0078D4" Foreground="White" HorizontalAlignment="Center"/>
  </StackPanel>
</Grid>
```

### Loading State

```xaml
<Grid Background="White">
  <!-- Skeleton screen (placeholder while loading) -->
  <StackPanel Padding="24" Spacing="16">
    <!-- Title skeleton -->
    <Rectangle Height="24" Fill="#F5F5F5" CornerRadius="4" Width="200"/>
    
    <!-- Content skeletons -->
    <Rectangle Height="12" Fill="#F5F5F5" CornerRadius="4" Margin="0,16,0,0"/>
    <Rectangle Height="12" Fill="#F5F5F5" CornerRadius="4" Width="80%"/>
    <Rectangle Height="12" Fill="#F5F5F5" CornerRadius="4" Width="90%"/>
    
    <!-- Button skeleton -->
    <Rectangle Height="36" Fill="#F5F5F5" CornerRadius="4" Width="150" Margin="0,16,0,0"/>
  </StackPanel>
</Grid>
```

---

## 6. Settings & Configuration

### Settings Panel

```xaml
<Grid Background="White">
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="240" MinWidth="180"/>
    <ColumnDefinition Width="*"/>
  </Grid.ColumnDefinitions>
  
  <!-- Settings categories (sidebar) -->
  <Border Grid.Column="0" Background="#F5F5F5" BorderBrush="#D0D0D0" BorderThickness="0,0,1,0">
    <StackPanel Spacing="0" Padding="0">
      <Button Content="Account" Padding="16,12" HorizontalAlignment="Stretch" 
              HorizontalContentAlignment="Left" Background="#E7F1FF" Foreground="#0078D4" 
              BorderThickness="0" BorderThickness="3,0,0,0" BorderBrush="#0078D4"/>
      <Button Content="Privacy" Padding="16,12" HorizontalAlignment="Stretch" 
              HorizontalContentAlignment="Left" Background="Transparent" BorderThickness="0"/>
      <Button Content="Notifications" Padding="16,12" HorizontalAlignment="Stretch" 
              HorizontalContentAlignment="Left" Background="Transparent" BorderThickness="0"/>
      <Button Content="Billing" Padding="16,12" HorizontalAlignment="Stretch" 
              HorizontalContentAlignment="Left" Background="Transparent" BorderThickness="0"/>
    </StackPanel>
  </Border>
  
  <!-- Settings content -->
  <ScrollViewer Grid.Column="1" Padding="32">
    <StackPanel MaxWidth="600" Spacing="24">
      <TextBlock Text="Account Settings" FontSize="24" FontWeight="SemiBold"/>
      
      <!-- Setting group -->
      <StackPanel Spacing="16" BorderBrush="#D0D0D0" BorderThickness="0,1,0,0" Padding="0,16,0,0">
        <TextBlock Text="Email Address" FontSize="14" FontWeight="SemiBold"/>
        <TextBlock Text="your@email.com" Foreground="#737373"/>
        <Button Content="Change Email" Width="auto" Height="36" Padding="16,0" Background="#0078D4" Foreground="White"/>
      </StackPanel>
      
      <!-- Toggle setting -->
      <Border BorderBrush="#D0D0D0" BorderThickness="0,1,0,0" Padding="0,16,0,0">
        <Grid>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="auto"/>
          </Grid.ColumnDefinitions>
          
          <StackPanel Grid.Column="0" Spacing="4">
            <TextBlock Text="Two-Factor Authentication" FontSize="14" FontWeight="SemiBold"/>
            <TextBlock Text="Enhance your account security" FontSize="12" Foreground="#737373"/>
          </StackPanel>
          
          <CheckBox Grid.Column="1" VerticalAlignment="Center" IsChecked="False"/>
        </Grid>
      </Border>
    </StackPanel>
  </ScrollViewer>
</Grid>
```

---

## 7. Advanced Patterns

### Search + Results

```xaml
<Grid Background="White">
  <Grid.RowDefinitions>
    <RowDefinition Height="auto"/>
    <RowDefinition Height="*"/>
  </Grid.RowDefinitions>
  
  <!-- Search bar -->
  <Border Grid.Row="0" Padding="24,16" BorderBrush="#D0D0D0" BorderThickness="0,0,0,1">
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="auto"/>
      </Grid.ColumnDefinitions>
      
      <TextBox Grid.Column="0" Height="40" Padding="12,8" Placeholder="Search..." 
               BorderBrush="#D0D0D0" BorderThickness="1" CornerRadius="4"/>
      
      <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8" VerticalAlignment="Center" Margin="16,0,0,0">
        <Button Content="Filters" Height="40" Padding="16,0" Background="Transparent" 
                Foreground="#0078D4" BorderBrush="#0078D4" BorderThickness="1"/>
      </StackPanel>
    </Grid>
  </Border>
  
  <!-- Results -->
  <ListBox Grid.Row="1" Padding="24" Spacing="12">
    <!-- Result items -->
  </ListBox>
</Grid>
```

---

## Summary: Pattern Selection Guide

| Use Case | Pattern | Layout |
|----------|---------|--------|
| User login | Authentication Form | Centered card |
| Long form | Multi-step wizard | Steps + content + buttons |
| Data overview | Analytics dashboard | Cards + charts + table |
| Browse/select | Master-detail | Sidebar list + content |
| Multiple sections | Tabbed navigation | Tab bar + content |
| Quick decision | Confirmation dialog | Modal overlay |
| No data | Empty state | Centered icon + message |
| Waiting for data | Loading skeleton | Gray placeholder blocks |
| Preferences | Settings panel | Sidebar categories + settings |
| Find data | Search + results | Search bar + result list |

Each pattern is a starting point—adapt to your specific needs while maintaining the design system principles.
