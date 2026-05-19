---
name: wpf-ui-professional-design
description: Complete production-grade WPF UI design system with everything needed for professional applications. Covers design principles, component library, 20+ UI patterns, theming, responsive design, animations, accessibility, performance, and troubleshooting. Use whenever designing, building, reviewing, or refining WPF interfaces—forms, dashboards, data grids, modals, navigation, themes, animations, responsive layouts, or ensuring professional quality before deployment.
compatibility: WPF (.NET Framework 4.5+, .NET 6+, .NET 8), XAML, C#, Visual Studio
---

# WPF UI Professional Design System — Complete Production Guide

**Everything you need to ship professional-grade WPF applications.**

This is your comprehensive design authority covering design principles, complete component library, 20+ proven UI patterns, theming architecture, responsive design, accessibility compliance, animations, performance optimization, and production testing.

---

## Quick Navigation

### 🎨 **Design Foundations** (Start here)
- Visual hierarchy, typography, color systems, spacing, elevation
- See: **Design Principles** section below

### 🧩 **Component Library** (Copy/paste ready)
- Buttons, inputs, cards, data grids, lists, modals, navigation
- See: `references/component-library.md`

### 📐 **UI Patterns by Scenario** (20+ patterns)
- Forms, dashboards, master-detail, wizards, search, settings panels
- See: `references/ui-patterns.md`

### 🎨 **Theming & Dark Mode** (Complete system)
- Build switchable themes, dark mode, color schemes
- See: `references/theming-system.md`

### 📱 **Responsive Design** (Adaptive layouts)
- Responsive grids, adaptive layouts, window resizing strategies
- See: `references/responsive-design.md`

### ✨ **Animations & Transitions** (Professional polish)
- Micro-animations, fade/slide/scale, motion principles
- See: `references/animations.md`

### ♿ **Accessibility** (WCAG Compliance)
- Screen readers, keyboard navigation, contrast, focus management
- See: `references/accessibility.md`

### 🔧 **WPF Troubleshooting** (Fix common issues)
- Layout problems, performance drains, styling issues
- See: `references/troubleshooting.md`

### ⚡ **Performance Optimization** (Keep it snappy)
- Virtualization, deferred loading, rendering optimization
- See: `references/performance.md`

### ✅ **UI Quality Testing** (Verify before shipping)
- Checklists, automated testing, design validation
- See: `references/testing.md`

---

## Design Principles — The Foundation

### 1. Visual Hierarchy
**Every screen should have crystal-clear importance levels.**

```
Primary Action (CTA)  ← 50% visual weight
Secondary Actions     ← 30% visual weight  
Tertiary/Support      ← 20% visual weight
```

**Implementation:**
- **Primary**: Filled buttons with brand color, largest font, prominent placement
- **Secondary**: Outlined/ghost buttons, medium font, standard placement
- **Tertiary**: Text-only buttons, small font, lower visibility

**Test:** Close your eyes, open them for 2 seconds. Can you identify the primary action immediately? If not, adjust.

### 2. Typography System — Professional & Consistent

**Define exactly 5 levels (no more):**

| Level | Size | Weight | Usage |
|-------|------|--------|-------|
| Display | 28px | SemiBold | Page titles |
| Heading 1 | 20px | SemiBold | Dialog titles, section headers |
| Heading 2 | 16px | SemiBold | Sub-section headers |
| Body | 14px | Regular | Main content, descriptions |
| Caption | 12px | Regular | Hints, metadata, timestamps |

**Rules:**
- Single font family throughout (Segoe UI, Inter, or system default)
- Line height 1.4–1.6 for readability
- Never add a 6th size (it breaks the system)
- Weight limit: Regular, Medium, SemiBold, Bold only

**Bad:** 11px, 13px, 15px, 17px, 19px, 24px (too many inconsistent sizes)
**Good:** 12px (caption), 14px (body), 16px (heading), 20px (h1), 28px (display)

### 3. Color System — Intentional Palette

**Five categories:**

