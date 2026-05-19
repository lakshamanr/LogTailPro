# Animations & Transitions — Professional Polish

Subtle, purposeful animations that enhance usability without distraction.

---

## Core Principle

**Animate purpose, not decoration.** Every animation should:
- Provide visual feedback (user knows action was registered)
- Guide attention (draw eye to important change)
- Maintain smoothness (never jumpy or slow)
- Keep performance good (60 FPS, <100ms typical)

---

## Fade Animation (Most Common)

```xaml
<!-- Fade in when element loads -->
<TextBlock Text="Hello" Opacity="0">
  <TextBlock.Triggers>
    <EventTrigger RoutedEvent="UIElement.Loaded">
      <BeginStoryboard>
        <Storyboard>
          <DoubleAnimation Storyboard.TargetProperty="Opacity" 
                          From="0" To="1" Duration="0:0:0.3"/>
        </Storyboard>
      </BeginStoryboard>
    </EventTrigger>
  </TextBlock.Triggers>
</TextBlock>
```

---

## Slide Animation

```xaml
<!-- Slide in from left -->
<Border>
  <Border.RenderTransform>
    <TranslateTransform X="-100"/>
  </Border.RenderTransform>
  
  <Border.Triggers>
    <EventTrigger RoutedEvent="UIElement.Loaded">
      <BeginStoryboard>
        <Storyboard>
          <DoubleAnimation 
            Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)"
            From="-100" To="0" Duration="0:0:0.3"/>
          <DoubleAnimation 
            Storyboard.TargetProperty="Opacity"
            From="0" To="1" Duration="0:0:0.3"/>
        </Storyboard>
      </BeginStoryboard>
    </EventTrigger>
  </Border.Triggers>
</Border>
```

---

## Scale/Grow Animation (CTA Button)

```xaml
<!-- Button that grows slightly on hover -->
<Button Content="Click Me" x:Name="CTAButton">
  <Button.Triggers>
    <EventTrigger RoutedEvent="UIElement.MouseEnter">
      <BeginStoryboard>
        <Storyboard>
          <DoubleAnimation Storyboard.TargetName="CTAButton" 
                          Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)"
                          To="1.05" Duration="0:0:0.2"/>
          <DoubleAnimation Storyboard.TargetName="CTAButton" 
                          Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)"
                          To="1.05" Duration="0:0:0.2"/>
        </Storyboard>
      </BeginStoryboard>
    </EventTrigger>
    <EventTrigger RoutedEvent="UIElement.MouseLeave">
      <BeginStoryboard>
        <Storyboard>
          <DoubleAnimation Storyboard.TargetName="CTAButton" 
                          Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)"
                          To="1" Duration="0:0:0.2"/>
          <DoubleAnimation Storyboard.TargetName="CTAButton" 
                          Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)"
                          To="1" Duration="0:0:0.2"/>
        </Storyboard>
      </BeginStoryboard>
    </EventTrigger>
  </Button.Triggers>
  
  <Button.RenderTransform>
    <ScaleTransform ScaleX="1" ScaleY="1"/>
  </Button.RenderTransform>
</Button>
```

---

## Color Transition

```xaml
<!-- Button changes color smoothly on hover -->
<Button Content="Hover Me" x:Name="HoverButton" 
        Background="#0078D4" Foreground="White">
  <Button.Triggers>
    <EventTrigger RoutedEvent="UIElement.MouseEnter">
      <BeginStoryboard>
        <Storyboard>
          <ColorAnimation Storyboard.TargetProperty="(Button.Background).(SolidColorBrush.Color)"
                         To="#106EBE" Duration="0:0:0.2"/>
        </Storyboard>
      </BeginStoryboard>
    </EventTrigger>
    <EventTrigger RoutedEvent="UIElement.MouseLeave">
      <BeginStoryboard>
        <Storyboard>
          <ColorAnimation Storyboard.TargetProperty="(Button.Background).(SolidColorBrush.Color)"
                         To="#0078D4" Duration="0:0:0.2"/>
        </Storyboard>
      </BeginStoryboard>
    </EventTrigger>
  </Button.Triggers>
</Button>
```

---

## Loading Spinner (Rotation)

```xaml
<Grid>
  <Ellipse x:Name="LoadingSpinner" Width="40" Height="40" 
           Stroke="#0078D4" StrokeThickness="3" Fill="None"
           HorizontalAlignment="Center" VerticalAlignment="Center">
    <Ellipse.RenderTransform>
      <RotateTransform CenterX="20" CenterY="20"/>
    </Ellipse.RenderTransform>
    
    <Ellipse.Triggers>
      <EventTrigger RoutedEvent="UIElement.Loaded">
        <BeginStoryboard RepeatBehavior="Forever">
          <Storyboard>
            <DoubleAnimation 
              Storyboard.TargetName="LoadingSpinner"
              Storyboard.TargetProperty="(UIElement.RenderTransform).(RotateTransform.Angle)"
              From="0" To="360" Duration="0:0:2"/>
          </Storyboard>
        </BeginStoryboard>
      </EventTrigger>
    </Ellipse.Triggers>
  </Ellipse>
</Grid>
```