**1. Primary Brand Color** (1 color)
- Used for main CTA, active states, highlights
- Example: #0078D4 (Microsoft blue)
- Rule: Use sparingly, only for interactive elements

**2. Semantic Colors** (5 colors)
- Success: #107C10 (green) — confirmations, valid states
- Error: #E81123 (red) — errors, failures, dangerous actions
- Warning: #FFB900 (amber) — alerts, cautions
- Info: #0078D4 (blue) — informational messages
- Neutral: #A6A6A6 (gray) — disabled, secondary states

**3. Neutral/Gray Scale** (7 shades)
```
#FFFFFF — Pure white (surfaces)
#F9F9F9 — Off-white (hover backgrounds)
#F5F5F5 — Light gray (section backgrounds)
#D0D0D0 — Medium gray (borders, dividers)
#A6A6A6 — Medium-dark gray (secondary text, disabled)
#737373 — Dark gray (primary text alt)
#161616 — Near-black (primary text)
```

**4. Interaction Colors** (states)
- Hover: 10% darker than base
- Pressed: 20% darker than base
- Disabled: 50% opacity + gray foreground
- Focus: Primary color + 2px border

**5. Surfaces**
- Primary surface: #FFFFFF
- Secondary surface: #F5F5F5
- Overlay/modal: #FFFFFF with shadow

**Rules:**
- Pick your palette once, use it everywhere
- Don't add random colors mid-project
- Test contrast ratios (WCAG AA: 4.5:1 for text, 3:1 for UI)

### 4. Spacing System — The 8px Grid

**Every spacing value should be a multiple of 8:**
- 4px — micro-adjustments (rare)
- 8px — tight grouping (inline elements)
- 16px — standard spacing (padding, gaps)
- 24px — section separators
- 32px — major breaks
- 48px — page-level spacing

**Apply consistently:**
- Component padding: 12–16px
- Gap between elements: 8–16px
- Gap between sections: 24px+
- Page margins: 32–48px

**Test:** Measure distances with ruler. All should be multiples of 8 (allow 4px for typography adjustments).

### 5. Elevation & Shadows — Depth

**Use exactly 3 elevation levels:**

**Level 0** — No shadow (flat)
- Default state for most components

**Level 1** — Subtle (slightly raised)
- `<DropShadowEffect BlurRadius="4" ShadowDepth="1" Opacity="0.12"/>`
- Cards on hover, dropdowns, tooltips

**Level 2** — Medium (clearly floating)
- `<DropShadowEffect BlurRadius="8" ShadowDepth="2" Opacity="0.15"/>`
- Modals, popovers, expanded panels

**Level 3** — Heavy (critical overlay)
- `<DropShadowEffect BlurRadius="16" ShadowDepth="4" Opacity="0.20"/>`
- Full-screen modals, critical alerts only

**Rule:** Never go beyond Level 3. Restraint = professionalism.

### 6. Corner Radius — Subtle Softness

- Buttons: 4px (slightly rounded)
- Cards: 8px (noticeable softness)
- Dialogs: 8px
- Inputs: 4px
- Never use 0px or 50px (too harsh or too rounded)

### 7. Borders — Minimal & Intentional

- Default borders: 1px #D0D0D0
- Focus borders: 2px primary color
- Dividers: 1px #D0D0D0 or 1px #F5F5F5
- Never use heavy borders (2px+) except for focus states

---

## Complete Component Library

**See `references/component-library.md` for:**
- Button styles (primary, secondary, tertiary, ghost, danger)
- Input fields (text, password, number, search)
- Cards and containers
- Data grid styling
- Lists and repeaters
- Modals and dialogs
- Navigation bars and menus
- Progress indicators
- Form field groups
- Alerts and notifications
- Each with XAML code ready to copy

---

## UI Patterns — 20+ Production-Ready Scenarios

**See `references/ui-patterns.md` for:**
- Login/authentication forms
- Multi-step forms and wizards
- Data entry grids with inline editing
- Master-detail dashboards
- Search + results layouts
- Settings/preferences panels
- Navigation patterns (sidebar, top nav, breadcrumbs)
- Modal dialogs (confirm, alert, input, selection)
- Empty states and loading states
- Error pages
- Onboarding flows
- Card-based layouts
- Timeline views
- Notification systems
- Export/import flows

Each pattern includes:
- Visual mockup description
- Complete XAML implementation
- Best practices
- Common pitfalls to avoid

---

## Theming System — Dark Mode & Custom Themes

**See `references/theming-system.md` for:**
- Dynamic theme switching at runtime
- Dark mode + light mode complete system
- Custom color schemes
- Resource dictionary organization
- Brand-specific themes
- Complete App.xaml template
- Real-time theme switching code

Build switchable themes so users can choose their preference.

---

## Responsive Design — Adaptive Layouts

**See `references/responsive-design.md` for:**
- Breakpoint strategies (mobile, tablet, desktop, ultrawide)
- Responsive grids
- Auto-sizing columns
- Layout adaptation code
- Window resize handling
- Adaptive navigation patterns

Ensure layouts work from 800px to 4K displays.

---

## Animations & Transitions — Professional Polish

**See `references/animations.md` for:**
- Fade, slide, scale animations
- Timing functions (ease-in, ease-out, ease-in-out)
- Storyboard implementation
- Micro-interactions
- Page transitions
- Loading animations
- Hover effects
- When NOT to animate (performance)

Add subtle motion that enhances, not distracts.

---

## Accessibility — WCAG Compliance

**See `references/accessibility.md` for:**
- Contrast requirements (WCAG AA minimum)
- Keyboard navigation implementation
- Screen reader support
- Focus management and visibility
- ARIA labels and descriptions
- Color-blind testing
- Motion/animation accessibility
- Testing tools and checklist

Ship accessible software that works for everyone.

---

## WPF Troubleshooting — Fix Common Issues

**See `references/troubleshooting.md` for:**
- StackPanel vs Grid performance
- ListView/DataGrid virtualization
- Margin/padding vs Alignment confusion
- TextBlock rendering issues
- Button/Control styling inheritance
- Focus rectangle problems
- Layout measurement issues
- Control template gotchas
- Binding performance issues

Diagnose and fix the 20 most common WPF design problems.

---

## Performance Optimization — Keep It Snappy

**See `references/performance.md` for:**
- UI virtualization for large lists
- Deferred loading patterns
- Rendering optimization techniques
- Binding performance
- Visual tree efficiency
- Memory management
- CPU usage reduction
- Profiling tools and methods

Ensure your app stays responsive even with complex UIs.

---

## Quality Testing — Before You Ship

**See `references/testing.md` for:**
- Design review checklist (25-point verification)
- Visual regression testing
- Responsive design testing across breakpoints
- Accessibility automated testing
- Performance benchmarking
- User testing guidance
- Common quality failures to catch
- Release readiness criteria

Never ship without running through the complete checklist.

---

## Design Review Checklist — Production Release Gatekeeper

Run this before deployment:

### Visual & Hierarchy
- [ ] Primary action is visually dominant (color, size, placement)
- [ ] Secondary actions are visually quieter
- [ ] Information grouped logically
- [ ] No competing visual elements

### Typography
- [ ] Exactly 4–5 font sizes used (no random sizes)
- [ ] Single font family throughout
- [ ] Font weights: Regular, Medium, SemiBold, Bold only
- [ ] Line height appropriate for readability (1.4–1.6)

### Color
- [ ] Only colors from established palette used
- [ ] Semantic colors used correctly (green=success, red=error)
- [ ] Primary accent used sparingly (interactive elements only)
- [ ] Text contrast passes WCAG AA (4.5:1)
- [ ] UI component contrast passes WCAG AA (3:1)

### Spacing
- [ ] All spacing is multiples of 8px
- [ ] Component padding consistent (12–16px)
- [ ] Gaps between elements: 8–16px (tight) or 24px+ (sections)
- [ ] No random pixel adjustments