---

## Pulse Animation (Attention)

```xaml
<!-- Element pulses to draw attention -->
<Border x:Name="PulseBox" Width="100" Height="100" Background="#FFB900" CornerRadius="8">
  <Border.Triggers>
    <EventTrigger RoutedEvent="UIElement.Loaded">
      <BeginStoryboard RepeatBehavior="Forever">
        <Storyboard>
          <DoubleAnimation 
            Storyboard.TargetName="PulseBox"
            Storyboard.TargetProperty="Opacity"
            From="1" To="0.5" Duration="0:0:1" AutoReverse="True"/>
        </Storyboard>
      </BeginStoryboard>
    </EventTrigger>
  </Border.Triggers>
</Border>
```

---

## Modal Backdrop Fade (Page Transition)

```xaml
<!-- Backdrop fades in -->
<Border x:Name="ModalBackdrop" Background="Black" Opacity="0">
  <Border.Triggers>
    <EventTrigger RoutedEvent="UIElement.Loaded">
      <BeginStoryboard>
        <Storyboard>
          <DoubleAnimation 
            Storyboard.TargetName="ModalBackdrop"
            Storyboard.TargetProperty="Opacity"
            To="0.3" Duration="0:0:0.2"/>
        </Storyboard>
      </BeginStoryboard>
    </EventTrigger>
  </Border.Triggers>
</Border>

<!-- Dialog slides up and fades in -->
<Border x:Name="Modal" Opacity="0" RenderTransformOrigin="0.5,0.5">
  <Border.RenderTransform>
    <TransformGroup>
      <ScaleTransform ScaleX="1" ScaleY="0.9"/>
      <TranslateTransform Y="50"/>
    </TransformGroup>
  </Border.RenderTransform>
  
  <Border.Triggers>
    <EventTrigger RoutedEvent="UIElement.Loaded">
      <BeginStoryboard>
        <Storyboard>
          <DoubleAnimation 
            Storyboard.TargetName="Modal"
            Storyboard.TargetProperty="Opacity"
            From="0" To="1" Duration="0:0:0.3"/>
          <DoubleAnimation 
            Storyboard.TargetName="Modal"
            Storyboard.TargetProperty="(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"
            From="50" To="0" Duration="0:0:0.3"/>
          <DoubleAnimation 
            Storyboard.TargetName="Modal"
            Storyboard.TargetProperty="(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"
            From="0.9" To="1" Duration="0:0:0.3"/>
        </Storyboard>
      </BeginStoryboard>
    </EventTrigger>
  </Border.Triggers>
</Border>
```

---

## C# Code-Behind Animations

For more control, use C#:

```csharp
public class AnimationHelper
{
    public static void FadeIn(UIElement element, int durationMs = 300)
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    public static void SlideInFromLeft(UIElement element, int durationMs = 300)
    {
        var transform = new TranslateTransform { X = -100 };
        element.RenderTransform = transform;
        
        var animation = new DoubleAnimation
        {
            From = -100,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        
        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    public static void ScaleTo(UIElement element, double scale, int durationMs = 200)
    {
        var transform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        element.RenderTransform = transform;
        
        var animationX = new DoubleAnimation
        {
            To = scale,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        
        var animationY = new DoubleAnimation
        {
            To = scale,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animationX);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animationY);
    }
}

// Usage
AnimationHelper.FadeIn(MyElement, 300);
AnimationHelper.SlideInFromLeft(MyElement, 300);
```

---

## Easing Functions

Choose appropriate easing for different animations:

```
EaseInOut:   Smooth, natural (default)
EaseIn:      Fast start, slow end (entrances)
EaseOut:     Slow start, fast end (exits)
Linear:      Constant speed (progress bars)
BounceEase:  Elastic feel (playful)
BackEase:    Slight overshoot (lively)
```

---

## When NOT to Animate

❌ Avoid animating:
- Rapid list updates (causes visual chaos)
- Layouts that change frequently (expensive)
- More than 2-3 animations per interaction (overwhelming)
- Animations longer than 500ms (feels slow)
- Animations during data loading (adds perceived latency)

---

## Performance Tips

1. **Keep animations short** (<300ms typical)
2. **Use GPU acceleration** (RenderTransform, not Margin)
3. **Avoid animating Layout properties** (Margin, Width, Height)
4. **Disable animations in focus mode** (accessibility)
5. **Profile before/after** (ensure smooth 60 FPS)

---

## Summary

Professional animations:
- ✅ Fade, slide, scale for common effects
- ✅ Smooth easing functions
- ✅ Duration 200-300ms (fast)
- ✅ Purposeful (not decorative)
- ✅ Performance-conscious (GPU accelerated)