### Components
- [ ] All buttons have hover/focus/disabled states
- [ ] Input fields have focus indicator (2px border)
- [ ] Error states show color + icon + text
- [ ] Disabled states look disabled (reduced opacity)
- [ ] Loading states present (spinners, skeleton screens)

### Elevation & Shadows
- [ ] Shadows used only for elevation (Level 0–3)
- [ ] No multiple competing shadows
- [ ] Borders minimal (1px, gray color)
- [ ] Focus borders visible (2px, primary color)

### Responsive
- [ ] Layout adapts to window resize
- [ ] No horizontal scrollbars
- [ ] Readable on 800px and 4K displays
- [ ] Touch targets minimum 44px (mobile consideration)

### Accessibility
- [ ] All text has sufficient contrast
- [ ] Focus visible on all interactive elements
- [ ] Keyboard navigation works (Tab, Enter, Esc)
- [ ] Screen reader labels present (Name, Description)
- [ ] No color alone used to convey information

### Performance
- [ ] Lists with 100+ items use virtualization
- [ ] No layout thrashing (no circular dependencies)
- [ ] Renders smoothly (60 FPS, no stuttering)
- [ ] No memory leaks (profiles clean)

### Consistency
- [ ] Same component looks the same everywhere
- [ ] Patterns repeated consistently
- [ ] No orphaned elements (everything follows patterns)
- [ ] Visual language cohesive across all screens

### Polish
- [ ] No harsh edges (CornerRadius applied)
- [ ] Transitions smooth (no jumpy animations)
- [ ] Microinteractions delight (hover feedback)
- [ ] Overall appearance professional (intentional design)

---

## Workflow: From Sketch to Production

1. **Define Your System** (2–3 hours)
   - Pick primary color
   - Establish 5 semantic colors
   - Create gray scale (7 shades)
   - Choose typography (font + 5 sizes)
   - Set spacing grid (8px multiples)
   - Define component library

2. **Build Component Library** (4–6 hours)
   - Buttons (all states)
   - Inputs (all states)
   - Cards
   - Lists/DataGrids
   - Modals
   - Store in shared resource dictionary

3. **Design Layouts** (per-feature)
   - Establish page structure
   - Apply spacing system
   - Use component library
   - Test responsive behavior

4. **Implement & Refine** (per-feature)
   - Build XAML using library
   - Apply focus states
   - Add transitions
   - Test accessibility

5. **Quality Gate** (pre-release)
   - Run complete checklist
   - Performance profile
   - Accessibility audit
   - User testing (if budget allows)

6. **Ship** ✅

---

## Key Mindset Shifts

**From:** Random colors, sizes, spacing → **To:** Intentional system
**From:** Component inconsistency → **To:** Reusable library
**From:** Hoping it looks good → **To:** Systematic validation

**Professional design isn't complicated. It's consistent.**

---

## Next Steps

1. **Read the component library** (`references/component-library.md`) — Copy the baseline styles
2. **Choose your UI pattern** (`references/ui-patterns.md`) — Pick the right layout for your app
3. **Set up theming** (`references/theming-system.md`) — Create your App.xaml resource dictionary
4. **Implement accessibility** (`references/accessibility.md`) — Add screen reader support, keyboard nav
5. **Test quality** (`references/testing.md`) — Run the 25-point checklist
6. **Ship with confidence** ✅

---

## How to Use This Skill

**Mention any of these to load relevant sections:**
- "Help me design a form"
- "What button styles should I use?"
- "How do I add dark mode?"
- "Why is my layout broken?"
- "Check my UI against the quality checklist"
- "Make my app look professional"
- "My grid is slow, how do I fix it?"
- "Add focus/keyboard navigation"
- "I need a complete design system"

I'll load the relevant reference and provide exact XAML, patterns, and guidance.

---

**Ready to build something professional? Share a screenshot, describe your current UI, or tell me what you're building. I'll guide you through every step.**
